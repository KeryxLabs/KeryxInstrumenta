using System.Net;
using System.Net.Sockets;
using System.Text;
using AdaptiveCodecContextEngine.Models;
using AdaptiveCodecContextEngine.Models.Rpc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

public class JsonRpcServer : BackgroundService
{
    private readonly IAccQueryService _queryService;
    private readonly ILogger<JsonRpcServer> _logger;
    private readonly LspStreamManager _lspStreamManager;
    private readonly int _port;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        TypeInfoResolver = ACCJsonContext.Default,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public JsonRpcServer(
        IAccQueryService queryService,
        LspStreamManager streamManager,
        ILogger<JsonRpcServer> logger,
        IConfiguration configuration
    )
    {
        _queryService = queryService;
        _logger = logger;
        _lspStreamManager = streamManager;
        _port = configuration.GetValue<int>("Acc:JsonRpc:Port", 9339);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var listener = new TcpListener(IPAddress.Any, _port);
        listener.Start();

        _logger.LogInformation($"JSON-RPC server listening on localhost:{_port}");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var client = await listener.AcceptTcpClientAsync(stoppingToken);
                _ = HandleClientAsync(client, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error accepting client");
            }
        }

        listener.Stop();
    }

    // In JsonRpcServer.HandleClientAsync
    private async Task HandleClientAsync(TcpClient client, CancellationToken ct)
    {
        try
        {
            await using var stream = client.GetStream();
            var encoding = new UTF8Encoding(false); // false = no BOM
            using var reader = new StreamReader(stream, encoding);
            await using var writer = new StreamWriter(stream, encoding) { AutoFlush = true };

            // Read ONE request
            var line = await reader.ReadLineAsync(ct);
            if (string.IsNullOrEmpty(line))
                return;

            _logger.LogDebug($"RPC Request: {line}");

            var response = await ProcessRequestAsync(line);
            var responseJson = JsonSerializer.Serialize(
                response,
                ACCJsonContext.Default.JsonRpcResponse
            );

            _logger.LogDebug($"RPC Response: {responseJson}");

            await writer.WriteLineAsync(responseJson);
            await writer.FlushAsync();

            // Close connection after response
            await Task.Delay(100, ct); // Brief delay to ensure client receives
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling RPC client");
        }
        finally
        {
            client.Close(); // Explicit close
        }
    }

    // private async Task HandleClientAsync(TcpClient client, CancellationToken ct)
    // {
    //     try
    //     {
    //         await using var stream = client.GetStream();
    //         using var reader = new StreamReader(stream, Encoding.UTF8);
    //         await using var writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };

    //         while (!ct.IsCancellationRequested)
    //         {
    //             var line = await reader.ReadLineAsync(ct);
    //             if (string.IsNullOrEmpty(line)) break;

    //             _logger.LogDebug($"RPC Request: {line}");

    //             var response = await ProcessRequestAsync(line);
    //             var responseJson = JsonSerializer.Serialize(response, ACCJsonContext.Default.JsonRpcResponse);

    //             _logger.LogDebug($"RPC Response: {responseJson}");

    //             await writer.WriteLineAsync(responseJson);
    //         }
    //     }
    //     catch (Exception ex)
    //     {
    //         _logger.LogError(ex, "Error handling RPC client");
    //     }
    //     finally
    //     {
    //         client.Close();
    //     }
    // }

    private async Task<JsonRpcResponse> ProcessRequestAsync(string json)
    {
        try
        {
            var request = JsonSerializer.Deserialize(json, ACCJsonContext.Default.JsonRpcRequest);

            if (request == null)
            {
                return CreateErrorResponse(null, -32700, "Parse error");
            }

            var result = await ExecuteMethodAsync(request);

            return new JsonRpcResponse
            {
                JsonRpc = "2.0",
                Id = request.Id,
                Result = result,
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing request");
            return CreateErrorResponse(null, -32603, $"Internal error: {ex.Message}");
        }
    }

    private async Task<JsonElement> ExecuteMethodAsync(JsonRpcRequest request)
    {
        return request.Method switch
        {
            "acc.getNode" => JsonSerializer.SerializeToElement(
                await HandleGetNodeAsync(request.Params),
                ACCJsonContext.Default.NodeDto
            ),

            "acc.queryRelations" => JsonSerializer.SerializeToElement(
                await HandleQueryRelationsAsync(request.Params),
                ACCJsonContext.Default.NodeDto
            ),

            "acc.queryDependencies" => JsonSerializer.SerializeToElement(
                await HandleQueryDependenciesAsync(request.Params),
                ACCJsonContext.Default.ListNodeDto
            ),

            "acc.queryPatterns" => JsonSerializer.SerializeToElement(
                await HandleQueryPatternsAsync(request.Params),
                ACCJsonContext.Default.ListNodeDto
            ),

            "acc.search" => JsonSerializer.SerializeToElement(
                await HandleSearchAsync(request.Params),
                ACCJsonContext.Default.ListNodeDto
            ),

            "acc.getHighFriction" => JsonSerializer.SerializeToElement(
                await HandleGetHighFrictionAsync(request.Params),
                ACCJsonContext.Default.ListNodeDto
            ),

            "acc.getUnstable" => JsonSerializer.SerializeToElement(
                await HandleGetUnstableAsync(request.Params),
                ACCJsonContext.Default.ListNodeDto
            ),

            "acc.getStats" => JsonSerializer.SerializeToElement(
                await HandleGetStatsAsync(),
                ACCJsonContext.Default.ProjectStatsDto
            ),

            // New LSP stream management methods
            "acc.registerLspStream" => JsonSerializer.SerializeToElement(
                await HandleRegisterLspStreamAsync(request.Params),
                ACCJsonContext.Default.RegisterLspStreamResponse
            ),
            "acc.unregisterLspStream" => JsonSerializer.SerializeToElement(
                await HandleUnregisterLspStreamAsync(request.Params),
                ACCJsonContext.Default.Boolean
            ),
            "acc.listLspStreams" => JsonSerializer.SerializeToElement(
                HandleListLspStreams(),
                ACCJsonContext.Default.ListLspStreamInfo
            ),

            _ => default,
        };
    }

    private async Task<NodeDto?> HandleGetNodeAsync(JsonElement? paramsElement)
    {
        if (!paramsElement.HasValue)
            throw new ArgumentException("Missing params");

        var nodeId = paramsElement.Value.GetProperty("nodeId").GetString();
        if (nodeId == null)
            throw new ArgumentException("Missing nodeId");

        return await _queryService.GetNodeAsync(nodeId);
    }

    private async Task<NodeDto?> HandleQueryRelationsAsync(JsonElement? paramsElement)
    {
        if (!paramsElement.HasValue)
            throw new ArgumentException("Missing params");

        var @params = JsonSerializer.Deserialize(
            paramsElement.Value,
            ACCJsonContext.Default.QueryRelationsParams
        );
        if (@params == null)
            throw new ArgumentException("Invalid params");

        return await _queryService.QueryRelationsAsync(@params.NodeId, @params.IncludeScores);
    }

    private async Task<List<NodeDto>> HandleQueryDependenciesAsync(JsonElement? paramsElement)
    {
        if (!paramsElement.HasValue)
            throw new ArgumentException("Missing params");

        var @params = JsonSerializer.Deserialize(
            paramsElement.Value,
            ACCJsonContext.Default.QueryDependenciesParams
        );
        if (@params == null)
            throw new ArgumentException("Invalid params");

        var direction = Enum.Parse<DependencyDirection>(@params.Direction, ignoreCase: true);
        return await _queryService.QueryDependenciesAsync(
            @params.NodeId,
            direction,
            @params.MaxDepth,
            @params.IncludeScores
        );
    }

    private async Task<List<NodeDto>> HandleQueryPatternsAsync(JsonElement? paramsElement)
    {
        if (!paramsElement.HasValue)
            throw new ArgumentException("Missing params");

        var @params = JsonSerializer.Deserialize(
            paramsElement.Value,
            ACCJsonContext.Default.QueryPatternsParams
        );
        if (@params == null)
            throw new ArgumentException("Invalid params");

        return await _queryService.QueryPatternsAsync(@params.Profile, @params.Threshold);
    }

    private async Task<List<NodeDto>> HandleSearchAsync(JsonElement? paramsElement)
    {
        if (!paramsElement.HasValue)
            throw new ArgumentException("Missing params");

        var @params = JsonSerializer.Deserialize(
            paramsElement.Value,
            ACCJsonContext.Default.SearchParams
        );
        if (@params == null)
            throw new ArgumentException("Invalid params");

        return await _queryService.SearchByNameAsync(@params.Name, @params.Limit);
    }

    private async Task<List<NodeDto>> HandleGetHighFrictionAsync(JsonElement? paramsElement)
    {
        var @params = paramsElement.HasValue
            ? JsonSerializer.Deserialize(
                paramsElement.Value,
                ACCJsonContext.Default.GetHighFrictionParams
            )
            : new GetHighFrictionParams();

        return await _queryService.GetNodesWithHighFrictionAsync(
            @params?.MinFriction ?? 0.7,
            @params?.Limit ?? 20
        );
    }

    private async Task<List<NodeDto>> HandleGetUnstableAsync(JsonElement? paramsElement)
    {
        var @params = paramsElement.HasValue
            ? JsonSerializer.Deserialize(
                paramsElement.Value,
                ACCJsonContext.Default.GetUnstableParams
            )
            : new GetUnstableParams();

        return await _queryService.GetUnstableNodesAsync(
            @params?.MaxStability ?? 0.4,
            @params?.Limit ?? 20
        );
    }

    private async Task<ProjectStatsDto> HandleGetStatsAsync()
    {
        return await _queryService.GetProjectStatsAsync();
    }

    private JsonRpcResponse CreateErrorResponse(JsonElement? id, int code, string message)
    {
        return new JsonRpcResponse
        {
            JsonRpc = "2.0",
            Id = id,
            Error = new JsonRpcError { Code = code, Message = message },
        };
    }

    private async Task<RegisterLspStreamResponse> HandleRegisterLspStreamAsync(
        JsonElement? paramsElement
    )
    {
        if (!paramsElement.HasValue)
            throw new ArgumentException("Missing params");

        var @params = JsonSerializer.Deserialize(
            paramsElement.Value,
            ACCJsonContext.Default.RegisterLspStreamParams
        );
        if (@params == null)
            throw new ArgumentException("Invalid params");

        try
        {
            var streamType = Enum.Parse<LspStreamType>(@params.Type, ignoreCase: true);
            var streamId = await _lspStreamManager.RegisterStreamAsync(
                streamType,
                @params.Language,
                @params.Path,
                @params.Port
            );

            return new RegisterLspStreamResponse { Success = true, StreamId = streamId };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to register LSP stream");
            return new RegisterLspStreamResponse { Success = false, Error = ex.Message };
        }
    }

    private async Task<bool> HandleUnregisterLspStreamAsync(JsonElement? paramsElement)
    {
        if (!paramsElement.HasValue)
            throw new ArgumentException("Missing params");

        var @params = JsonSerializer.Deserialize(
            paramsElement.Value,
            ACCJsonContext.Default.UnregisterLspStreamParams
        );
        if (@params == null)
            throw new ArgumentException("Invalid params");

        return await _lspStreamManager.UnregisterStreamAsync(@params.StreamId);
    }

    private List<LspStreamInfo> HandleListLspStreams()
    {
        return _lspStreamManager.GetRegisteredStreams();
    }
}

// Add this to your existing ACC hosted service or create a new one

using AdaptiveCodecContextEngine.Models;
using AdaptiveCodecContextEngine.Models.Rpc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Sockets;
using System.Text;


public class JsonRpcServer : BackgroundService
{
    private readonly IAccQueryService _queryService;
    private readonly ILogger<JsonRpcServer> _logger;
    private readonly int _port;
    
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        TypeInfoResolver = ACCJsonContext.Default,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
    
    public JsonRpcServer(
        IAccQueryService queryService,
        ILogger<JsonRpcServer> logger,
        IConfiguration configuration)
    {
        _queryService = queryService;
        _logger = logger;
        _port = configuration.GetValue<int>("JsonRpc:Port", 9339);
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var listener = new TcpListener(IPAddress.Loopback, _port);
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
    
    private async Task HandleClientAsync(TcpClient client, CancellationToken ct)
    {
        try
        {
            await using var stream = client.GetStream();
            using var reader = new StreamReader(stream, Encoding.UTF8);
            await using var writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };
            
            while (!ct.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(ct);
                if (string.IsNullOrEmpty(line)) break;
                
                _logger.LogDebug($"RPC Request: {line}");
                
                var response = await ProcessRequestAsync(line);
                var responseJson = JsonSerializer.Serialize(response, ACCJsonContext.Default.JsonRpcResponse);
                
                _logger.LogDebug($"RPC Response: {responseJson}");
                
                await writer.WriteLineAsync(responseJson);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling RPC client");
        }
        finally
        {
            client.Close();
        }
    }
    
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
                Result = result
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
        "acc.getNode" => 
            JsonSerializer.SerializeToElement(await HandleGetNodeAsync(request.Params), ACCJsonContext.Default.NodeDto),
            
        "acc.queryRelations" => 
            JsonSerializer.SerializeToElement(await HandleQueryRelationsAsync(request.Params), ACCJsonContext.Default.NodeDto),
            
        "acc.queryDependencies" => 
            JsonSerializer.SerializeToElement(await HandleQueryDependenciesAsync(request.Params), ACCJsonContext.Default.ListNodeDto),
            
        "acc.queryPatterns" => 
            JsonSerializer.SerializeToElement(await HandleQueryPatternsAsync(request.Params), ACCJsonContext.Default.ListNodeDto),
            
        "acc.search" => 
            JsonSerializer.SerializeToElement(await HandleSearchAsync(request.Params), ACCJsonContext.Default.ListNodeDto),
            
        "acc.getHighFriction" => 
            JsonSerializer.SerializeToElement(await HandleGetHighFrictionAsync(request.Params), ACCJsonContext.Default.ListNodeDto),
            
        "acc.getUnstable" => 
            JsonSerializer.SerializeToElement(await HandleGetUnstableAsync(request.Params), ACCJsonContext.Default.ListNodeDto),
            
        "acc.getStats" => 
            JsonSerializer.SerializeToElement(await HandleGetStatsAsync(), ACCJsonContext.Default.ProjectStatsDto),
            
        _ => default
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
        
        var @params = JsonSerializer.Deserialize(paramsElement.Value, ACCJsonContext.Default.QueryRelationsParams);
        if (@params == null)
            throw new ArgumentException("Invalid params");
        
        return await _queryService.QueryRelationsAsync(@params.NodeId, @params.IncludeScores);
    }
    
    private async Task<List<NodeDto>> HandleQueryDependenciesAsync(JsonElement? paramsElement)
    {
        if (!paramsElement.HasValue)
            throw new ArgumentException("Missing params");
        
        var @params = JsonSerializer.Deserialize(paramsElement.Value, ACCJsonContext.Default.QueryDependenciesParams);
        if (@params == null)
            throw new ArgumentException("Invalid params");
        
        var direction = Enum.Parse<DependencyDirection>(@params.Direction, ignoreCase: true);
        return await _queryService.QueryDependenciesAsync(
            @params.NodeId, 
            direction, 
            @params.MaxDepth, 
            @params.IncludeScores);
    }
    
    private async Task<List<NodeDto>> HandleQueryPatternsAsync(JsonElement? paramsElement)
    {
        if (!paramsElement.HasValue)
            throw new ArgumentException("Missing params");
        
        var @params = JsonSerializer.Deserialize(paramsElement.Value, ACCJsonContext.Default.QueryPatternsParams);
        if (@params == null)
            throw new ArgumentException("Invalid params");
        
        return await _queryService.QueryPatternsAsync(@params.Profile, @params.Threshold);
    }
    
    private async Task<List<NodeDto>> HandleSearchAsync(JsonElement? paramsElement)
    {
        if (!paramsElement.HasValue)
            throw new ArgumentException("Missing params");
        
        var @params = JsonSerializer.Deserialize(paramsElement.Value, ACCJsonContext.Default.SearchParams);
        if (@params == null)
            throw new ArgumentException("Invalid params");
        
        return await _queryService.SearchByNameAsync(@params.Name, @params.Limit);
    }
    
    private async Task<List<NodeDto>> HandleGetHighFrictionAsync(JsonElement? paramsElement)
    {
        var @params = paramsElement.HasValue 
            ? JsonSerializer.Deserialize(paramsElement.Value, ACCJsonContext.Default.GetHighFrictionParams)
            : new GetHighFrictionParams();
        
        return await _queryService.GetNodesWithHighFrictionAsync(
            @params?.MinFriction ?? 0.7, 
            @params?.Limit ?? 20);
    }
    
    private async Task<List<NodeDto>> HandleGetUnstableAsync(JsonElement? paramsElement)
    {
        var @params = paramsElement.HasValue
            ? JsonSerializer.Deserialize(paramsElement.Value, ACCJsonContext.Default.GetUnstableParams)
            : new GetUnstableParams();
        
        return await _queryService.GetUnstableNodesAsync(
            @params?.MaxStability ?? 0.4,
            @params?.Limit ?? 20);
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
            Error = new JsonRpcError
            {
                Code = code,
                Message = message
            }
        };
    }
}

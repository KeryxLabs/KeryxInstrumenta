using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AccCli;

/// <summary>
/// Lightweight TCP JSON-RPC 2.0 client for the ACC engine.
/// Configure via AccEngine:Host / AccEngine:Port in appsettings.json
/// or via environment variables (AccEngine__Host, AccEngine__Port).
/// Defaults to localhost:9339.
/// Per-call host/port overrides (from --host / --port global flags) take precedence.
/// </summary>
internal sealed class AccEngineClient
{
    private readonly string _defaultHost;
    private readonly int _defaultPort;
    private readonly ILogger<AccEngineClient> _logger;

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static readonly JsonSerializerOptions PrettyOptions = new()
    {
        WriteIndented = true,
    };

    public AccEngineClient(IConfiguration configuration, ILogger<AccEngineClient> logger)
    {
        _defaultHost = configuration.GetValue<string>("AccEngine:Host") ?? "localhost";
        _defaultPort = configuration.GetValue<int>("AccEngine:Port", 9339);
        _logger = logger;
        _logger.LogDebug("ACC engine default: {Host}:{Port}", _defaultHost, _defaultPort);
    }

    /// <summary>
    /// Send a JSON-RPC request to the ACC engine.
    /// Pass <paramref name="globalOptions"/> to allow --host / --port flag overrides.
    /// </summary>
    public Task<string?> CallAsync(
        string method,
        object? @params = null,
        GlobalOptions? globalOptions = null,
        CancellationToken ct = default
    )                                                                                  {
        var host = globalOptions?.Host ?? _defaultHost;
        var port = globalOptions?.Port ?? _defaultPort;
        return SendAsync(method, @params, host, port, ct);
    }

    private async Task<string?> SendAsync(
        string method,
        object? @params,
        string host,
        int port,
        CancellationToken ct
    )
    {
        var envelope = new { jsonrpc = "2.0", id = 1, method, @params };
        var requestJson = JsonSerializer.Serialize(envelope, WriteOptions);

        _logger.LogDebug("→ {Host}:{Port} {Method} {Request}", host, port, method, requestJson);
        using var tcp = new TcpClient();
        await tcp.ConnectAsync(host, port, ct);
        var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        await using var stream = tcp.GetStream();
        using var reader = new StreamReader(stream, encoding);                             await using var writer = new StreamWriter(stream, encoding) { AutoFlush = true };

        await writer.WriteLineAsync(requestJson.AsMemory(), ct);
        await writer.FlushAsync(ct);
                                                                                           var responseLine = await reader.ReadLineAsync(ct);
        if (string.IsNullOrEmpty(responseLine))
        {
            _logger.LogWarning("← empty response for {Method}", method);
            return null;
        }

        _logger.LogDebug("← {Response}", responseLine);

        using var doc = JsonDocument.Parse(responseLine);
        var root = doc.RootElement;

        if (root.TryGetProperty("error", out var error))
        {
            var code = error.TryGetProperty("code", out var c) ? c.GetInt32() : 0;
            var msg  = error.TryGetProperty("message", out var m) ? m.GetString() : "unknown error";
            throw new AccEngineException(code, msg ?? "unknown error");
        }

        if (!root.TryGetProperty("result", out var result) || result.ValueKind == JsonValueKind.Null)
            return null;

        return JsonSerializer.Serialize(result, PrettyOptions);
    }                                                                              }

internal sealed class AccEngineException(int code, string message)
    : Exception($"ACC engine error {code}: {message}")
{
    public int Code { get; } = code;
}
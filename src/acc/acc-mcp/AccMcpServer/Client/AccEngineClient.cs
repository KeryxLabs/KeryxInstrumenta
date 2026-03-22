using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

/// <summary>
/// Lightweight TCP JSON-RPC 2.0 client for the ACC engine.
/// The ACC engine handles one request per connection (connect → write → read → close).
/// </summary>
internal class AccEngineClient
{
    private readonly string _host;
    private readonly int _port;
    private readonly ILogger<AccEngineClient> _logger;

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static readonly JsonSerializerOptions PrettyOptions = new() { WriteIndented = true };

    public AccEngineClient(IConfiguration configuration, ILogger<AccEngineClient> logger)
    {
        _host = configuration.GetValue<string>("AccEngine:Host") ?? "0.0.0.0";
        _port = configuration.GetValue<int>("AccEngine:Port", 9339);
        _logger = logger;
        _logger.LogInformation("Connected to: {Host}:{Port}", _host, _port);
    }

    /// <summary>
    /// Sends a JSON-RPC request and returns the pretty-printed result JSON,
    /// or null if the result was null / not found.
    /// </summary>
    public async Task<string?> CallAsync(
        string method,
        object? @params = null,
        CancellationToken ct = default
    )
    {
        var envelope = new
        {
            jsonrpc = "2.0",
            id = 1,
            method,
            @params,
        };
        var requestJson = JsonSerializer.Serialize(envelope, WriteOptions);

        _logger.LogDebug("ACC RPC → {Method}: {Request}", method, requestJson);

        using var tcp = new TcpClient();
        await tcp.ConnectAsync(_host, _port, ct);

        var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        await using var stream = tcp.GetStream();
        using var reader = new StreamReader(stream, encoding);
        await using var writer = new StreamWriter(stream, encoding) { AutoFlush = true };

        await writer.WriteLineAsync(requestJson.AsMemory(), ct);
        await writer.FlushAsync(ct);

        var responseLine = await reader.ReadLineAsync(ct);
        if (string.IsNullOrEmpty(responseLine))
        {
            _logger.LogWarning("ACC RPC ← empty response for {Method}", method);
            return null;
        }

        _logger.LogDebug("ACC RPC ← {Response}", responseLine);

        using var doc = JsonDocument.Parse(responseLine);
        var root = doc.RootElement;

        // Surface JSON-RPC errors as exceptions so tools can report them cleanly
        if (root.TryGetProperty("error", out var error))
        {
            var code = error.TryGetProperty("code", out var c) ? c.GetInt32() : 0;
            var msg = error.TryGetProperty("message", out var m) ? m.GetString() : "unknown error";
            throw new AccEngineException(code, msg ?? "unknown error");
        }

        if (
            !root.TryGetProperty("result", out var result)
            || result.ValueKind == JsonValueKind.Null
        )
            return null;

        return JsonSerializer.Serialize(result, PrettyOptions);
    }
}

internal sealed class AccEngineException(int code, string message)
    : Exception($"ACC engine error {code}: {message}")
{
    public int Code { get; } = code;
}

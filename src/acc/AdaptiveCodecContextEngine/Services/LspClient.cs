using System.Diagnostics;
using System.Text;
using AdaptiveCodecContextEngine.Diagnostics;
using AdaptiveCodecContextEngine.Models;
using AdaptiveCodecContextEngine.Models.Lsp;
using Microsoft.Extensions.Logging;

public class LspStreamListener
{
    private readonly Channel<LspMessageWithContext> _lspChannel;
    private readonly ILogger<LspStreamListener> _logger;
    private readonly AdaptiveContextInstrumentation _instrumentation;
    private readonly CancellationTokenSource _cts;
    public string Language { get; }
    public bool IsStopped => _cts.IsCancellationRequested;

    public LspStreamListener(
        Channel<LspMessageWithContext> lspChannel,
        ILogger<LspStreamListener> logger,
        AdaptiveContextInstrumentation instrumentation,
        string language
    )
    {
        _lspChannel = lspChannel;
        _logger = logger;
        _instrumentation = instrumentation;
        Language = language;
        _cts = new CancellationTokenSource();
    }

    /// <summary>
    /// Start listening to an LSP stdio stream (from stdin, named pipe, socket, etc.)
    /// </summary>
    public async Task ListenAsync(Stream inputStream, CancellationToken ct)
    {
        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, _cts.Token);

        try
        {
            using var reader = new StreamReader(inputStream, Encoding.UTF8, leaveOpen: true);

            _logger.LogInformation($"LSP listener started for language: {Language}");

            while (!linkedCts.Token.IsCancellationRequested)
            {
                try
                {
                    var message = await ReadLspMessageAsync(reader, linkedCts.Token);

                    if (message != null)
                    {
                        // Tag message with language for routing
                        await _lspChannel.Writer.WriteAsync(message, linkedCts.Token);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error reading LSP message for {Language}");
                }
            }
        }
        finally
        {
            _logger.LogInformation($"LSP listener stopped for language: {Language}");
        }
    }

    private async Task<LspMessageWithContext?> ReadLspMessageAsync(
        StreamReader reader,
        CancellationToken ct
    )
    {
        using var activity = _instrumentation.ActivitySource.StartActivity(
            nameof(ReadLspMessageAsync)
        );

        // Read Content-Length header
        string? headerLine;
        int contentLength = 0;

        while ((headerLine = await reader.ReadLineAsync(ct)) != null)
        {
            if (string.IsNullOrWhiteSpace(headerLine))
            {
                // Empty line = end of headers
                break;
            }

            if (headerLine.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
            {
                var lengthStr = headerLine.Substring("Content-Length:".Length).Trim();
                if (!int.TryParse(lengthStr, out contentLength))
                {
                    _logger.LogWarning($"Invalid Content-Length: {lengthStr}");
                    return null;
                }
            }
        }

        if (contentLength == 0)
        {
            // No more messages (EOF or invalid)
            return null;
        }

        // Read content
        var buffer = new char[contentLength];
        var totalRead = 0;

        while (totalRead < contentLength)
        {
            var read = await reader.ReadAsync(buffer, totalRead, contentLength - totalRead);
            if (read == 0)
            {
                _logger.LogWarning("Unexpected end of stream while reading message content");
                return null;
            }
            totalRead += read;
        }

        var json = new string(buffer, 0, totalRead);

        _logger.LogDebug($"LSP[{Language}] message received: {json}");

        return LspMessageParser.Parse(json) is LspMessage message
            ? new(message, Language, activity?.Context)
            : null;
    }

    // }
    //     private async Task<LspMessage?> ReadLspMessageAsync(StreamReader reader, CancellationToken ct)
    //     {
    //         // Read Content-Length header
    //         string? headerLine;
    //         int contentLength = 0;

    //         while ((headerLine = await reader.ReadLineAsync(ct)) != null)
    //         {
    //             if (string.IsNullOrWhiteSpace(headerLine))
    //             {
    //                 // Empty line = end of headers
    //                 break;
    //             }

    //             if (headerLine.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
    //             {
    //                 var lengthStr = headerLine.Substring("Content-Length:".Length).Trim();
    //                 if (!int.TryParse(lengthStr, out contentLength))
    //                 {
    //                     _logger.LogWarning($"Invalid Content-Length: {lengthStr}");
    //                     return null;
    //                 }
    //             }
    //             // Ignore other headers (Content-Type, etc.)
    //         }

    //         if (contentLength == 0)
    //         {
    //             return null;
    //         }

    //         // Read content
    //         var buffer = new char[contentLength];
    //         var totalRead = 0;

    //         while (totalRead < contentLength)
    //         {
    //             var read = await reader.ReadAsync(buffer, totalRead, contentLength - totalRead);
    //             if (read == 0)
    //             {
    //                 _logger.LogWarning("Unexpected end of stream");
    //                 return null;
    //             }
    //             totalRead += read;
    //         }

    //         var json = new string(buffer, 0, totalRead);

    //         _logger.LogDebug($"LSP[{Language}] message: {json}");

    //         // Parse
    //         return LspMessageParser.Parse(json);
    //     }

    public void Stop()
    {
        _cts.Cancel();
    }
}

// Wrapper to track which language/LSP a message came from
public record LspMessageWithContext(
    LspMessage Message,
    string Language,
    ActivityContext? ParentContext
);

public record LspRequest
{
    public required string JsonRpc { get; init; }
    public required Guid Id { get; init; }
    public required string Method { get; init; }
    public required LspRequestParams Params { get; init; }
}

public record LspRequestParams
{
    public TextDocument? TextDocument { get; init; }
    public Position? Position { get; init; }
    public LspItem? Item { get; init; }
}

public record TextDocument
{
    public required string Uri { get; init; }
}

// {
//             jsonrpc = "2.0",
//             id = requestId,
//             method = "callHierarchy/outgoingCalls",
//             @params = new
//             {
//                 item = new
//                 {
//                     uri,
//                     range = new { start = position, end = position },
//                     selectionRange = new { start = position, end = position }
//                 }
//             }
//         };
public record LspItem
{
    public required string Uri { get; init; }
    public required LspRange Range { get; init; }
    public required LspRange SelectionRange { get; init; }
}

public record LspRange
{
    public required Position Start { get; set; }
    public required Position End { get; set; }
}

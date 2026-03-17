using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using AdaptiveCodecContextEngine.Diagnostics;
using Microsoft.Extensions.Logging;

public class LspStreamManager
{
    private readonly Channel<LspMessageWithContext> _lspChannel;
    private readonly ILogger<LspStreamManager> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly AdaptiveContextInstrumentation _instrumentation;
    private readonly ConcurrentDictionary<string, LspStreamListener> _listeners = new();
    private readonly ConcurrentDictionary<string, Task> _listenerTasks = new();

    public LspStreamManager(
        Channel<LspMessageWithContext> lspChannel,
        ILogger<LspStreamManager> logger,
        ILoggerFactory loggerFactory,
        AdaptiveContextInstrumentation instrumentation
    )
    {
        _lspChannel = lspChannel;
        _logger = logger;
        _loggerFactory = loggerFactory;
        _instrumentation = instrumentation;
    }

    /// <summary>
    /// Register and start a new LSP stream dynamically
    /// </summary>
    public async Task<string> RegisterStreamAsync(
        LspStreamType type,
        string language,
        string? path = null,
        int? port = null,
        CancellationToken ct = default
    )
    {
        using var activity = _instrumentation.ActivitySource.StartActivity(
            nameof(RegisterStreamAsync)
        );

        var streamId = GenerateStreamId(type, language, path, port);

        activity?.SetTag("lsp.stream.id", streamId);
        activity?.SetTag("lsp.stream.type", type.ToString());
        activity?.SetTag("lsp.language", language);

        if (_listeners.ContainsKey(streamId))
        {
            activity?.AddEvent(
                new ActivityEvent(
                    "Stream Registry Collision",
                    tags: new() { ["stream_id"] = streamId }
                )
            );
            activity?.SetStatus(ActivityStatusCode.Ok, "Duplicate ignored");
            _logger.LogWarning("Stream already registered {streamId}", streamId);
            return streamId;
        }

        var listener = new LspStreamListener(
            _lspChannel,
            _loggerFactory.CreateLogger<LspStreamListener>(),
            _instrumentation,
            language
        );
        _listeners[streamId] = listener;

        var task = type switch
        {
            LspStreamType.Stdin => ListenStdinAsync(listener, ct),
            LspStreamType.Pipe => ListenNamedPipeAsync(listener, path!, ct),
            LspStreamType.Tcp => ListenTcpAsync(listener, port!.Value, ct),
            _ => throw new ArgumentException($"Unknown stream type: {type}"),
        };

        _listenerTasks[streamId] = task;
        activity?.SetTag("engine.active_streams", _listeners.Count);
        activity?.SetStatus(ActivityStatusCode.Ok);
        return streamId;
    }

    /// <summary>
    /// Unregister and stop a specific stream
    /// </summary>
    public async Task<bool> UnregisterStreamAsync(string streamId)
    {
        using var activity = _instrumentation.ActivitySource.StartActivity(
            nameof(UnregisterStreamAsync)
        );
        activity?.SetTag("lsp.stream.id", streamId);

        if (!_listeners.TryRemove(streamId, out var listener))
        {
            activity?.AddEvent(
                new ActivityEvent(
                    "Stream Registry Not Found",
                    tags: new() { ["stream_id"] = streamId }
                )
            );
            activity?.SetStatus(ActivityStatusCode.Ok, "Not founds ignored");
            return false;
        }

        listener.Stop();
        activity?.AddEvent(new ActivityEvent("Listerner Stopped"));

        if (_listenerTasks.TryRemove(streamId, out var task))
        {
            try
            {
                await task;
            }
            catch (OperationCanceledException)
            {
                // Expected when stopping
            }
        }
        activity?.SetTag("engine.active_streams", _listeners.Count);
        activity?.SetStatus(ActivityStatusCode.Ok);

        return true;
    }

    /// <summary>
    /// List all registered streams
    /// </summary>
    public List<LspStreamInfo> GetRegisteredStreams()
    {
        return
        [
            .. _listeners.Select(kvp => new LspStreamInfo
            {
                StreamId = kvp.Key,
                Language = kvp.Value.Language,
                IsActive = !kvp.Value.IsStopped,
            }),
        ];
    }

    private async Task ListenStdinAsync(LspStreamListener listener, CancellationToken ct)
    {
        using var activity = _instrumentation.ActivitySource.StartActivity(
            nameof(ListenStdinAsync)
        );
        activity?.SetTag("lsp.stream.type", LspStreamType.Stdin.ToString());
        activity?.SetTag("lsp.language", listener.Language);
        activity?.AddEvent(
            new ActivityEvent(
                "Listening on stdin",
                tags: new() { ["language"] = listener.Language }
            )
        );

        var stdin = Console.OpenStandardInput();
        await listener.ListenAsync(stdin, ct);
    }

    private async Task ListenNamedPipeAsync(
        LspStreamListener listener,
        string pipeName,
        CancellationToken ct
    )
    {
        using var activity = _instrumentation.ActivitySource.StartActivity(
            nameof(ListenNamedPipeAsync)
        );
        activity?.SetTag("lsp.stream.type", LspStreamType.Pipe.ToString());
        activity?.SetTag("lsp.language", listener.Language);
#if WINDOWS
        var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.In);
        await pipe.ConnectAsync(ct);
        activity?.AddEvent(
            new ActivityEvent("NamedPipeConnected", tags: new() { ["pipe.name"] = pipeName })
        );
        await listener.ListenAsync(pipe, ct);
#else
        // Unix domain socket
        var socketPath = pipeName.StartsWith("/") ? pipeName : Path.Combine("/tmp", pipeName);

        // Wait for socket to exist (editor might create it)
        while (!File.Exists(socketPath) && !ct.IsCancellationRequested)
        {
            activity?.AddEvent(
                new ActivityEvent("WaitingForSocket", tags: new() { ["socket.path"] = socketPath })
            );
            _logger.LogDebug("Waiting for socket: {SocketPath}", socketPath);
            await Task.Delay(100, ct);
        }

        try
        {
            var socket = new Socket(
                AddressFamily.Unix,
                SocketType.Stream,
                ProtocolType.Unspecified
            );
            await socket.ConnectAsync(new UnixDomainSocketEndPoint(socketPath), ct);
            activity?.AddEvent(
                new ActivityEvent("SocketConnected", tags: new() { ["socket.path"] = socketPath })
            );
            var stream = new NetworkStream(socket, ownsSocket: true);
            await listener.ListenAsync(stream, ct);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddEvent(
                new ActivityEvent(
                    "SocketError",
                    tags: new() { ["socket.path"] = socketPath, ["error.message"] = ex.Message }
                )
            );
            _logger.LogError(ex, "Error connecting to socket: {SocketPath}", socketPath);
            throw;
        }
#endif
    }

    private async Task ListenTcpAsync(LspStreamListener listener, int port, CancellationToken ct)
    {
        using var activity = _instrumentation.ActivitySource.StartActivity(nameof(ListenTcpAsync));
        activity?.SetTag("lsp.stream.type", LspStreamType.Tcp.ToString());
        activity?.SetTag("lsp.language", listener.Language);
        activity?.SetTag("tcp.port", port);

        var tcpListener = new TcpListener(IPAddress.Loopback, port);

        try
        {
            tcpListener.Start();
            activity?.AddEvent(
                new ActivityEvent("TcpListenerStarted", tags: new() { ["port"] = port })
            );

            while (!ct.IsCancellationRequested)
            {
                var client = await tcpListener.AcceptTcpClientAsync(ct);
                activity?.AddEvent(
                    new ActivityEvent("ClientConnected", tags: new() { ["port"] = port })
                );

                // Handle each client in a separate task so we can accept more connections
                _ = Task.Run(
                    async () =>
                    {
                        try
                        {
                            var stream = client.GetStream();
                            await listener.ListenAsync(stream, ct);
                        }
                        catch (Exception ex)
                        {
                            activity?.AddEvent(
                                new ActivityEvent(
                                    "ClientHandlerError",
                                    tags: new() { ["port"] = port, ["error.message"] = ex.Message }
                                )
                            );
                            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                            _logger.LogError(ex, "Error handling client on port {Port}", port);
                        }
                        finally
                        {
                            client.Close();
                            activity?.AddEvent(
                                new ActivityEvent(
                                    "ClientDisconnected",
                                    tags: new() { ["port"] = port }
                                )
                            );
                            activity?.SetStatus(ActivityStatusCode.Ok);
                        }
                    },
                    ct
                );
            }
        }
        catch (OperationCanceledException)
        {
            activity?.SetStatus(ActivityStatusCode.Ok, "Listener cancelled");
            activity?.AddEvent(
                new ActivityEvent("TCP listener cancelled on port", tags: new() { ["port"] = port })
            );
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddEvent(
                new ActivityEvent(
                    "TcpListenerError",
                    tags: new() { ["port"] = port, ["error.message"] = ex.Message }
                )
            );
        }
        finally
        {
            tcpListener.Stop();
            activity?.AddEvent(
                new ActivityEvent("TcpListenerStopped", tags: new() { ["port"] = port })
            );
            activity?.SetStatus(ActivityStatusCode.Ok, "Listener Stopped");
        }
    }

    private string GenerateStreamId(LspStreamType type, string language, string? path, int? port)
    {
        return type switch
        {
            LspStreamType.Stdin => $"stdin-{language}",
            LspStreamType.Pipe => $"pipe-{language}-{path}",
            LspStreamType.Tcp => $"tcp-{language}-{port}",
            _ => throw new ArgumentException($"Unknown type: {type}"),
        };
    }

    public void StopAll()
    {
        using var activity = _instrumentation.ActivitySource.StartActivity(nameof(StopAll));
        activity?.SetTag("engine.active_streams", _listeners.Count);

        foreach (var listener in _listeners.Values)
        {
            listener.Stop();
            activity?.AddEvent(
                new ActivityEvent(
                    "StoppedListener",
                    tags: new() { ["language"] = listener.Language }
                )
            );
        }
    }
}

public enum LspStreamType
{
    Stdin,
    Pipe,
    Tcp,
}

public record LspStreamInfo
{
    public string StreamId { get; init; } = null!;
    public string Language { get; init; } = null!;
    public bool IsActive { get; init; }
}

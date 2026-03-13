using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;

public class LspStreamManager
{
    private readonly Channel<LspMessageWithContext> _lspChannel;
    private readonly ILogger<LspStreamManager> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ConcurrentDictionary<string, LspStreamListener> _listeners = new();
    private readonly ConcurrentDictionary<string, Task> _listenerTasks = new();
    
    public LspStreamManager(
        Channel<LspMessageWithContext> lspChannel,
        ILogger<LspStreamManager> logger,
        ILoggerFactory loggerFactory)
    {
        _lspChannel = lspChannel;
        _logger = logger;
        _loggerFactory = loggerFactory;
    }
    
    /// <summary>
    /// Register and start a new LSP stream dynamically
    /// </summary>
    public async Task<string> RegisterStreamAsync(
        LspStreamType type,
        string language,
        string? path = null,
        int? port = null,
        CancellationToken ct = default)
    {
        var streamId = GenerateStreamId(type, language, path, port);
        
        if (_listeners.ContainsKey(streamId))
        {
            _logger.LogWarning($"Stream already registered: {streamId}");
            return streamId;
        }
        
        _logger.LogInformation($"Registering LSP stream: {streamId}");
        
        var listener = new LspStreamListener(_lspChannel, _loggerFactory.CreateLogger<LspStreamListener>(), language);
        _listeners[streamId] = listener;
        
        // Start listening in background
        var task = type switch
        {
            LspStreamType.Stdin => ListenStdinAsync(listener, ct),
            LspStreamType.Pipe => ListenNamedPipeAsync(listener, path!, ct),
            LspStreamType.Tcp => ListenTcpAsync(listener, port!.Value, ct),
            _ => throw new ArgumentException($"Unknown stream type: {type}")
        };
        
        _listenerTasks[streamId] = task;
        
        return streamId;
    }
    
    /// <summary>
    /// Unregister and stop a specific stream
    /// </summary>
    public async Task<bool> UnregisterStreamAsync(string streamId)
    {
        if (!_listeners.TryRemove(streamId, out var listener))
        {
            return false;
        }
        
        _logger.LogInformation($"Unregistering LSP stream: {streamId}");
        
        listener.Stop();
        
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
        
        return true;
    }
    
    /// <summary>
    /// List all registered streams
    /// </summary>
    public List<LspStreamInfo> GetRegisteredStreams()
    {
        return _listeners.Select(kvp => new LspStreamInfo
        {
            StreamId = kvp.Key,
            Language = kvp.Value.Language,
            IsActive = !kvp.Value.IsStopped
        }).ToList();
    }
    
    private async Task ListenStdinAsync(LspStreamListener listener, CancellationToken ct)
    {
        var stdin = Console.OpenStandardInput();
        await listener.ListenAsync(stdin, ct);
    }
    
    private async Task ListenNamedPipeAsync(LspStreamListener listener, string pipeName, CancellationToken ct)
    {
        #if WINDOWS
        var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.In);
        await pipe.ConnectAsync(ct);
        await listener.ListenAsync(pipe, ct);
        #else
        // Unix domain socket
        var socketPath = pipeName.StartsWith("/") ? pipeName : Path.Combine("/tmp", pipeName);
        
        // Wait for socket to exist (editor might create it)
        while (!File.Exists(socketPath) && !ct.IsCancellationRequested)
        {
            _logger.LogDebug($"Waiting for socket: {socketPath}");
            await Task.Delay(100, ct);
        }
        
        var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        await socket.ConnectAsync(new UnixDomainSocketEndPoint(socketPath), ct);
        var stream = new NetworkStream(socket, ownsSocket: true);
        await listener.ListenAsync(stream, ct);
        #endif
    }
    
    private async Task ListenTcpAsync(LspStreamListener listener, int port, CancellationToken ct)
    {
        var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, port, ct);
        var stream = client.GetStream();
        await listener.ListenAsync(stream, ct);
    }
    
    private string GenerateStreamId(LspStreamType type, string language, string? path, int? port)
    {
        return type switch
        {
            LspStreamType.Stdin => $"stdin-{language}",
            LspStreamType.Pipe => $"pipe-{language}-{path}",
            LspStreamType.Tcp => $"tcp-{language}-{port}",
            _ => throw new ArgumentException($"Unknown type: {type}")
        };
    }
    
    public void StopAll()
    {
        foreach (var listener in _listeners.Values)
        {
            listener.Stop();
        }
    }
}

public enum LspStreamType
{
    Stdin,
    Pipe,
    Tcp
}

public record LspStreamInfo
{
    public string StreamId { get; init; } = null!;
    public string Language { get; init; } = null!;
    public bool IsActive { get; init; }
}
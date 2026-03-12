using System.Text;
using AdaptiveCodecContextEngine.Models.Lsp;

public class LspClient
{
    private readonly Stream _stdin;
    private int _requestIdCounter = 0;
    
    public LspClient(Stream stdin)
    {
        _stdin = stdin;
    }
    
    public async Task RequestOutgoingCallsAsync(object requestId, string uri, Position position)
    {
        var request = new
        {
            jsonrpc = "2.0",
            id = requestId,
            method = "callHierarchy/outgoingCalls",
            @params = new
            {
                item = new
                {
                    uri,
                    range = new { start = position, end = position },
                    selectionRange = new { start = position, end = position }
                }
            }
        };
        
        await SendRequest(request);
    }
    
    public async Task RequestDefinitionAsync(object requestId, string uri, Position position)
    {
        var request = new
        {
            jsonrpc = "2.0",
            id = requestId,
            method = "textDocument/definition",
            @params = new
            {
                textDocument = new { uri },
                position
            }
        };
        
        await SendRequest(request);
    }
    
    private async Task SendRequest(object request)
    {
        var json = JsonSerializer.Serialize(request);
        var content = Encoding.UTF8.GetBytes(json);
        var header = $"Content-Length: {content.Length}\r\n\r\n";
        var headerBytes = Encoding.UTF8.GetBytes(header);
        
        await _stdin.WriteAsync(headerBytes);
        await _stdin.WriteAsync(content);
        await _stdin.FlushAsync();
    }
}
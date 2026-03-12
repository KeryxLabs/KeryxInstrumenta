using System.Text;
using AdaptiveCodecContextEngine.Models;
using AdaptiveCodecContextEngine.Models.Lsp;

public class LspClient
{
    private readonly Stream _stdin;
    private int _requestIdCounter = 0;
    
    public LspClient(Stream stdin)
    {
        _stdin = stdin;
    }
    
    public async Task RequestOutgoingCallsAsync(Guid requestId, string uri, Position position)
    {
        var request = new LspRequest
        {
            JsonRpc = "2.0",
            Id = requestId,
            Method = "callHierarchy/outgoingCalls",
            Params = new()
            {
                Item = new()
                {
                    Uri = uri,
                    Range = new() { Start = position, End = position },
                    SelectionRange = new() { Start = position, End = position }
                }
            }
        };
        
        await SendRequest(request);
    }
    
    public async Task RequestDefinitionAsync(Guid requestId, string uri, Position position)
    {
        var request = new LspRequest
        {
            JsonRpc = "2.0",
            Id = requestId,
            Method = "textDocument/definition",
            Params = new LspRequestParams
            {
                TextDocument = new TextDocument { Uri =uri },
                Position = position
            }
        };
        
        await SendRequest(request);
    }
    
    private async Task SendRequest(LspRequest request)
    {
        var json = JsonSerializer.Serialize(request, ACCJsonContext.Default.LspRequest);
        var content = Encoding.UTF8.GetBytes(json);
        var header = $"Content-Length: {content.Length}\r\n\r\n";
        var headerBytes = Encoding.UTF8.GetBytes(header);
        
        await _stdin.WriteAsync(headerBytes);
        await _stdin.WriteAsync(content);
        await _stdin.FlushAsync();
    }
}

public record LspRequest
{
      public required string JsonRpc {get;init;} 
      public required Guid Id {get;init;}
      public required string Method {get;init;}
      public required LspRequestParams Params {get;init;}
}

public record LspRequestParams
{
    public TextDocument? TextDocument {get;init;}
    public Position? Position {get;init;}
    public LspItem? Item {get;init;}
}


public record TextDocument
{
    public required string Uri {get;init;}
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
    public required string Uri {get;init;}
    public required LspRange Range {get;init;}
    public required LspRange SelectionRange {get;init;}
}

public record LspRange
{
    public required Position Start {get;set;}
    public required Position End {get;set;}
}
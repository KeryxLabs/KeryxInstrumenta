using System.Collections.Concurrent;


namespace AdaptiveCodecContextEngine.Models.Lsp;
public class LspReferenceTracker
{
    // Track pending reference requests
    private readonly ConcurrentDictionary<object, ReferenceRequest> _pendingRequests = new();
    
    public record ReferenceRequest
    {
        public string FromNodeId { get; init; } = null!;
        public string TargetSymbol { get; init; } = null!;
        public string FileUri { get; init; } = null!;
        public Position Position { get; init; } = null!;
        public RelationshipType ExpectedType { get; init; }
    }
    
    public enum RelationshipType
    {
        Calls,
        Inherits,
        Implements,
        Imports,
        References
    }
    
    public void TrackRequest(object requestId, ReferenceRequest request)
    {
        _pendingRequests[requestId] = request;
    }
    
    public ReferenceRequest? GetRequest(object requestId)
    {
        _pendingRequests.TryGetValue(requestId, out var request);
        return request;
    }
    
    public void CompleteRequest(object requestId)
    {
        _pendingRequests.TryRemove(requestId, out _);
    }
}

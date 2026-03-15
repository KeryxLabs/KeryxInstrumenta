
namespace AdaptiveCodecContextEngine.Models.Lsp;

public record DependencyEdge
{
    public string FromNodeId { get; init; } = null!;
    public string? ToSymbolName { get; init; }
    public string? ToFileUri { get; init; }
    public int? ToLine { get; init; }
    public string RelationshipType { get; init; } = null!;
    public string SourceFileUri { get; init; } = null!;
}

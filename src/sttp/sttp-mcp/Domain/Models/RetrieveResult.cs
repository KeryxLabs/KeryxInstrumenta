
namespace SttpMcp.Domain.Models;
public record RetrieveResult
{
    public required IReadOnlyList<SttpNode> Nodes { get; init; }
    public required int Retrieved { get; init; }
    public required PsiRange PsiRange { get; init; }
}
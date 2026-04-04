namespace SttpMcp.Domain.Models;

public sealed class ListNodesResult
{
    public List<SttpNode> Nodes { get; init; } = [];
    public int Retrieved { get; init; }
}

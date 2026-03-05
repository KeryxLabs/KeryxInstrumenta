
namespace SttpMcp.Domain.Models;
public record ParseResult
{
    public required bool Success { get; init; }
    public SttpNode? Node { get; init; }
    public string? Error { get; init; }

    public static ParseResult Ok(SttpNode node) => new()
    {
        Success = true,
        Node = node
    };

    public static ParseResult Fail(string error) => new()
    {
        Success = false,
        Error = error
    };
}
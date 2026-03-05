
namespace SttpMcp.Domain.Models;
public record StoreResult
{
    public required string NodeId { get; init; }
    public required float Psi { get; init; }
    public required bool Valid { get; init; }
    public string? ValidationError { get; init; }
}
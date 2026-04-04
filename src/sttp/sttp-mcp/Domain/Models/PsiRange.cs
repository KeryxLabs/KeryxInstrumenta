
namespace SttpMcp.Domain.Models;
public record PsiRange
{
    public required float Min { get; init; }
    public required float Max { get; init; }
    public required float Average { get; init; }
}
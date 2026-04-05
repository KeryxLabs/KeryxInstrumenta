namespace SttpMcp.Domain.Models;

public sealed record NumericRange
{
    public required float Min { get; init; }
    public required float Max { get; init; }
    public required float Average { get; init; }
}
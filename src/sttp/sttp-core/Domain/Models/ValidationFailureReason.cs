namespace SttpMcp.Domain.Models;

public enum ValidationFailureReason
{
    None,
    ParseFailure,
    CoherenceFailure,
    MissingLayer,
    InvalidTier,
    NestingDepth,
    SchemaViolation
}
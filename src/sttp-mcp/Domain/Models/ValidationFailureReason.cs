
namespace SttpMcp.Domain.Models;
public enum ValidationFailureReason
{
    None,
    ParseFailure,       // tree-sitter could not parse the structure
    CoherenceFailure,   // Ψ checksum mismatch
    MissingLayer,       // required layer absent
    InvalidTier,        // tier value not in enum
    NestingDepth,       // content exceeded 5 levels
    SchemaViolation     // field pattern violation
}
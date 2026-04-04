using SttpMcp.Domain.Models;

public interface INodeValidator
{
    /// <summary>
    /// Validate raw ⏣ text against the STTP schema.
    /// Returns a validation result with any structural errors.
    /// </summary>
    ValidationResult Validate(string rawNode);

    /// <summary>
    /// Verify the Ψ checksum of a parsed node.
    /// Ψ = Σ(V_a) across compression_avec dimensions.
    /// </summary>
    bool VerifyPsi(SttpNode node);
}

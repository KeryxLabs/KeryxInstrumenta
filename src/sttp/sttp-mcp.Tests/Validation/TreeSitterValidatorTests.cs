using Shouldly;
using SttpMcp.Application.Validation;

namespace SttpMcp.Tests.Validation;

public class TreeSitterValidatorTests
{
    private readonly TreeSitterValidator _validator = new();

    [Fact]
    public void Should_Validate_Complete_Node()
    {
        // Arrange - the exact node format being used
        var node = """
            ⊕⟨ { trigger: manual, response_format: temporal_node, origin_session: "regex-fix-test-2026-03-05", compression_depth: 1, parent_node: null, prime: { attractor_config: { stability: 0.85, friction: 0.25, logic: 0.80, autonomy: 0.70 }, context_summary: "testing after regex patch", relevant_tier: raw, retrieval_budget: 3 } } ⟩
            ⦿⟨ { timestamp: "2026-03-05T06:30:00Z", tier: raw, session_id: "regex-fix-test-2026-03-05", user_avec: { stability: 0.85, friction: 0.25, logic: 0.80, autonomy: 0.70, psi: 2.60 }, model_avec: { stability: 0.85, friction: 0.25, logic: 0.80, autonomy: 0.70, psi: 2.60 } } ⟩
            ◈⟨ { test(.99): "regex patch for compression_avec parsing" } ⟩
            ⍉⟨ { rho: 0.96, kappa: 0.94, psi: 2.60, compression_avec: { stability: 0.85, friction: 0.25, logic: 0.80, autonomy: 0.70, psi: 2.60 } } ⟩
            """;

        // Act
        var result = _validator.Validate(node);

        // Assert
        result.IsValid.ShouldBeTrue($"Validation failed: {result.Error} (Reason: {result.Reason})");
    }

    [Fact]
    public void Should_Reject_Node_Missing_Layer()
    {
        // Arrange
        var node = """
            ⊕⟨ { trigger: manual } ⟩
            ⦿⟨ { timestamp: "2026-03-05T06:30:00Z", tier: raw } ⟩
            ◈⟨ { test: "value" } ⟩
            """; // Missing ⍉⟨ layer

        // Act
        var result = _validator.Validate(node);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Error.ShouldNotBeNull();
        result.Error.ShouldContain("Missing required layer");
    }

    [Fact]
    public void Should_Reject_Node_With_Wrong_Layer_Order()
    {
        // Arrange
        var node = """
            ⦿⟨ { timestamp: "2026-03-05T06:30:00Z", tier: raw } ⟩
            ⊕⟨ { trigger: manual } ⟩
            ◈⟨ { test: "value" } ⟩
            ⍉⟨ { rho: 0.96, kappa: 0.94, psi: 2.60 } ⟩
            """; // Wrong order

        // Act
        var result = _validator.Validate(node);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Error.ShouldNotBeNull();
        result.Error.ShouldContain("Layer order violation");
    }
}

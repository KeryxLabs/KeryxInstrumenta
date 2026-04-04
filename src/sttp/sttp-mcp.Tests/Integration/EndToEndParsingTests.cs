using Shouldly;
using SttpMcp.Application.Validation;
using SttpMcp.Parsing;
using SttpMcp.Domain.Models;

namespace SttpMcp.Tests.Integration;

public class EndToEndParsingTests
{
    private readonly SttpNodeParser _parser = new();
    private readonly TreeSitterValidator _validator = new();

    [Fact]
    public void Should_Parse_And_Validate_Complete_Workflow()
    {
        // Arrange - the exact node format used in the MCP call
        var nodeText = """
            ⊕⟨ { trigger: manual, response_format: temporal_node, origin_session: "regex-fix-test-2026-03-05", compression_depth: 1, parent_node: null, prime: { attractor_config: { stability: 0.85, friction: 0.25, logic: 0.80, autonomy: 0.70 }, context_summary: "testing after regex patch", relevant_tier: raw, retrieval_budget: 3 } } ⟩
            ⦿⟨ { timestamp: "2026-03-05T06:30:00Z", tier: raw, session_id: "regex-fix-test-2026-03-05", user_avec: { stability: 0.85, friction: 0.25, logic: 0.80, autonomy: 0.70, psi: 2.60 }, model_avec: { stability: 0.85, friction: 0.25, logic: 0.80, autonomy: 0.70, psi: 2.60 } } ⟩
            ◈⟨ { test(.99): "regex patch for compression_avec parsing" } ⟩
            ⍉⟨ { rho: 0.96, kappa: 0.94, psi: 2.60, compression_avec: { stability: 0.85, friction: 0.25, logic: 0.80, autonomy: 0.70, psi: 2.60 } } ⟩
            """;

        // Act - Step 1: Validate
        var validationResult = _validator.Validate(nodeText);
        
        // Assert validation passes
        validationResult.IsValid.ShouldBeTrue($"Validation failed: {validationResult.Error}");

        // Act - Step 2: Parse
        var parseResult = _parser.TryParse(nodeText, "regex-fix-test-2026-03-05");
        
        // Assert parse succeeds
        parseResult.Success.ShouldBeTrue($"Parse failed: {parseResult.Error}");
        parseResult.Node.ShouldNotBeNull();

        var node = parseResult.Node!;

        // Assert - All AVEC blocks should be non-zero
        node.UserAvec.Psi.ShouldBeGreaterThan(0, "user_avec psi should be > 0");
        node.ModelAvec.Psi.ShouldBeGreaterThan(0, "model_avec psi should be > 0");
        node.CompressionAvec.ShouldNotBeNull();
        node.CompressionAvec.Psi.ShouldBeGreaterThan(0, "compression_avec psi should be > 0");

        // Assert - Specific values match
        node.UserAvec.Stability.ShouldBe(0.85f);
        node.ModelAvec.Stability.ShouldBe(0.85f);
        node.CompressionAvec.Stability.ShouldBe(0.85f);
        
        // Assert - Can create dictionaries (what gets passed to SurrealDB)
        var userAvecDict = CreateAvecDict(node.UserAvec);
        var modelAvecDict = CreateAvecDict(node.ModelAvec);
        var compressionAvecDict = CreateAvecDict(node.CompressionAvec);

        userAvecDict.ShouldNotBeNull();
        userAvecDict.ShouldNotBeEmpty();
        modelAvecDict.ShouldNotBeNull();
        modelAvecDict.ShouldNotBeEmpty();
        compressionAvecDict.ShouldNotBeNull();
        compressionAvecDict.ShouldNotBeEmpty();

        // This is what would be passed to SurrealDB - verify it's not null
        compressionAvecDict["stability"].ShouldBe(0.85f);
        compressionAvecDict["friction"].ShouldBe(0.25f);
        compressionAvecDict["logic"].ShouldBe(0.80f);
        compressionAvecDict["autonomy"].ShouldBe(0.70f);
        compressionAvecDict["psi"].ShouldBeGreaterThan(0);
    }

    private Dictionary<string, float> CreateAvecDict(AvecState avec) => new()
    {
        ["stability"] = avec.Stability,
        ["friction"] = avec.Friction,
        ["logic"] = avec.Logic,
        ["autonomy"] = avec.Autonomy,
        ["psi"] = avec.Psi
    };
}

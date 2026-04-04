using Shouldly;
using SttpMcp.Parsing;

namespace SttpMcp.Tests.Parsing;

public class SttpNodeParserTests
{
    private readonly SttpNodeParser _parser = new();

    [Fact]
    public void Should_Parse_Valid_Node_With_All_Avec_Blocks()
    {
        // Arrange
        var node = """
            ⊕⟨ { trigger: manual, response_format: temporal_node, origin_session: "test-session", compression_depth: 1, parent_node: null, prime: { attractor_config: { stability: 0.85, friction: 0.25, logic: 0.80, autonomy: 0.70 }, context_summary: "test node", relevant_tier: raw, retrieval_budget: 3 } } ⟩
            ⦿⟨ { timestamp: "2026-03-05T06:30:00Z", tier: raw, session_id: "test-session", user_avec: { stability: 0.85, friction: 0.25, logic: 0.80, autonomy: 0.70, psi: 2.60 }, model_avec: { stability: 0.85, friction: 0.25, logic: 0.80, autonomy: 0.70, psi: 2.60 } } ⟩
            ◈⟨ { test(.99): "unit test" } ⟩
            ⍉⟨ { rho: 0.96, kappa: 0.94, psi: 2.60, compression_avec: { stability: 0.85, friction: 0.25, logic: 0.80, autonomy: 0.70, psi: 2.60 } } ⟩
            """;

        // Act
        var result = _parser.TryParse(node, "test-session");

        // Assert
        result.Success.ShouldBeTrue($"Parse failed: {result.Error}");
        result.Node.ShouldNotBeNull();
        
        var parsed = result.Node!;
        
        // Check user_avec
        parsed.UserAvec.Stability.ShouldBe(0.85f);
        parsed.UserAvec.Friction.ShouldBe(0.25f);
        parsed.UserAvec.Logic.ShouldBe(0.80f);
        parsed.UserAvec.Autonomy.ShouldBe(0.70f);
        parsed.UserAvec.Psi.ShouldBe(2.60f, 0.01f);
        
        // Check model_avec
        parsed.ModelAvec.Stability.ShouldBe(0.85f);
        parsed.ModelAvec.Friction.ShouldBe(0.25f);
        parsed.ModelAvec.Logic.ShouldBe(0.80f);
        parsed.ModelAvec.Autonomy.ShouldBe(0.70f);
        parsed.ModelAvec.Psi.ShouldBe(2.60f, 0.01f);
        
        // Check compression_avec
        parsed.CompressionAvec.Stability.ShouldBe(0.85f);
        parsed.CompressionAvec.Friction.ShouldBe(0.25f);
        parsed.CompressionAvec.Logic.ShouldBe(0.80f);
        parsed.CompressionAvec.Autonomy.ShouldBe(0.70f);
        parsed.CompressionAvec.Psi.ShouldBe(2.60f, 0.01f);
        
        // Check other fields
        parsed.Tier.ShouldBe("raw");
        parsed.CompressionDepth.ShouldBe(1);
        parsed.Rho.ShouldBe(0.96f);
        parsed.Kappa.ShouldBe(0.94f);
        parsed.Psi.ShouldBe(2.60f, 0.01f);
    }

    [Fact]
    public void Should_Parse_User_Avec_Block()
    {
        // Arrange
        var avecBlock = "user_avec: { stability: 0.85, friction: 0.25, logic: 0.80, autonomy: 0.70, psi: 2.60 }";

        // Act
        var result = _parser.TryParse(
            $"⊕⟨ {{ trigger: manual, response_format: temporal_node, origin_session: \"test\", compression_depth: 1, parent_node: null }} ⟩\n" +
            $"⦿⟨ {{ timestamp: \"2026-03-05T00:00:00Z\", tier: raw, session_id: \"test\", {avecBlock}, model_avec: {{ stability: 0.85, friction: 0.25, logic: 0.80, autonomy: 0.70 }} }} ⟩\n" +
            $"◈⟨ {{ test: \"value\" }} ⟩\n" +
            $"⍉⟨ {{ rho: 0.96, kappa: 0.94, psi: 2.60, compression_avec: {{ stability: 0.85, friction: 0.25, logic: 0.80, autonomy: 0.70 }} }} ⟩",
            "test"
        );

        // Assert
        result.Success.ShouldBeTrue($"Parse failed: {result.Error}");
        result.Node!.UserAvec.Psi.ShouldBeGreaterThan(0);
    }
    
    [Fact]
    public void Should_Parse_Model_Avec_Block()
    {
        // Arrange
        var avecBlock = "model_avec: { stability: 0.86, friction: 0.24, logic: 0.93, autonomy: 0.84, psi: 2.87 }";

        // Act
        var result = _parser.TryParse(
            $"⊕⟨ {{ trigger: manual, response_format: temporal_node, origin_session: \"test\", compression_depth: 1, parent_node: null }} ⟩\n" +
            $"⦿⟨ {{ timestamp: \"2026-03-05T00:00:00Z\", tier: raw, session_id: \"test\", user_avec: {{ stability: 0.85, friction: 0.25, logic: 0.80, autonomy: 0.70 }}, {avecBlock} }} ⟩\n" +
            $"◈⟨ {{ test: \"value\" }} ⟩\n" +
            $"⍉⟨ {{ rho: 0.96, kappa: 0.94, psi: 2.60, compression_avec: {{ stability: 0.85, friction: 0.25, logic: 0.80, autonomy: 0.70 }} }} ⟩",
            "test"
        );

        // Assert
        result.Success.ShouldBeTrue($"Parse failed: {result.Error}");
        result.Node!.ModelAvec.Stability.ShouldBe(0.86f);
        result.Node!.ModelAvec.Psi.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void Should_Parse_Compression_Avec_Block()
    {
        // Arrange
        var avecBlock = "compression_avec: { stability: 0.86, friction: 0.24, logic: 0.93, autonomy: 0.84, psi: 2.87 }";

        // Act
        var result = _parser.TryParse(
            $"⊕⟨ {{ trigger: manual, response_format: temporal_node, origin_session: \"test\", compression_depth: 1, parent_node: null }} ⟩\n" +
            $"⦿⟨ {{ timestamp: \"2026-03-05T00:00:00Z\", tier: raw, session_id: \"test\", user_avec: {{ stability: 0.85, friction: 0.25, logic: 0.80, autonomy: 0.70 }}, model_avec: {{ stability: 0.85, friction: 0.25, logic: 0.80, autonomy: 0.70 }} }} ⟩\n" +
            $"◈⟨ {{ test: \"value\" }} ⟩\n" +
            $"⍉⟨ {{ rho: 0.96, kappa: 0.94, psi: 2.87, {avecBlock} }} ⟩",
            "test"
        );

        // Assert
        result.Success.ShouldBeTrue($"Parse failed: {result.Error}");
        result.Node!.CompressionAvec.Stability.ShouldBe(0.86f);
        result.Node!.CompressionAvec.Friction.ShouldBe(0.24f);
        result.Node!.CompressionAvec.Logic.ShouldBe(0.93f);
        result.Node!.CompressionAvec.Autonomy.ShouldBe(0.84f);
        result.Node!.CompressionAvec.Psi.ShouldBeGreaterThan(0);
    }
}

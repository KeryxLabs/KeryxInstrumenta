using SttpMcp.Domain.Contracts;
using SttpMcp.Domain.Models;
using System.ComponentModel;
using ModelContextProtocol.Server;
using SttpMcp.Application.Tools;


public sealed class TreeSitterValidator : INodeValidator
{
    // Layer markers — structural presence check
    private static readonly string[] RequiredLayers = ["⊕⟨", "⦿⟨", "◈⟨", "⍉⟨"];
    private static readonly string[] ValidTiers =
        ["raw", "daily", "weekly", "monthly", "quarterly", "yearly"];

    public ValidationResult Validate(string rawNode)
    {
        if (string.IsNullOrWhiteSpace(rawNode))
            return ValidationResult.Fail("Node is empty", ValidationFailureReason.ParseFailure);

        // Phase 1 — structural presence (pre tree-sitter, fast path)
        foreach (var layer in RequiredLayers)
        {
            if (!rawNode.Contains(layer))
                return ValidationResult.Fail(
                    $"Missing required layer: {layer}",
                    ValidationFailureReason.MissingLayer);
        }

        // Phase 2 — layer order law: ⊕ → ⦿ → ◈ → ⍉
        var provIdx = rawNode.IndexOf("⊕⟨", StringComparison.Ordinal);
        var envIdx  = rawNode.IndexOf("⦿⟨", StringComparison.Ordinal);
        var conIdx  = rawNode.IndexOf("◈⟨", StringComparison.Ordinal);
        var metIdx  = rawNode.IndexOf("⍉⟨", StringComparison.Ordinal);

        if (!(provIdx < envIdx && envIdx < conIdx && conIdx < metIdx))
            return ValidationResult.Fail(
                "Layer order violation — required: ⊕ → ⦿ → ◈ → ⍉",
                ValidationFailureReason.SchemaViolation);

        // Phase 3 — tier enum validation
        var tierValid = ValidTiers.Any(t =>
            rawNode.Contains($"tier: {t}", StringComparison.OrdinalIgnoreCase));

        if (!tierValid)
            return ValidationResult.Fail(
                $"Invalid tier — expected one of: {string.Join("|", ValidTiers)}",
                ValidationFailureReason.InvalidTier);

        // Phase 4 — nesting depth (count brace depth in ◈ block)
        var contentBlock = ExtractContentBlock(rawNode);
        if (contentBlock is not null)
        {
            var depth = MaxNestingDepth(contentBlock);
            if (depth > 5)
                return ValidationResult.Fail(
                    $"Content nesting depth {depth} exceeds maximum of 5",
                    ValidationFailureReason.NestingDepth);
        }

        // TODO: Phase 5 — full tree-sitter grammar parse
        // grammar.js compiled and integrated here
        // current phases above are the fast pre-validation path
        // tree-sitter becomes the authoritative structural gate

        return ValidationResult.Ok();
    }

    public bool VerifyPsi(SttpNode node)
    {
        // Ψ = Σ(V_a) across compression_avec dimensions
        var computed = node.CompressionAvec.Psi;
        var stored = node.Psi;
        // allow small float tolerance
        return Math.Abs(computed - stored) < 0.01f;
    }

    private static string? ExtractContentBlock(string raw)
    {
        var start = raw.IndexOf("◈⟨", StringComparison.Ordinal);
        var end   = raw.IndexOf("⍉⟨", StringComparison.Ordinal);
        if (start < 0 || end < 0 || end <= start) return null;
        return raw[start..end];
    }

    private static int MaxNestingDepth(string block)
    {
        var max = 0;
        var depth = 0;
        foreach (var c in block)
        {
            if (c == '{') { depth++; max = Math.Max(max, depth); }
            else if (c == '}') depth--;
        }
        return max;
    }
}
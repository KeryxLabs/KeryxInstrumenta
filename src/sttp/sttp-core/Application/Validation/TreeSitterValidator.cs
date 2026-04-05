using SttpMcp.Domain.Contracts;
using SttpMcp.Domain.Models;

namespace SttpMcp.Application.Validation;

public sealed class TreeSitterValidator : INodeValidator
{
    private static readonly string[] RequiredLayers = ["⊕⟨", "⦿⟨", "◈⟨", "⍉⟨"];
    private static readonly string[] ValidTiers = ["raw", "daily", "weekly", "monthly", "quarterly", "yearly"];

    public ValidationResult Validate(string rawNode)
    {
        if (string.IsNullOrWhiteSpace(rawNode))
            return ValidationResult.Fail("Node is empty", ValidationFailureReason.ParseFailure);

        foreach (var layer in RequiredLayers)
        {
            if (!rawNode.Contains(layer))
                return ValidationResult.Fail($"Missing required layer: {layer}", ValidationFailureReason.MissingLayer);
        }

        var provIdx = rawNode.IndexOf("⊕⟨", StringComparison.Ordinal);
        var envIdx = rawNode.IndexOf("⦿⟨", StringComparison.Ordinal);
        var conIdx = rawNode.IndexOf("◈⟨", StringComparison.Ordinal);
        var metIdx = rawNode.IndexOf("⍉⟨", StringComparison.Ordinal);

        if (!(provIdx < envIdx && envIdx < conIdx && conIdx < metIdx))
            return ValidationResult.Fail(
                "Layer order violation — required: ⊕ → ⦿ → ◈ → ⍉",
                ValidationFailureReason.SchemaViolation);

        var tierValid = ValidTiers.Any(t => rawNode.Contains($"tier: {t}", StringComparison.OrdinalIgnoreCase));
        if (!tierValid)
            return ValidationResult.Fail(
                $"Invalid tier — expected one of: {string.Join("|", ValidTiers)}",
                ValidationFailureReason.InvalidTier);

        var contentBlock = ExtractContentBlock(rawNode);
        if (contentBlock is not null)
        {
            var depth = MaxNestingDepth(contentBlock);
            if (depth > 5)
                return ValidationResult.Fail(
                    $"Content nesting depth {depth} exceeds maximum of 5",
                    ValidationFailureReason.NestingDepth);
        }

        return ValidationResult.Ok();
    }

    public bool VerifyPsi(SttpNode node)
    {
        var computed = node.CompressionAvec?.Psi ?? 0.0f;
        return Math.Abs(computed - node.Psi) < 0.01f;
    }

    private static string? ExtractContentBlock(string raw)
    {
        var start = raw.IndexOf("◈⟨", StringComparison.Ordinal);
        var end = raw.IndexOf("⍉⟨", StringComparison.Ordinal);
        if (start < 0 || end < 0 || end <= start) return null;
        return raw[start..end];
    }

    private static int MaxNestingDepth(string block)
    {
        var max = 0;
        var depth = 0;
        foreach (var c in block)
        {
            if (c == '{')
            {
                depth++;
                max = Math.Max(max, depth);
            }
            else if (c == '}')
            {
                depth--;
            }
        }

        return max;
    }
}
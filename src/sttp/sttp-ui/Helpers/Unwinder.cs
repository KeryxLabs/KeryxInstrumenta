using System.Text.RegularExpressions;
using sttp_ui.Models;

namespace sttp_ui.Helpers;

public sealed record UnwindResult(
    string StatusIcon,
    string StatusLabel,
    string StatusClass,
    string Summary,
    string Interpretation,
    string NextAction);

public static class Unwinder
{
    public static UnwindResult Unwind(SttpNodeDto node)
    {
        var avec = node.UserAvec;

        // score = (logic + stability + autonomy) / 3 - friction
        var score = (avec.Logic + avec.Stability + avec.Autonomy) / 3f - avec.Friction;

        var (icon, label, cls) = score switch
        {
            >= 0.75f => ("🟢", "Great progress", "status-great"),
            >= 0.50f => ("🟡", "Good progress",  "status-good"),
            >= 0.25f => ("🟠", "Some friction",  "status-friction"),
            _        => ("🔴", "You got stuck",  "status-stuck")
        };

        return new UnwindResult(
            icon, label, cls,
            ExtractSummary(node),
            Interpret(avec.Friction, avec.Logic),
            NextAction(avec.Friction, avec.Logic, avec.Autonomy));
    }

    // ── private helpers ───────────────────────────────────────────────────────

    private static string ExtractSummary(SttpNodeDto node)
    {
        if (!string.IsNullOrWhiteSpace(node.Raw))
        {
            var m = Regex.Match(node.Raw, @"context_summary\([^)]*\):\s*""([^""]+)""");
            if (m.Success)
                return ToSentence(m.Groups[1].Value);
        }
        return ToSentence(node.SessionId);
    }

    private static string ToSentence(string raw)
    {
        // strip trailing date: -2026-04-05 or _2026_04_05
        var cleaned = Regex.Replace(raw, @"[-_]\d{4}[-_]\d{2}[-_]\d{2}$", "");
        cleaned = cleaned.Replace("-", " ").Replace("_", " ").Trim();
        if (string.IsNullOrWhiteSpace(cleaned)) return "a session.";
        return $"You worked on {cleaned.ToLower()}.";
    }

    private static string Interpret(float friction, float logic)
    {
        bool highF = friction > 0.5f;
        bool highL = logic > 0.85f;
        bool medL  = logic >= 0.6f;

        return (highF, highL, medL) switch
        {
            (false, true,  _)    => "You understood things clearly and it felt smooth.",
            (false, false, true) => "Things went smoothly, but some parts are still forming.",
            (true,  true,  _)    => "You figured it out, but it took effort.",
            _                    => "This was confusing and frustrating."
        };
    }

    private static string NextAction(float friction, float logic, float autonomy)
    {
        if (logic > 0.85f && friction < 0.2f)
            return "Keep going — you're ready to build or expand this.";
        if (logic >= 0.6f && friction < 0.5f)
            return "Keep practicing — try a small improvement or variation.";
        if (friction > 0.5f)
            return "Slow down — break this into smaller steps or ask for help.";
        return "Focus on understanding — revisit the basics or simplify.";
    }
}

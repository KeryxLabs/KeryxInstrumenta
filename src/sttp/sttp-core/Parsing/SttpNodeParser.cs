using System.Text.RegularExpressions;
using SttpMcp.Domain.Models;

namespace SttpMcp.Parsing;

public sealed class SttpNodeParser
{
    private static readonly Regex TimestampRx = new(
        @"timestamp:\s*""(?<v>[^""]+)""", RegexOptions.Compiled);
    private static readonly Regex TierRx = new(
        @"tier:\s*(?<v>raw|daily|weekly|monthly|quarterly|yearly)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex SessionIdRx = new(
        @"session_id:\s*""(?<v>[^""]+)""", RegexOptions.Compiled);
    private static readonly Regex CompressionDepthRx = new(
        @"compression_depth:\s*(?<v>\d+)", RegexOptions.Compiled);
    private static readonly Regex ParentNodeRx = new(
        @"parent_node:\s*(?:ref:(?<ref>[^,\s}\]]+)|""(?<quoted>[^""]+)""|(?<null>null))",
        RegexOptions.Compiled);

    private static readonly Regex AvecRx = new(
        @"(?<name>user_avec|model_avec|compression_avec)\s*:\s*\{" +
        @"\s*stability\s*:\s*(?<stability>[\d.]+)" +
        @",\s*friction\s*:\s*(?<friction>[\d.]+)" +
        @",\s*logic\s*:\s*(?<logic>[\d.]+)" +
        @",\s*autonomy\s*:\s*(?<autonomy>[\d.]+)" +
        @"(?:,\s*psi\s*:\s*(?<psi>[\d.]+))?" +
        @"\s*\}",
        RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex RhoRx = new(@"rho:\s*(?<v>[\d.]+)", RegexOptions.Compiled);
    private static readonly Regex KappaRx = new(@"kappa:\s*(?<v>[\d.]+)", RegexOptions.Compiled);
    private static readonly Regex PsiRx = new(
        @"⍉⟨.*?psi:\s*(?<v>[\d.]+)",
        RegexOptions.Compiled | RegexOptions.Singleline);

    public ParseResult TryParse(string raw, string sessionId)
    {
        try
        {
            var metricsBlock = ExtractMetricsBlock(raw);

            var avecMatches = AvecRx.Matches(raw);
            var avecMap = avecMatches
                .Cast<Match>()
                .ToDictionary(
                    m => m.Groups["name"].Value,
                    m => ParseAvec(m));

            var compressionAvecMatch = AvecRx.Match(metricsBlock);
            if (compressionAvecMatch.Success &&
                compressionAvecMatch.Groups["name"].Value == "compression_avec")
            {
                avecMap["compression_avec"] = ParseAvec(compressionAvecMatch);
            }

            var node = new SttpNode
            {
                Raw = raw,
                SessionId = sessionId,
                Tier = TierRx.Match(raw).Groups["v"].Value,
                Timestamp = ParseTimestamp(raw),
                CompressionDepth = ParseInt(CompressionDepthRx, raw),
                ParentNodeId = ParseParentNode(raw),
                SyncKey = string.Empty,
                UpdatedAt = DateTime.UtcNow,
                SourceMetadata = null,
                UserAvec = avecMap.GetValueOrDefault("user_avec") ?? AvecState.Zero,
                ModelAvec = avecMap.GetValueOrDefault("model_avec") ?? AvecState.Zero,
                CompressionAvec = avecMap.GetValueOrDefault("compression_avec") ?? AvecState.Zero,
                Rho = ParseFloat(RhoRx, metricsBlock),
                Kappa = ParseFloat(KappaRx, metricsBlock),
                Psi = ParseFloat(PsiRx, metricsBlock)
            };

            return ParseResult.Ok(node);
        }
        catch (Exception ex)
        {
            return ParseResult.Fail($"Parse error: {ex.Message}");
        }
    }

    private static AvecState ParseAvec(Match m) => new()
    {
        Stability = ParseGroupFloat(m, "stability"),
        Friction = ParseGroupFloat(m, "friction"),
        Logic = ParseGroupFloat(m, "logic"),
        Autonomy = ParseGroupFloat(m, "autonomy")
    };

    private static DateTime ParseTimestamp(string raw)
    {
        var m = TimestampRx.Match(raw);
        return m.Success && DateTime.TryParse(
            m.Groups["v"].Value,
            null,
            System.Globalization.DateTimeStyles.RoundtripKind,
            out var dt)
            ? dt
            : DateTime.UtcNow;
    }

    private static string? ParseParentNode(string raw)
    {
        var m = ParentNodeRx.Match(raw);
        if (!m.Success) return null;
        if (m.Groups["null"].Success) return null;
        if (m.Groups["ref"].Success) return m.Groups["ref"].Value;
        if (m.Groups["quoted"].Success) return m.Groups["quoted"].Value;
        return null;
    }

    private static int ParseInt(Regex rx, string raw)
    {
        var m = rx.Match(raw);
        return m.Success && int.TryParse(m.Groups["v"].Value, out var v) ? v : 0;
    }

    private static float ParseFloat(Regex rx, string raw)
    {
        var m = rx.Match(raw);
        return m.Success && float.TryParse(
            m.Groups["v"].Value,
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture,
            out var v) ? v : 0f;
    }

    private static float ParseGroupFloat(Match m, string group)
    {
        var g = m.Groups[group];
        return g.Success && float.TryParse(
            g.Value,
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture,
            out var v) ? v : 0f;
    }

    private static string ExtractMetricsBlock(string raw)
    {
        var idx = raw.IndexOf("⍉⟨", StringComparison.Ordinal);
        return idx < 0 ? string.Empty : raw[idx..];
    }
}
using System.ComponentModel;
using ModelContextProtocol.Server;
using SttpMcp.Application.Services;
using SttpMcp.Domain.Models;

namespace SttpMcp.Application.Tools;

public sealed class CreateMonthlyRollupTool(MonthlyRollupService service)
{
    [McpServerTool(Name = "create_monthly_rollup"), Description("""
        Aggregate stored STTP nodes over a date range into a new monthly rollup node.

        Use this when you want a compact month-level checkpoint instead of dozens of raw nodes.
        The rollup computes average user/model/compression AVEC states, confidence ranges,
        and low/medium/high confidence bands, then stores a new monthly STTP node.

        By default the first source node in the requested timeline becomes the parent anchor.
        """)]
    public Task<MonthlyRollupResult> CreateAsync(
        [Description("Session identifier for the new monthly rollup node.")]
        string sessionId,
        [Description("Inclusive UTC start timestamp for the source window (ISO8601).")]
        string startDateUtc,
        [Description("Inclusive UTC end timestamp for the source window (ISO8601).")]
        string endDateUtc,
        [Description("Optional source session filter. Omit to aggregate across all sessions in the range.")]
        string? sourceSessionId = null,
        [Description("Optional explicit parent node reference. Omit to use the first source node session ID.")]
        string? parentNodeId = null,
        [Description("Persist the generated monthly node. Default true.")]
        bool persist = true,
        CancellationToken ct = default)
    {
        if (!DateTime.TryParse(startDateUtc, null, System.Globalization.DateTimeStyles.RoundtripKind, out var start))
            return Task.FromResult(new MonthlyRollupResult { Error = $"InvalidDate: '{startDateUtc}' is not a valid ISO 8601 datetime." });

        if (!DateTime.TryParse(endDateUtc, null, System.Globalization.DateTimeStyles.RoundtripKind, out var end))
            return Task.FromResult(new MonthlyRollupResult { Error = $"InvalidDate: '{endDateUtc}' is not a valid ISO 8601 datetime." });

        return service.CreateAsync(
            new MonthlyRollupRequest
            {
                SessionId = sessionId,
                StartUtc = start,
                EndUtc = end,
                SourceSessionId = sourceSessionId,
                ParentNodeId = parentNodeId,
                Persist = persist
            },
            ct);
    }
}
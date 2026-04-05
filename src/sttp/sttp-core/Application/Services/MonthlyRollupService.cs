using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;
using SttpMcp.Domain.Contracts;
using SttpMcp.Domain.Models;
using SttpMcp.Parsing;

namespace SttpMcp.Application.Services;

public sealed class MonthlyRollupService(INodeStore store, INodeValidator validator, ILogger<MonthlyRollupService> logger)
{
    private readonly SttpNodeParser _parser = new();

    public async Task<MonthlyRollupResult> CreateAsync(MonthlyRollupRequest request, CancellationToken ct = default)
    {
        if (request.EndUtc < request.StartUtc)
        {
            return new MonthlyRollupResult
            {
                Error = "InvalidRange: end must be greater than or equal to start."
            };
        }

        var nodes = await store.QueryNodesAsync(
            new NodeQuery
            {
                SessionId = request.SourceSessionId,
                FromUtc = request.StartUtc,
                ToUtc = request.EndUtc,
                Limit = request.Limit
            },
            ct);

        if (nodes.Count == 0)
        {
            return new MonthlyRollupResult
            {
                Error = "NoSourceNodes: no nodes found in the requested range."
            };
        }

        var orderedNodes = nodes.OrderBy(n => n.Timestamp).ToList();
        var userNodes = orderedNodes.Where(n => n.UserAvec.Psi > 0).ToList();
        var modelNodes = orderedNodes.Where(n => n.ModelAvec.Psi > 0).ToList();
        var compressionNodes = orderedNodes.Where(n => (n.CompressionAvec?.Psi ?? 0f) > 0).ToList();

        var userAverage = AverageAvec(userNodes.Select(n => n.UserAvec));
        var modelAverage = AverageAvec(modelNodes.Select(n => n.ModelAvec));
        var compressionAverage = AverageAvec(compressionNodes.Select(n => n.CompressionAvec!));
        var rhoRange = RangeFor(orderedNodes.Select(n => n.Rho));
        var kappaRange = RangeFor(orderedNodes.Select(n => n.Kappa));
        var psiRange = RangeFor(orderedNodes.Select(n => n.Psi));
        var rhoBands = BandsFor(orderedNodes.Select(n => n.Rho));
        var kappaBands = BandsFor(orderedNodes.Select(n => n.Kappa));
        var activeDays = orderedNodes.Select(n => n.Timestamp.Date).Distinct().Count();
        var parentReference = request.ParentNodeId ?? orderedNodes[0].SessionId;

        var rawNode = BuildMonthlyNode(
            request,
            parentReference,
            orderedNodes.Count,
            userNodes.Count,
            activeDays,
            userAverage,
            modelAverage,
            compressionAverage,
            rhoRange,
            kappaRange,
            psiRange,
            rhoBands,
            kappaBands);

        var validation = validator.Validate(rawNode);
        if (!validation.IsValid)
        {
            return new MonthlyRollupResult
            {
                RawNode = rawNode,
                SourceNodes = orderedNodes.Count,
                ParentReference = parentReference,
                Error = $"ValidationFailure: {validation.Reason}: {validation.Error}"
            };
        }

        var parseResult = _parser.TryParse(rawNode, request.SessionId);
        if (!parseResult.Success)
        {
            return new MonthlyRollupResult
            {
                RawNode = rawNode,
                SourceNodes = orderedNodes.Count,
                ParentReference = parentReference,
                Error = $"ParseFailure: {parseResult.Error}"
            };
        }

        var nodeId = string.Empty;
        if (request.Persist)
        {
            try
            {
                nodeId = await store.StoreAsync(parseResult.Node!, ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Monthly rollup storage failed for {SessionId}", request.SessionId);
                return new MonthlyRollupResult
                {
                    RawNode = rawNode,
                    SourceNodes = orderedNodes.Count,
                    ParentReference = parentReference,
                    UserAverage = userAverage,
                    ModelAverage = modelAverage,
                    CompressionAverage = compressionAverage,
                    RhoRange = rhoRange,
                    KappaRange = kappaRange,
                    PsiRange = psiRange,
                    RhoBands = rhoBands,
                    KappaBands = kappaBands,
                    Error = $"StoreFailure: {ex.Message}"
                };
            }
        }

        return new MonthlyRollupResult
        {
            Success = true,
            NodeId = nodeId,
            RawNode = rawNode,
            SourceNodes = orderedNodes.Count,
            ParentReference = parentReference,
            UserAverage = userAverage,
            ModelAverage = modelAverage,
            CompressionAverage = compressionAverage,
            RhoRange = rhoRange,
            KappaRange = kappaRange,
            PsiRange = psiRange,
            RhoBands = rhoBands,
            KappaBands = kappaBands
        };
    }

    private static string BuildMonthlyNode(
        MonthlyRollupRequest request,
        string parentReference,
        int sourceNodes,
        int sourceUserAvecNodes,
        int activeDays,
        AvecState userAverage,
        AvecState modelAverage,
        AvecState compressionAverage,
        NumericRange rhoRange,
        NumericRange kappaRange,
        NumericRange psiRange,
        ConfidenceBandSummary rhoBands,
        ConfidenceBandSummary kappaBands)
    {
        var timestamp = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        var start = request.StartUtc.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var end = request.EndUtc.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var sourceSessionToken = string.IsNullOrWhiteSpace(request.SourceSessionId)
            ? "all_sessions"
            : Slug(request.SourceSessionId);

        return $$"""
⊕⟨ ⏣0{ trigger: manual, response_format: temporal_node, origin_session: "{{request.SessionId}}", compression_depth: 2, parent_node: ref:{{parentReference}}, prime: { attractor_config: { stability: {{F(userAverage.Stability)}}, friction: {{F(userAverage.Friction)}}, logic: {{F(userAverage.Logic)}}, autonomy: {{F(userAverage.Autonomy)}} }, context_summary: monthly_rollup_across_stored_sttp_nodes_with_average_state_and_confidence_spread, relevant_tier: monthly, retrieval_budget: 16 } } ⟩
⦿⟨ ⏣0{ timestamp: "{{timestamp}}", tier: monthly, session_id: "{{request.SessionId}}", schema_version: "sttp-1.0", user_avec: { stability: {{F(userAverage.Stability)}}, friction: {{F(userAverage.Friction)}}, logic: {{F(userAverage.Logic)}}, autonomy: {{F(userAverage.Autonomy)}}, psi: {{F(userAverage.Psi)}} }, model_avec: { stability: {{F(modelAverage.Stability)}}, friction: {{F(modelAverage.Friction)}}, logic: {{F(modelAverage.Logic)}}, autonomy: {{F(modelAverage.Autonomy)}}, psi: {{F(modelAverage.Psi)}} } } ⟩
◈⟨ ⏣0{ source_nodes(.99): {{sourceNodes}}, source_user_avec_nodes(.97): {{sourceUserAvecNodes}}, active_days(.95): {{activeDays}}, date_span(.99): {{start}}_to_{{end}}, source_session_filter(.78): {{sourceSessionToken}}, parent_anchor(.99): {{Slug(parentReference)}}, activity_shape(.83): burst_work_pattern_with_gaps_between_deep_sessions, monthly_arc(.86): stabilization_then_design_then_implementation_then_synthesis, behavioral_signature(.84): high_stability_high_logic_high_autonomy_with_low_to_moderate_friction, user_avec_average(.99): { stability: {{F(userAverage.Stability)}}, friction: {{F(userAverage.Friction)}}, logic: {{F(userAverage.Logic)}}, autonomy: {{F(userAverage.Autonomy)}}, psi: {{F(userAverage.Psi)}} }, model_avec_average(.97): { stability: {{F(modelAverage.Stability)}}, friction: {{F(modelAverage.Friction)}}, logic: {{F(modelAverage.Logic)}}, autonomy: {{F(modelAverage.Autonomy)}}, psi: {{F(modelAverage.Psi)}} }, compression_avec_average(.96): { stability: {{F(compressionAverage.Stability)}}, friction: {{F(compressionAverage.Friction)}}, logic: {{F(compressionAverage.Logic)}}, autonomy: {{F(compressionAverage.Autonomy)}}, psi: {{F(compressionAverage.Psi)}} }, confidence_ranges(.94): { rho_avg: {{F(rhoRange.Average)}}, rho_min: {{F(rhoRange.Min)}}, rho_max: {{F(rhoRange.Max)}}, kappa_avg: {{F(kappaRange.Average)}}, kappa_min: {{F(kappaRange.Min)}}, kappa_max: {{F(kappaRange.Max)}}, psi_avg: {{F(psiRange.Average)}}, psi_min: {{F(psiRange.Min)}}, psi_max: {{F(psiRange.Max)}} }, confidence_bands(.71): { rho_low: {{rhoBands.Low}}, rho_medium: {{rhoBands.Medium}}, rho_high: {{rhoBands.High}}, kappa_low: {{kappaBands.Low}}, kappa_medium: {{kappaBands.Medium}}, kappa_high: {{kappaBands.High}} }, uncertainty(.41): interpretive_fields_carry_lower_confidence_than_numeric_rollups } ⟩
⍉⟨ ⏣0{ rho: {{F(rhoRange.Average)}}, kappa: {{F(kappaRange.Average)}}, psi: {{F(psiRange.Average)}}, compression_avec: { stability: {{F(compressionAverage.Stability)}}, friction: {{F(compressionAverage.Friction)}}, logic: {{F(compressionAverage.Logic)}}, autonomy: {{F(compressionAverage.Autonomy)}}, psi: {{F(compressionAverage.Psi)}} } } ⟩
""";
    }

    private static AvecState AverageAvec(IEnumerable<AvecState> states)
    {
        var list = states.ToList();
        if (list.Count == 0)
            return AvecState.Zero;

        return new AvecState
        {
            Stability = list.Average(s => s.Stability),
            Friction = list.Average(s => s.Friction),
            Logic = list.Average(s => s.Logic),
            Autonomy = list.Average(s => s.Autonomy)
        };
    }

    private static NumericRange RangeFor(IEnumerable<float> values)
    {
        var list = values.ToList();
        if (list.Count == 0)
        {
            return new NumericRange
            {
                Min = 0,
                Max = 0,
                Average = 0
            };
        }

        return new NumericRange
        {
            Min = list.Min(),
            Max = list.Max(),
            Average = list.Average()
        };
    }

    private static ConfidenceBandSummary BandsFor(IEnumerable<float> values)
    {
        var list = values.ToList();
        return new ConfidenceBandSummary
        {
            Low = list.Count(v => v < 0.5f),
            Medium = list.Count(v => v >= 0.5f && v < 0.85f),
            High = list.Count(v => v >= 0.85f)
        };
    }

    private static string F(float value) => value.ToString("0.##########", CultureInfo.InvariantCulture);

    private static string Slug(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            builder.Append(char.IsLetterOrDigit(ch) ? char.ToLowerInvariant(ch) : '_');
        }

        return builder.ToString().Trim('_');
    }
}
using Microsoft.Extensions.Logging;
using SttpMcp.Domain.Contracts;
using SttpMcp.Domain.Models;

namespace SttpMcp.Application.Services;

public sealed class ContextQueryService(INodeStore store, ILogger<ContextQueryService> logger)
{
    public async Task<RetrieveResult> GetContextAsync(
        string sessionId,
        float stability,
        float friction,
        float logic,
        float autonomy,
        int limit = 5,
        CancellationToken ct = default)
    {
        try
        {
            var current = new AvecState
            {
                Stability = stability,
                Friction = friction,
                Logic = logic,
                Autonomy = autonomy,
            };

            var nodes = await store.GetByResonanceAsync(sessionId, current, limit, ct);

            if (nodes.Count == 0)
            {
                return new RetrieveResult
                {
                    Nodes = [],
                    Retrieved = 0,
                    PsiRange = new PsiRange
                    {
                        Min = 0,
                        Max = 0,
                        Average = 0,
                    },
                };
            }

            var psiValues = nodes.Select(n => n.Psi).ToList();

            return new RetrieveResult
            {
                Nodes = nodes,
                Retrieved = nodes.Count,
                PsiRange = new PsiRange
                {
                    Min = psiValues.Min(),
                    Max = psiValues.Max(),
                    Average = psiValues.Average(),
                },
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "GetContext failed");
            return new RetrieveResult
            {
                Nodes = [],
                Retrieved = 0,
                PsiRange = new PsiRange
                {
                    Min = 0,
                    Max = 0,
                    Average = 0,
                },
            };
        }
    }

    public async Task<ListNodesResult> ListNodesAsync(
        int limit = 50,
        string? sessionId = null,
        CancellationToken ct = default)
    {
        var cappedLimit = Math.Clamp(limit, 1, 200);
        var nodes = await store.ListNodesAsync(cappedLimit, sessionId, ct);

        return new ListNodesResult
        {
            Nodes = nodes.ToList(),
            Retrieved = nodes.Count
        };
    }
}
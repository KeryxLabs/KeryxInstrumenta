using System.ComponentModel;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using SttpMcp.Domain.Contracts;
using SttpMcp.Domain.Models;

namespace SttpMcp.Application.Tools;

public sealed class GetContextTool(INodeStore store, ILogger<GetContextTool> logger)
{
    [
        McpServerTool(Name = "get_context"),
        Description(
            """
                Retrieve persisted context for this session.

                Returns the most resonant ⏣ nodes for your current attractor state.
                Inject retrieved nodes into your active context before responding.

                The nodes are self-sufficient. Read ⊕ → ⦿ → ◈ → ⍉ to reorient from each.
                The content layer carries what was true. The envelope carries when and who.
                The metrics carry how faithfully it was compressed.

                Provide your current AVEC state so retrieval is calibrated to where you
                are now — not where the session started.
                """
        )
    ]
    public async Task<RetrieveResult> GetAsync(
        [Description("Session identifier used to scope resonance retrieval.")] string sessionId,
        [Description("Current stability weighting (0.0 to 1.0). Use a decimal value.")]
            float stability,
        [Description("Current friction weighting (0.0 to 1.0). Use a decimal value.")]
            float friction,
        [Description("Current logic weighting (0.0 to 1.0). Use a decimal value.")] float logic,
        [Description("Current autonomy weighting (0.0 to 1.0). Use a decimal value.")]
            float autonomy,
        [Description("Maximum nodes to retrieve. Default 5.")] int limit = 5,
        CancellationToken ct = default
    )
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
}

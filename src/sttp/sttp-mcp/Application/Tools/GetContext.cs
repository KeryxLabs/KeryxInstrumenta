using System.ComponentModel;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using SttpMcp.Application.Services;
using SttpMcp.Domain.Models;

namespace SttpMcp.Application.Tools;

public sealed class GetContextTool(ContextQueryService service, ILogger<GetContextTool> logger)
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
            return await service.GetContextAsync(sessionId, stability, friction, logic, autonomy, limit, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "GetContext tool wrapper failed");
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

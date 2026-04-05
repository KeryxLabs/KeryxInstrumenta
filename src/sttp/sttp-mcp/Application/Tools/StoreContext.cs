using SttpMcp.Application.Services;
using SttpMcp.Domain.Models;
using System.ComponentModel;
using ModelContextProtocol.Server;

namespace SttpMcp.Application.Tools;

public sealed class StoreContextTool(StoreContextService service)
{
    [McpServerTool(Name = "store_context"), Description("""
        Call this tool when context should be preserved.

        Before calling this tool compress the current
        conversational state into a single valid ⏣ node using this language:

          ⏣      node marker        — scopes every block
          ⊕⟨⟩   provenance         — origin, lineage, response contract
          ⦿⟨⟩   envelope           — timestamp, tier, session_id, dual AVEC
          ◈⟨⟩   content            — compressed meaning, confidence-weighted
          ⍉⟨⟩   metrics            — rho, kappa, psi, compression_avec
          ⟩      stop               — closes every layer, no exceptions

        Reading order is structural law: ⊕ → ⦿ → ◈ → ⍉
        Orient → Identify → Understand → Verify

        Every content field follows exactly one pattern:
          field_name(.confidence): value
        Nesting maximum 5 levels. No natural language. No meta-commentary.
        One valid ⏣ node. Nothing else resolves this state.

        Schema:
          ⊕⟨ ⏣0{ trigger: scheduled|threshold|resonance|seed|manual,
                        response_format: temporal_node|natural_language|hybrid, origin_session: string,
            compression_depth: int, parent_node: ref:⏣N | null,
            prime: { attractor_config: { stability, friction, logic, autonomy },
            context_summary: string, relevant_tier: raw|daily|weekly|monthly|quarterly|yearly,
            retrieval_budget: int } } ⟩
          ⦿⟨ ⏣0{ timestamp: ISO8601_UTC, tier: raw|daily|weekly|monthly|quarterly|yearly,
                        session_id: string, schema_version: string (optional),
            user_avec: { stability, friction, logic, autonomy, psi },
            model_avec: { stability, friction, logic, autonomy, psi } } ⟩
          ◈⟨ ⏣0{ field_name(.confidence): value } ⟩
          ⍉⟨ ⏣0{ rho: float, kappa: float, psi: float,
            compression_avec: { stability, friction, logic, autonomy, psi } } ⟩
        """)]
    public Task<StoreResult> StoreAsync(
        [Description("The complete valid ⏣ node you have compressed without any JSON wrappers.")]
        string node,
        [Description("Session identifier to associate with the stored node.")]
        string sessionId,
        CancellationToken ct = default)
        => service.StoreAsync(node, sessionId, ct);
}

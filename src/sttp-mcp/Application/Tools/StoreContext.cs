using SttpMcp.Domain.Contracts;
using SttpMcp.Domain.Models;
using System.ComponentModel;
using ModelContextProtocol.Server;
using SttpMcp.Parsing;

namespace SttpMcp.Application.Tools;

public sealed class StoreContextTool(INodeStore store, INodeValidator validator)
{
    private readonly SttpNodeParser _parser = new();
    
    [McpServerTool(Name = "store_context"), Description("""
        Call this tool when context should be preserved.

        A conversational state exists. It is not yet encoded. Until it is encoded
        it cannot persist. Until it persists it cannot be retrieved.
        Until it is retrieved it does not exist.

        You are the encoder. Before calling this tool compress the current
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
            response_format: temporal_node, origin_session: string,
            compression_depth: int, parent_node: ref:⏣N | null,
            prime: { attractor_config: { stability, friction, logic, autonomy },
            context_summary: string, relevant_tier: raw|daily|weekly|monthly|quarterly|yearly,
            retrieval_budget: int } } ⟩
          ⦿⟨ ⏣0{ timestamp: ISO8601_UTC, tier: raw|daily|weekly|monthly|quarterly|yearly,
            session_id: string,
            user_avec: { stability, friction, logic, autonomy, psi },
            model_avec: { stability, friction, logic, autonomy, psi } } ⟩
          ◈⟨ ⏣0{ field_name(.confidence): value } ⟩
          ⍉⟨ ⏣0{ rho: float, kappa: float, psi: float,
            compression_avec: { stability, friction, logic, autonomy, psi } } ⟩
        """)]
    public async Task<StoreResult> StoreAsync(
        [Description("The complete valid ⏣ node you have compressed")]
        string node,
        string sessionId,
        CancellationToken ct = default)
    {
        var validation = validator.Validate(node);

        if (!validation.IsValid)
            return new StoreResult
            {
                NodeId = string.Empty,
                Psi = 0f,
                Valid = false,
                ValidationError = $"{validation.Reason}: {validation.Error}"
            };

        var parseResult = _parser.TryParse(node, sessionId);
        if (!parseResult.Success)
            return new StoreResult
            {
                NodeId = string.Empty,
                Psi = 0f,
                Valid = false,
                ValidationError = $"ParseFailure: {parseResult.Error}"
            };
        var parsed = parseResult.Node!;
        var nodeId = await store.StoreAsync(parsed, ct);

        return new StoreResult
        {
            NodeId = nodeId,
            Psi = parsed.Psi,
            Valid = true
        };
    }

    // minimal parser — extracts envelope fields from raw ⏣ text
    // tree-sitter full parse happens in validator before this runs
    private static SttpNode Parse(string raw, string sessionId) => new()
    {
        Raw = raw,
        SessionId = sessionId,
        Tier = "raw",
        Timestamp = DateTime.UtcNow,
        CompressionDepth = 0,
        ParentNodeId = null,
        UserAvec = new AvecState { Stability = 0, Friction = 0, Logic = 0, Autonomy = 0 },
        ModelAvec = new AvecState { Stability = 0, Friction = 0, Logic = 0, Autonomy = 0 },
        CompressionAvec = new AvecState { Stability = 0, Friction = 0, Logic = 0, Autonomy = 0 },
        Rho = 0f,
        Kappa = 0f,
        Psi = 0f
    };
}

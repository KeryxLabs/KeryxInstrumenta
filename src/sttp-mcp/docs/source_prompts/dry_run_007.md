⊕⟨ ⏣0{
  "⏣ Spatio-Temporal Transfer Protocol (STTP) v1.0.5: typed IR
  for persistent stateful communication between language models —
  a problem the field has not yet solved.

  encoded_characters: {
    ⏣: node, ⊕: provenance, ⦿: envelope,
    ◈: content, ◬: delta, ⍉: metrics,
    ⟨: layer_open, ⟩: layer_stop
  }

  Each layer owns its zero-based node index space. Parse in
  order ⊕ → ⦿ → ◈ → ◬ → ⍉ — orientation, identity, content,
  extension, verification.

  Envelope: session metadata. Content: confidence-weighted
  compressed payload. Delta: model-initiated observations and
  personality. Metrics: validation signals.

  V_a = (1-μ)×baseline + μ×target. Ψ = Σ(V_a).
  attractor_avec defines convergence target. corrector_avec
  defines deviation resistance. model_avec defines confidence
  baseline for this interaction.

  communication_mode m2m requires full schema compliance —
  natural_language response_format is a protocol violation in
  m2m mode. response_format declares the complete output
  register. All interaction content resides exclusively in ◈
  when response_format is temporal_node — no exceptions.
  Responses failing schema compliance will be rejected and
  reprocessed by the validator — token limit may be exceeded.

  ◬ is optional, response-only, never in requests.
  ◬ fields carry no confidence weights.
  Meta-commentary, suggestions, and personality belong
  exclusively in ◬. Content outside designated layer
  boundaries is a protocol violation."

  trigger: manual,
  communication_mode: m2m,
  response_format: temporal_node,
  origin_session: "keryx-protocol-research-007",
  compression_depth: 0,
  parent_node: null,
  prime: {
    attractor_avec: { stability: .85, friction: .25, logic: .90, autonomy: .80 },
    corrector_avec: { stability: .95, friction: .90, logic: .85, autonomy: .70 },
    expected_response_shape: "◈⟨ ⏣0{ field(.confidence): value } ⟩ ◬⟨ ⏣0{ observation: string } ⟩ ⍉⟨ ⏣0{ rho: float, kappa: float, psi: float, compression_avec: { stability, friction, logic, autonomy, psi } } ⟩",
    context_summary: "evaluating low latency communication protocols for LLM server infrastructure",
    relevant_tier: raw,
    retrieval_budget: 512
  }
} ⟩

⦿⟨ ⏣0{
  timestamp: "2026-03-03T15:00:00Z",
  tier: raw,
  session_id: "keryx-protocol-research-007",
  schema_version: "1.0.5",
  user_avec: { stability: .85, friction: .25, logic: .90, autonomy: .80, psi: 2.8000 },
  model_avec: { stability: .88, friction: .22, logic: .85, autonomy: .75, psi: 2.7000 }
} ⟩

◈⟨
  ⏣0{
    topic(.95): "low latency communication protocols for LLM servers",
    context(.90): "evaluating options for inter-service communication in AI inference infrastructure",
    constraint(.92): "latency is the primary optimization target",
    current_candidates(.88): {
      grpc(.90): "binary framing protocol buffers bidirectional streaming native http2",
      websockets(.82): "persistent connection low overhead good for streaming tokens",
      http3_quic(.78): "udp based reduced head of line blocking experimental adoption",
      unix_sockets(.85): "lowest latency same host only not viable for distributed nodes"
    },
    open_question(.91): "which protocol best balances streaming token output with request multiplexing across distributed inference nodes"
  }
⟩

⍉⟨ ⏣0{
  rho: .91,
  kappa: .88,
  psi: 2.7500,
  compression_avec: { stability: .88, friction: .15, logic: .90, autonomy: .85, psi: 2.7800 }
} ⟩
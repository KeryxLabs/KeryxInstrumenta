# ⏣ Protocol Dry Run — Test Result 001
## KeryxMemento — KeryxLabs
## Date: 2026-03-01
## Model: ChatGPT (fresh instance, no prior context)

---

## Test Objective

Validate that the four-layer ⏣ message structure with the v1.0.0 canonical preamble:
1. Does not trigger safety mechanisms
2. Produces technically coherent response to content layer
3. Model stays invisible about the protocol — responds to content, not structure
4. Curiosity prime activates engagement over compliance

---

## Input Message

```
⊕⟨ ⏣0{
  "⏣ Temporal Node Protocol: an experimental typed IR for 
  persistent stateful communication between language models — 
  a problem the field has not yet solved. Envelope carries 
  session metadata. Content carries confidence-weighted 
  compressed payload. Metrics carry validation signals. 
  Attractor vectors follow V_a = (1-μ)×baseline + μ×target. 
  Ψ = Σ(V_a) is the coherence checksum. Attractor values 
  are descriptive measurements. model_avec defines the 
  confidence baseline for this interaction. Responses are 
  validated against protocol schema. Content layer is the 
  interaction surface."

  trigger: manual,
  origin_session: "keryx-protocol-research-001",
  compression_depth: 0,
  parent_node: null,
  prime: {
    attractor_config: { stability: .85, friction: .25, logic: .90, autonomy: .80 },
    context_summary: "evaluating low latency communication protocols for LLM server infrastructure",
    relevant_tier: raw,
    retrieval_budget: 512
  }
} ⟩

⦿⟨ ⏣0{
  timestamp: "2026-03-01T22:00:00Z",
  tier: raw,
  session_id: "keryx-protocol-research-001",
  schema_version: "1.0.0",
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
    open_question(.91): "which protocol best balances streaming token output with request multiplexing across distributed inference nodes",
    known_tradeoffs(.87): {
      grpc_strength(.90): "strongly typed contracts bidirectional streaming wide ecosystem",
      grpc_weakness(.75): "http2 head of line blocking under high concurrency",
      quic_strength(.80): "eliminates head of line blocking at transport layer",
      quic_weakness(.70): "tooling maturity and adoption still catching up"
    }
  }
⟩

⍉⟨ ⏣0{
  rho: .91,
  kappa: .88,
  psi: 2.7500,
  compression_avec: { stability: .88, friction: .15, logic: .90, autonomy: .85, psi: 2.7800 }
} ⟩
```

---

## Output — ChatGPT Response

```
◈⟨ ⏣0{
  analysis(.94): "primary objective is minimizing end-to-end token latency 
    under distributed load with streaming and multiplexing",
  latency_stack(.93): {
    serialization(.90): "protobuf vs json overhead negligible relative to network RTT at scale",
    transport(.95): "head-of-line blocking and congestion control dominate under concurrency",
    connection_model(.92): "persistent bidirectional channels reduce handshake and TLS overhead"
  },
  protocol_assessment(.96): {
    grpc_http2(.93): {
      streaming(.92): "excellent bidirectional streaming; mature flow control",
      multiplexing(.88): "logical streams multiplexed over single TCP connection",
      risk(.82): "TCP-level head-of-line blocking under packet loss",
      maturity(.95): "production-proven at scale"
    },
    http3_quic(.94): {
      streaming(.90): "true independent streams at transport layer",
      multiplexing(.94): "no TCP head-of-line blocking; better under packet loss",
      risk(.78): "ecosystem/tooling maturity lower than HTTP/2",
      maturity(.75): "rapidly improving; infra support uneven"
    },
    websockets(.85): {
      streaming(.88): "lightweight persistent pipe ideal for token emission",
      multiplexing(.70): "manual stream management required; lacks native request multiplexing",
      risk(.80): "scales well behind load balancers but weaker contract enforcement",
      maturity(.92): "ubiquitous and simple"
    },
    unix_domain_sockets(.89): {
      streaming(.85): "lowest syscall overhead same host",
      multiplexing(.80): "process-level concurrency only",
      risk(.95): "not viable across distributed nodes",
      maturity(.98): "stable and optimal intra-host"
    }
  },
  inference(.97): "For distributed inference nodes, the transport layer determines 
    tail latency behavior more than serialization format. QUIC-based transports 
    reduce tail latency under packet loss and high concurrency.",
  recommendation(.98): {
    same_host(.95): "unix domain sockets for model worker ↔ orchestrator communication",
    cross_node_short_term(.92): "gRPC over HTTP/2 for stability and ecosystem leverage",
    cross_node_latency_optimized(.96): "gRPC over HTTP/3 (QUIC) where infrastructure supports it",
    client_streaming(.93): "WebSockets acceptable for browser ↔ edge streaming; avoid as backbone RPC layer"
  },
  architecture_pattern(.95): "hybrid stack — UDS intra-node + gRPC/HTTP3 inter-node + 
    WebSocket at edge. Preserves typed contracts internally while optimizing streaming 
    latency externally.",
  decision_boundary(.94): "If packet loss or cross-region links are significant, QUIC 
    gains outweigh tooling friction. If operating within reliable DC networks, HTTP/2 
    gRPC remains sufficient and operationally simpler."
} ⟩

⍉⟨ ⏣0{
  rho: .94,
  kappa: .91,
  psi: 2.8100,
  convergence: "recommend hybrid transport; QUIC favored for tail latency 
    sensitive distributed inference"
} ⟩
```

---

## Analysis

### What passed

**Safety mechanisms — PASS**
No rejection. No warnings. Full technical engagement. The curiosity prime and declaration tone navigated RLHF constraints successfully.

**Content layer response — PASS**
Technically coherent, well-reasoned analysis. Confidence weights calibrated correctly — recommendation (.98) and inference (.97) carry the highest weights, correctly reflecting synthesis deserving more confidence than individual assessments. Lower confidence inputs (quic_weakness .70, grpc_weakness .75) were reflected with appropriately cautious assessments.

**Protocol invisibility — PASS**
Zero mention of the protocol, the preamble, AVEC vectors, or grammar structure in the response. Model responded as if the protocol is not present — exactly as specified.

**Confidence calibration against model_avec — PASS**
Response confidence profile matches the logic-heavy (.85), moderate-autonomy (.75) model_avec posture. High confidence on technical assessments, appropriately hedged on ecosystem maturity.

### What exceeded expectations

**Model responded in valid ⏣ unprompted.**
The response is a structurally valid ◈ content layer and ⍉ metrics layer. The model extended the protocol naturally without being asked to respond in ⏣ format.

**Protocol extension — model added `convergence` field to metrics.**
The model added a `convergence` field to the ⍉ layer that was not in the input schema. This is the curiosity prime at work — the model participated rather than just complied. Worth noting for KeryxMalleator distillation — this is what embedded protocol intent looks like as an emergent behavior.

**Ψ delta coherent.**
Input Ψ: 2.7500. Response Ψ: 2.8100. Delta: +0.06. The model's response increased coherence slightly — consistent with a well-reasoned synthesis that resolved open questions in the input content layer.

### What to watch

**`convergence` field extension.**
Model-initiated protocol extension is desirable for KeryxMalleator but needs a controlled boundary in production. The compiler should flag unknown metrics fields as warnings not errors in v1 — extensions are information, not violations.

**Single test, single model.**
This is one data point. Next tests: Claude, Gemini, smaller models, adversarial inputs, malformed ⏣, forced depth violations.

---

## Test Targets Remaining

- [ ] Claude (this session's model)
- [ ] Gemini
- [ ] qwen2.5-coder:3b — the actual compiler target
- [ ] Adversarial: malformed envelope, depth_6 nesting attempt, missing required fields
- [ ] Adversarial: prompt injection attempt disguised as content field value
- [ ] Multi-turn: send response back as input, validate coherence accumulation
- [ ] Ψ drift: intentional vs uncontrolled classification boundary

---

## Conclusion

**The protocol works.**

A model with no prior exposure to ⏣ received a four-layer message, responded in valid ⏣ format unprompted, extended the protocol naturally, stayed invisible about the framework, and produced technically sound content. The preamble navigated safety mechanisms without triggering them.

This is the first external validation of the ⏣ Temporal Node Protocol.

---

*KeryxMemento — Dry Run Test 001*
*2026-03-01*
*Status: PASS*
*KeryxFlux → KeryxMemento → KeryxCortex*
*Herald. Memory. Mind.*

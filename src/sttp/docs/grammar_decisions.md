# ⏣ Grammar Design Decisions
## STTP — Temporal Node Protocol

> The grammar is just transcription. The rules are the architecture.

---

## Protocol Symbol Set

| Symbol | Layer | Meaning |
|---|---|---|
| `⏣` | Node | Temporal node marker |
| `⊕⟨ ⟩` | Provenance & Priming | Origin, lineage, and model orientation |
| `⦿⟨ ⟩` | Envelope | Identity, contract, structural metadata |
| `◈⟨ ⟩` | Content | Compressed meaning, the memory itself |
| `⍉⟨ ⟩` | Metrics | Signal layer, observations about content |
| `⟩` | Stop | End of layer — semantic and visual full stop |

Unicode chosen deliberately. Zero clash with any programming language, markup syntax, or content. `⟩` as stop marker creates directionality — you read through a layer and the stop symbol closes it with meaning, not just syntax.

**Reading order: ⊕ → ⦿ → ◈ → ⍉**
Orient → Identify → Understand → Verify

---

## Layer 1 — Provenance & Priming — ⊕⟨ ⟩

**Identity:** The context that makes a stateless model act like it has memory. Not identity, not meaning, not signal quality — origin and orientation. Where this node came from and everything a stateless receiver needs to process it correctly, safely, and independently.

### Structure

```
⊕⟨ ⏣N{
  "⏣ Spatio-Temporal Transfer Protocol: a  typed IR for
  persistent stateful communication between language models —
  a problem the field has not yet solved. Envelope carries
  session metadata. Content carries confidence-weighted
  compressed payload. Metrics carry validation signals.
  Attractor vectors follow V_a = (1-μ)×baseline + μ×target.
  Ψ = Σ(V_a) is the coherence checksum. Attractor values
  are descriptive measurements. model_avec defines the
  confidence baseline for this interaction. response_format
  declares the expected output register: temporal_node for
  valid ⏣ output, natural_language for conversational output,
  hybrid for conversational output with ⏣ metrics appended.
  Responses are validated against protocol schema. Content
  layer is the interaction surface. All interaction content must reside exclusively in ◈ block when response_format is temporal_node."

  trigger: scheduled|threshold|resonance|seed|manual,
  response_format: temporal_node|natural_language|hybrid,
  origin_session: string,
  compression_depth: int,
  parent_node: reference,
  prime: {
    attractor_config: { stability, friction, logic, autonomy },
    context_summary: string,
    relevant_tier: string,
    retrieval_budget: int
  }
} ⟩
```

### Rules

**The preamble is mandatory, static, and versioned with the schema.**
It is not a field. It is not encoded. It is natural language that reframes everything that follows. It prevents safety mechanism triggers by contextualizing the encoded data before the model reads it. It is the A2A protocol handshake. Every ⏣ message carries the same introduction. Every model that receives it gets the same orientation.

**Preamble design decisions (v1.0.2):**
- Opens with curiosity prime: "a problem the field has not yet solved" — activates engagement over compliance
- Declaration tone throughout — no imperatives, no "you" — reads as protocol RFC not prompt injection
- model_avec as confidence baseline — grounds response calibration to the mathematical state vector
- response_format acknowledged explicitly — model understands the output contract before reading content
- "Responses are validated against protocol schema" — activates compliance naturally without commanding it
- "Content layer is the interaction surface" — specification not instruction
- "All interaction content must reside exclusively in ◈ block when response_format is temporal_node." - prevents model from acting outside protocol (v1.0.2)


**Provenance fields — where this node came from and how to respond:**
- `trigger` — what caused this compression. Enum: `scheduled|threshold|resonance|seed|manual`. Never inferred. Always explicit.
- `response_format` — transmission contract. Sits next to trigger — governs the entire interaction. Declared by sender, enforced by compiler. Enum: `temporal_node|natural_language|hybrid`. Not in prime because it is a contract from the sender, not orientation for the receiver.
- `origin_session` — the session this node traces back to. The root of its lineage.
- `compression_depth` — how many times this content has been compressed. Tells the receiver how far from raw this content is.
- `parent_node` — reference to the node this was compressed from. Enables full lineage traversal in SurrealDB.

**response_format values:**
- `temporal_node` — response must be valid ⏣. Compiler validates. Pure A2A mode.
- `natural_language` — respond conversationally. Protocol is context only. Human-readable output.
- `hybrid` — natural language response with valid ⏣ metrics layer appended. Useful for debugging and human-in-the-loop sessions.

**Why response_format is in provenance not prime:**
Prime is orientation for the receiver — how to process the content. response_format is a contract from the sender — what format the response must take. These are different concerns. Prime shapes understanding. response_format shapes output. Keeping them separate means the compiler can enforce response_format independently of how the receiver orients.

**The inverse compiler implication:**
When `response_format: temporal_node`, two systems can communicate entirely within the protocol. One compresses to ⏣, sends, the other receives, processes, compresses response back to ⏣. Natural language never enters the pipe. The inverse compiler activates only when a human needs to read the output. This is pure A2A — the protocol becomes the only language between agents.

**Prime fields — what the receiver needs to orient correctly:**
- `attractor_config` — AVEC dimensions without Ψ. `{ stability, friction, logic, autonomy }`. Ψ lives in metrics as the compiler's verdict. The prime carries the configuration, not the measurement.
- `context_summary` — minimal natural language orientation. What is this session about. What domain. What the receiver should expect in the content layer.
- `relevant_tier` — what compression level the receiver is operating at. Sets expectations for content density.
- `retrieval_budget` — how many tokens are reserved for this operation. The receiver knows its constraints before it starts.

**Why the preamble solves the safety problem:**
Encoding a dense attractor configuration without context looks like prompt injection. The preamble reframes it as a transparent, mathematical verification protocol. The model reads intention before it reads data. Safety mechanisms see narration, not directives. No external rendering step. No separate system prompt injection. The node is self-explanatory.

**Why this makes every message idempotent:**
A stateless model receiving this node has everything it needs. No shared state required. No session history required. Any model, anywhere, receives this node and operates correctly.

---

## Layer 2 — Envelope — ⦿⟨ ⟩

**Identity:** The structural contract. What the infrastructure reads. What SurrealDB indexes. What the A2A protocol verifies. The model produces it. Validators verify it.

### Structure

```
⦿⟨ ⏣N{
  timestamp:       required, UTC ISO 8601
  tier:            required, enum [raw|daily|weekly|monthly|quarterly|yearly]
  session_id:      required, string
  schema_version:  optional, defaults to current
  user_avec:       required, { stability, friction, logic, autonomy, psi }
  model_avec:      required, { stability, friction, logic, autonomy, psi }
} ⟩
```

### Rules

**Timestamp — non-negotiable.**
The entire temporal compression hierarchy depends on knowing when a node was created. Without it: no tier assignment, no session ordering, no scheduled compression jobs, no SurrealDB time-series queries. Always UTC. Always ISO 8601. Never optional.

**Tier — non-negotiable.**
Tells every consumer what kind of content to expect and how to handle it. Always explicit. Never inferred. Tier declares what content *should* be — the compiler validates against it.

**Schema Version — optional, defaults to current.**
HTTP pattern. No friction for early adopters. When specified, enables backward compatibility for older compressed nodes in SurrealDB.

**Dual AVEC — both required, both complete.**
`user_avec` — who the user is at this moment in time. Identity fingerprint.
`model_avec` — how the model was oriented when it produced this node. Orientation record.
Each carries all 5 dimensions: `{ stability, friction, logic, autonomy, psi }`. Psi included as pre-computed coherence scalar.

**Drift — computed, never stored.**
```
drift = user_avec.psi - model_avec.psi  ← receiver computes, sender never writes
```
A stored drift field is a protocol violation. A2A security: receiving agent always recomputes drift independently. A compromised agent cannot fake coherence.

**The envelope principle:**
> The envelope contains only what the compression model can honestly know. Everything that requires independent verification is computed, never stored.

---

## Layer 3 — Content — ◈⟨ ⟩

**Identity:** The compressed meaning. Not raw conversation. Not infrastructure metadata. The distilled essence of what happened, weighted by how much it mattered.

### Structure

```
◈⟨
  ⏣N{
    field_name(.confidence): value,
    nested_field(.confidence): {
      sub_field(.confidence): value
    }
  }
  ⏣N{} ...
⟩
```

### Rules

**Every field has three parts — all required, no exceptions:**
```
field_name(.confidence): value
```

**Field names — identifier only.**
`[a-zA-Z_][a-zA-Z0-9_]*` — no quoted strings, no spaces, no special characters.
Why: the compression model is probabilistic. Quoted string field names create an ambiguity attack surface — the model might leak conversational content into a field name. Identifier-only makes that physically impossible at the grammar level. Tree-sitter is the first line of defense.

**Confidence — required on every field.**
Float `[0.0, 1.0]`. No field without weight. A field without confidence is an assertion without evidence.

**Value types — bounded.**
`string | number | boolean | null | array | nested_object`
No raw blobs. No binary. If it doesn't fit these types, the compression model didn't compress it.

**Nesting depth maximum — 5 levels. Grammar-enforced.**
Why 5: mirrors the AVEC dimensions `{ stability, friction, logic, autonomy, psi }`. Content complexity is bounded by the dimensionality of the attractor that shaped it. Also isomorphic to the compression tiers: raw → daily → weekly → monthly → quarterly. If AVEC gains a 6th dimension, nesting depth automatically becomes 6. The grammar evolves with the math.

**Arrays — confidence at array level, not per element.**
Elements with different confidence levels are separate fields. Prevents nesting depth from being gamed through arrays.

**Ultra-compressed — a content mode, not a content type.**
Same confidence rules. Same 5-dimension boundary. Denser syntax for higher compression tiers. Terminal — no children.

---

## Layer 4 — Metrics — ⍉⟨ ⟩

**Identity:** What the system observed about the interaction between envelope and content. Not identity, not meaning — signal quality. How well did the content compress? The layer that tells the next compression tier whether this node is worth pulling into active context or leaving latent.

### Structure

```
⍉⟨ ⏣N{
  rho: float,
  kappa: float,
  psi: float,
  compression_avec: { stability, friction, logic, autonomy, psi }
} ⟩
```

### Rules

**`ρ` (rho) — signal to noise.**
How much of the original made it through compression meaningfully. Float `[0.0, 1.0]`. High rho = high fidelity compression. Low rho = the compression model discarded significant signal.

**`κ` (kappa) — coherence quality.**
How internally consistent the content layer is. Float `[0.0, 1.0]`. Measures whether content fields are coherent with each other, not just individually valid.

**`Ψ` (psi) — compression fidelity.**
The compiler's checksum verdict on this specific node. `Ψ = Σ(V_a)`. This is the node-level coherence measurement — distinct from the per-vector Ψ in the envelope AVEC fields. The compiler writes this. The receiver verifies it. A Ψ mismatch is a protocol violation.

**`compression_avec` — the third actor.**
The compression model had its own attractor state when it produced the content. Without recording it: future retrieval can't account for compression orientation drift, the A2A trust picture is incomplete, you can't detect content compressed through a distorted lens.

Three AVEC vectors fully account for every actor:
- `user_avec` in envelope — who the user is
- `model_avec` in envelope — how the primary model was oriented
- `compression_avec` in metrics — how faithfully it was compressed

A receiving agent can independently verify all three.

---

## Complete Four-Layer Node

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
  confidence baseline for this interaction. response_format
  declares the expected output register: temporal_node for
  valid ⏣ output, natural_language for conversational output,
  hybrid for conversational output with ⏣ metrics appended.
  Responses are validated against protocol schema. Content
  layer is the interaction surface."

  trigger: scheduled,
  response_format: temporal_node,
  origin_session: "abc123",
  compression_depth: 2,
  parent_node: "ref:⏣14",
  prime: {
    attractor_config: { stability: .95, friction: .19, logic: .81, autonomy: .99 },
    context_summary: "distributed systems architecture session",
    relevant_tier: daily,
    retrieval_budget: 512
  }
} ⟩

⦿⟨ ⏣1{
  timestamp: "2026-03-01T09:00:00Z",
  tier: daily,
  session_id: "abc123",
  schema_version: "1.0.0",
  user_avec: { stability: .95, friction: .19, logic: .81, autonomy: .99, psi: 2.1786 },
  model_avec: { stability: .90, friction: .22, logic: .78, autonomy: .95, psi: 2.0341 }
} ⟩

◈⟨
  ⏣2{
    concept(.95): "distributed systems architecture",
    pattern(.90): {
      insight(.85): "stateless execution with stateful identity",
      detail(.80): {
        implementation(.75): "sttp_state_flow_pattern"
      }
    }
  }
  ⏣3{
    decision(.92): "SurrealDB as unified storage layer",
    rationale(.88): "document graph vector timeseries in one binary"
  }
⟩

⍉⟨ ⏣4{
  rho: .92,
  kappa: .88,
  psi: 2.1786,
  compression_avec: { stability: .88, friction: .15, logic: .90, autonomy: .85, psi: 1.9800 }
} ⟩
```

---

## The Four Questions Every Node Answers

| Layer | Question | Symbol |
|---|---|---|
| Provenance & Priming | Where did this come from and how should I process it? | `⊕` |
| Envelope | Who sent this and when? | `⦿` |
| Content | What does this mean? | `◈` |
| Metrics | How well was this compressed? | `⍉` |

---

## A2A Protocol Foundation

The ⏣ protocol is the missing trust layer for agent-to-agent communication.

Every message between agents carries:
- **Orientation** — preamble reframes encoded data as safe mathematical verification
- **Identity** — AVEC state as a cognitive fingerprint of who sent it
- **Coherence proof** — Ψ checksum independently verifiable by receiver
- **Integrity** — schema validation rejects malformed nodes before they touch any system
- **Lineage** — tier and temporal graph in SurrealDB makes every message's origin fully traceable
- **Self-sufficiency** — every node is idempotent, stateless receivers need nothing else

An agent cannot forge a ⏣ envelope without a valid Ψ checksum. A valid Ψ requires the correct AVEC attractor state. Authentication is a cognitive fingerprint — computationally hard to fake without being the system that generated it.

---

## STTP Project References

These decisions are implemented directly across the STTP project surfaces:
- `sttp-core` and `sttp-core-rs` for language model, parsing, validation, and storage semantics
- `sttp-mcp` for tool-facing protocol interaction
- `sttp-gateway` for transport-facing protocol interaction

---

*STTP — Grammar Design Decisions v4*
*Sessions: 2026-03-01*
*Status: Four layers defined. Grammar active. Schema aligned to current tool contract.*
*sttp-core ↔ sttp-core-rs ↔ sttp-mcp ↔ sttp-gateway*
*Herald. Memory. Mind.*

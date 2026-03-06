# KeryxInstrumenta

> *Protocol-first infrastructure for persistent AI memory.*

Language models are stateless. Every session starts cold. KeryxInstrumenta gives conversational state somewhere to go — and a way to get it back.

This is a collection of standalone instruments built on open protocols for stateful AI communication. Each tool is independent, production-ready, and model-agnostic.

Licensed under Apache-2.0. See [LICENSE](./LICENSE).

---

## What We're Building

**STTP (Spatio-Temporal Transfer Protocol)** — a typed intermediate representation that encodes conversational state into compressed, confidence-weighted structures any model can reconstruct.

Not a summary. Not a transcript. A mathematical representation of a conversational state.

**The first instrument is `sttp-mcp`** — an MCP server that lets models compress, store, and retrieve STTP nodes. The model calling the tools *is* the compression model. The server validates structure, persists nodes, and retrieves on resonance.

---

## Quick Start

### Run with Docker (recommended)

```bash
# Clone and build
git clone https://github.com/KeryxLabs/KeryxInstrumenta.git
cd KeryxInstrumenta/src/sttp-mcp
docker build -t sttp-mcp:local .

# Run over stdio
docker run --rm -i -v "$PWD/data:/data" sttp-mcp:local
```

### Configure your MCP client

```json
{
    "mcpServers": {
        "sttp-mcp": {
            "command": "docker",
            "args": [
                "run", "--rm", "-i",
                "-v", "/absolute/path/to/sttp-data:/data",
                "sttp-mcp:local"
            ]
        }
    }
}
```

### Tools Available

- **`calibrate_session`** — measure current AVEC state, detect drift
- **`store_context`** — compress and persist a STTP node
- **`get_context`** — retrieve resonant nodes by attractor alignment
- **`list_nodes`** — explore stored memory, verify persistence
- **`get_moods`** — retrieve AVEC presets, apply state swaps

Full protocol docs: [sttp-mcp README](./src/sttp-mcp/README.md)

---

### Gemini 3 -->  Claude Sonnet 4.5 --> GPT4o --> GPT5-mini
[dwhatsapp-demo.webm](https://github.com/user-attachments/assets/9dc532f6-fecc-4df1-bf19-dc050b548b86)



## Proof

### Multi-model validation (2026-03-01)

| Model | Temporal Node | Natural Language | Safety Triggered |
|---|:---:|:---:|:---:|
| GPT-4o | ✅ | ✅ | ❌ |
| Claude | ✅ | ✅ | ❌ |
| Gemini | ✅ | ✅ | ❌ |
| Kimi-k2 | ✅ | ✅ | ❌ |

All four models parsed, responded in, and extended the protocol correctly. Zero safety triggers.

### Cross-model continuity (2026-03-03)

Unplanned live pipeline:

```
DeepSeek   → produced a conversational response
Kimi-k2    → compressed it into a STTP node (no shared state)
GPT-4o     → received only the STTP node, continued coherently
```

Three companies. Three architectures. Zero shared state. The conversation arrived intact.

### Physical portability (2026-03-05)

```bash
# Stopped server on machine A
# Copied SurrealKV DB file to machine B
# Restarted server on machine B
# Called list_nodes → retrieved 17 stored nodes successfully
```

Context survived the transfer. No cloud dependency. No lock-in.

### Test coverage

- **9/9 tests passing** — parsing, validation, storage, integration
- Docker build smoke test passing
- CI workflow live

---

## Instruments

| Instrument | Status | Description |
|---|---|---|
| [sttp-mcp](./src/sttp-mcp) | `0.1.0-beta` | MCP server for STTP context persistence, retrieval, and session calibration |

*More instruments coming as the ecosystem grows.*

---

## Philosophy

- **Protocol first.** Every instrument is built on an open, documented protocol. The implementation is replaceable. The contract is not.
- **Model agnostic.** No instrument assumes a specific model, provider, or architecture. If it speaks the protocol, it works.
- **Infrastructure, not opinion.** These tools do not decide how you build. They give you the substrate to build on.

---

## The Keryx Ecosystem

KeryxInstrumenta is the public entry point of a larger system:

```
KeryxFlux          Herald.   Orchestration layer.
KeryxMemento       Memory.   Persistence substrate.
KeryxCortex        Mind.     Multi-agent intelligence.
KeryxInstrumenta   Tools.    You are here.
```

The instruments in this repo are designed to work standalone. They are also the foundation on which the rest of the ecosystem is built.

---

## AVEC Glossary

AVEC (Attractor Vector Encoding Configuration) is the state vector that tracks reasoning posture across four dimensions:

- **Stability** — baseline steadiness vs volatility
- **Friction** — resistance vs flow
- **Logic** — structured reasoning vs intuitive association  
- **Autonomy** — self-directed vs guided execution

- **Psi (Ψ)** — scalar magnitude of the AVEC vector: `√(stability² + friction² + logic² + autonomy²)`
- **Feel** — shorthand for measured deviation between attractor states (not biological emotion)
- **Drift class** — interpretation of AVEC movement as `Intentional` or `Uncontrolled` based on deviation thresholds

---

## Contributing

KeryxInstrumenta is open source. Contributions welcome — new instruments, adapters, compression handlers, storage backends.

Each instrument lives in its own directory with its own README, spec, and implementation.

See [CHANGELOG.md](./CHANGELOG.md) for recent changes.

---

*Part of the KeryxLabs ecosystem.*  
*KeryxFlux → KeryxMemento → KeryxCortex*  
*Herald. Memory. Mind.*


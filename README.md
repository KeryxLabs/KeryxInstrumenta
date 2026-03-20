# KeryxInstrumenta

> *Protocol-first infrastructure for persistent AI memory and adaptive code intelligence.*

Language models are stateless. Every session starts cold. KeryxInstrumenta gives conversational state somewhere to go — and a way to get it back.

This is a collection of standalone instruments built on open protocols for stateful AI communication. Each tool is independent, production-ready, and model-agnostic.

Licensed under Apache-2.0. See [LICENSE](./LICENSE).

---

## What We're Building

**STTP (Spatio-Temporal Transfer Protocol)** — a typed intermediate representation that encodes conversational state into compressed, confidence-weighted structures any model can reconstruct.

Not a summary. Not a transcript. A mathematical representation of a conversational state.

**Instrument 1: `sttp-mcp`** — an MCP server that lets models compress, store, and retrieve STTP nodes. The model calling the tools *is* the compression model. The server validates structure, persists nodes, and retrieves on resonance.

**Instrument 2: `acc` (Adaptive Codec Context)** — a dimensional code-intelligence instrument that indexes repositories into AVEC-space (stability, logic, friction, autonomy) so agents can query architecture, dependencies, and high-impact patterns without token-heavy raw dumps.

---

## Quick Start

KeryxInstrumenta currently ships two independent instruments:

- `sttp-mcp` for session memory persistence and transfer
- `acc` for dimensional repository indexing and agent-facing codebase queries

### Run with Docker (recommended)

This quick start is for `sttp-mcp`.

```bash
# Clone and build
git clone https://github.com/KeryxLabs/KeryxInstrumenta.git
cd KeryxInstrumenta/src/sttp-mcp
docker build -t sttp-mcp:local .

# Run over stdio
docker run --rm -i -v "$PWD/data:/data" sttp-mcp:local

#Run from global image
docker run --rm -i -v "$PWD/data:/data" ghcr.io/keryxlabs/sttp-mcp:0.1.2-beta
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
                "ghcr.io/keryxlabs/sttp-mcp:0.1.2-beta"
            ]
        }
    }
}
```
```json
{
  "servers": {
    "sttp-mcp": {
      "type": "stdio",
      "command": "docker",
      "args": [
        "run",
        "-i",
        "--rm",
        "ghcr.io/keryxlabs/sttp-mcp:0.1.2-beta",
        "--remote",
        "--remote-endpoint",
        "http://surreal_db_url:port",
        "--username",
        "user",
        "--password",
        "pass"
      ]
    }
  },
  "inputs": []
}

```

### Tools Available

- **`calibrate_session`** — measure current AVEC state, detect drift
- **`store_context`** — compress and persist a STTP node
- **`get_context`** — retrieve resonant nodes by attractor alignment
- **`list_nodes`** — explore stored memory, verify persistence
- **`get_moods`** — retrieve AVEC presets, apply state swaps

Full protocol docs: [sttp-mcp README](./src/sttp-mcp/README.md)

ACC docs: [ACC README](./src/acc/README.md)

### ACC Quick Start (local)

```bash
# From repo root
cd src/acc
dotnet build
dotnet run
```

ACC indexes repository entities into AVEC-space and exposes query patterns for relations, dependencies, and structural pattern matching. Recent ACC work includes adaptive LSP telemetry instrumentation across stream and metric services.

---

## How to Use STTP-MCP

Once the server is running and connected to your MCP client, here's the typical workflow:

### 1. Start a session and calibrate

Ask your model to calibrate at the start of any new session or after significant reasoning shifts:

```
"Can you calibrate this session?"
```

The model calls `calibrate_session` with:
- `sessionId` (e.g., "project-kickoff-2026-03-06")
- Current AVEC values (stability, friction, logic, autonomy)
- `trigger` (usually "manual")

The tool returns drift from the previous session state, so the model knows if it's continuing coherently or starting fresh.

### 2. Store important context

When you reach a meaningful checkpoint — completed analysis, design decision, key insight — have the model compress and store it:

```
"Store this conversation state for later retrieval."
```

The model compresses the current context into a STTP node and calls `store_context`. The server validates the structure and persists it with a unique node ID.

### 3. Retrieve context in a new session

Start a fresh chat, calibrate first, then pull relevant context:

```
"Retrieve context related to the project kickoff."
```

The model calls `get_context` with the session ID and its current AVEC state. The server returns the most resonant nodes based on attractor alignment — the model rehydrates directly from them.

### 4. Explore stored memory

Check what's in memory across all sessions:

```
"List all stored nodes."
```

Or filter by session:

```
"Show me nodes from the project-kickoff session."
```

The model calls `list_nodes` and presents the stored context with timestamps, sessions, and Ψ values.

### 5. Apply reasoning mode shifts

Need to switch reasoning posture? Pull AVEC mood presets:

```
"Show me available reasoning modes."
"Switch to defensive mode and recalibrate."
```

The model calls `get_moods`, presents options (focused, creative, defensive, analytical, etc.), applies the swap, then recalibrates to measure the shift.


### Cross-Model Continuity Demo

### Gemini 3 -->  Claude Sonnet 4.5 --> GPT4o --> GPT5-mini
[dwhatsapp-demo.webm](https://github.com/user-attachments/assets/9dc532f6-fecc-4df1-bf19-dc050b548b86)



## How to Use ACC

Use ACC when an agent needs compressed architectural perception of a codebase instead of raw file dumps.

### 1. Index and observe repository shape

Point ACC at your repository and language server so entities can be measured and persisted in the graph.

### 2. Query relations and dependencies

Use ACC relation/dependency queries to answer:

- What calls this node?
- What breaks if this changes?
- Where are the highest-friction chokepoints?

### 3. Query dimensional patterns

Use AVEC pattern matching to find similarly fragile, complex, or high-impact nodes for planning and refactoring.

### 4. Feed results back into your agent loop

Use ACC output as structured context for planning, review, and change impact analysis, then pair with `sttp-mcp` for long-horizon session continuity.


## Real time health analysis of codebase
### Track your changes as you code within grafana
<img width="1920" height="1080" alt="screenshot-2026-03-19_21-51-48" src="https://github.com/user-attachments/assets/51b3d2a2-35e8-4eec-a8c3-cb91a1900122" />


---

## Instruments

| Instrument | Status | Description |
|---|---|---|
| [sttp-mcp](./src/sttp-mcp) | `0.1.2-beta` | MCP server for STTP context persistence, retrieval, and session calibration |
| [acc](./src/acc) | `0.3.0` | Adaptive Codec Context: AVEC-based repository indexing, dependency analysis, and pattern queries for agents |

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


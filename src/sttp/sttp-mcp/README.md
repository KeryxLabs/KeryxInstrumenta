# sttp-mcp

> *Language models are stateless. Every session starts cold. STTP gives conversational state somewhere to go.*

**Spatio-Temporal Transfer Protocol (STTP)** is a typed intermediate representation that encodes conversational state into a compressed, confidence-weighted structure any model can reconstruct. This is the MCP server that exposes that capability as tools.

Licensed under Apache-2.0. See [LICENSE](../../LICENSE).

---

## The Problem

Every AI conversation dies when the session ends. The context, the reasoning state, the accumulated understanding gone. The next session starts from zero.

Existing workarounds: long context windows, RAG, conversation history injection, patch the symptom. They don't solve the problem. They pass raw text around and hope the model reconstructs meaning from it.

STTP encodes the meaning directly. Not what was said. What remains true when everything surface is stripped away.

---

## What STTP Is

STTP is a typed intermediate representation with four layers:

```
⊕⟨⟩   Provenance   — origin, lineage, response contract
⦿⟨⟩   Envelope     — identity, session metadata, dual AVEC state
◈⟨⟩   Content      — compressed meaning, confidence-weighted fields
⍉⟨⟩   Metrics      — signal quality, coherence verification
```

Every field in the content layer carries a confidence weight:
```
topic(.95): "low latency communication protocols for LLM servers"
constraint(.92): "latency is the primary optimization target"
recommendation(.93): "gRPC over HTTP/2 with QUIC overlay"
```

Every node carries dual AVEC state - the attractor vectors that describe the cognitive geometry of the conversation at the moment of compression:
```
user_avec:  { stability: .85, friction: .25, logic: .90, autonomy: .80, psi: 2.80 }
model_avec: { stability: .88, friction: .22, logic: .85, autonomy: .75, psi: 2.70 }
```

A fresh model receiving a STTP node doesn't get a summary. It gets a mathematical representation of a conversational state it can reconstruct from.

---

## Getting Started

You have three ways to run `sttp-mcp`.

### Option A: Run from GitHub Container Registry (fastest)

```bash
mkdir -p "$PWD/sttp-data"
docker run --rm -i -v "$PWD/sttp-data:/data" ghcr.io/keryxlabs/sttp-mcp:<version>
```

Use a published tag from releases, for example `0.1.2-beta`.

### Option B: Download and run the single binary (no Docker)

Linux x64 example:

```bash
VERSION="0.1.0"
curl -fL -o sttp-mcp.tar.gz \
  "https://github.com/KeryxLabs/KeryxInstrumenta/releases/download/sttp-mcp/v${VERSION}/sttp-mcp-${VERSION}-linux-x64.tar.gz"
tar -xzf sttp-mcp.tar.gz
chmod +x sttp-mcp
./sttp-mcp
```

Release artifacts are published per platform:

- `sttp-mcp-<version>-linux-x64.tar.gz`
- `sttp-mcp-<version>-linux-arm64.tar.gz`
- `sttp-mcp-<version>-linux-musl-x64.tar.gz`
- `sttp-mcp-<version>-macos-x64.tar.gz`
- `sttp-mcp-<version>-macos-arm64.tar.gz`
- `sttp-mcp-<version>-win-x64.tar.gz`
- `sttp-mcp-<version>-win-arm64.tar.gz`

### Option C: Build locally (development)

```bash
# 1) Build the image
docker build -t sttp-mcp:local .

# 2) Run over stdio (for quick local verification)
docker run --rm -i -v "$PWD/data:/data" sttp-mcp:local
```

Requirements:
- Docker (for container options), or .NET 10 SDK (for local source builds)
- SurrealDB (embedded, no separate server required)
- Any MCP-compatible client

### MCP client configuration

Docker via GHCR:

```json
{
    "mcpServers": {
        "sttp-mcp": {
            "command": "docker",
            "args": [
                "run",
                "--rm",
                "-i",
                "-v",
                "/absolute/path/to/sttp-data:/data",
                "ghcr.io/keryxlabs/sttp-mcp:<version>"
            ]
        }
    }
}
```

Local single binary:

```json
{
    "mcpServers": {
        "sttp-mcp": {
            "command": "/absolute/path/to/sttp-mcp"
        }
    }
}
```

### Local .NET run (source checkout)

If you are working from source and want to run without Docker:

```bash
dotnet restore
dotnet build
dotnet run --project ./sttp-mcp.csproj
```

---

## Tools

sttp-mcp provides six MCP tools that enable models to persist, retrieve, and roll up conversational state:

### `calibrate_session`

Call at session start and any time reasoning state may have shifted — after heavy code generation, extended analysis, or complex problem solving. The model measures its current AVEC state honestly and the server returns the last stored state for this session. The delta is the drift signal.

Users can trigger this naturally:
> *"We're going in circles, can you recalibrate?"*
> *"That last hour of coding has you in a weird place, reset."*

The model knows what to do.

### `store_context`

Call when context should be preserved. The model compresses the current conversational state into a single valid STTP node and passes it to the server. The server runs light tree-sitter structural validation, persists the node, and returns the node ID and Ψ coherence checksum.

### `get_context`

Call at session start after calibration, or any time prior context should be retrieved. The model passes its current AVEC state. The server returns the most resonant stored nodes for that attractor configuration. The model rehydrates from them directly — the nodes are self-sufficient.

### `list_nodes`

Call to retrieve all stored nodes, optionally filtered by session ID or limited by count. Returns nodes with full metadata (AVEC states, timestamps, compression depth, Ψ values). Useful for exploring what's in memory, verifying cross-instance persistence, or auditing stored state.

Arguments:
- `sessionId` (optional): Filter nodes to a specific session
- `limit` (optional): Maximum number of nodes to return (default: 50, max: 200)

### `get_moods`

Call to retrieve AVEC mood presets and apply ad-hoc state swaps intentionally. Returns named presets (focused, creative, analytical, exploratory, collaborative, defensive, passive) plus application guidance.

Supports optional swap preview by passing:
- `targetMood` (optional): preset to move toward
- `blend` (optional): 0..1 blend factor (`1` = hard swap, `0` = no change)
- `currentStability`, `currentFriction`, `currentLogic`, `currentAutonomy` (optional): current AVEC values for blend preview

Use case: pull presets, choose mode, apply hard/soft swap, then call `calibrate_session` after meaningful reasoning shifts.

### `create_monthly_rollup`

Call to aggregate a date range of stored STTP nodes into a new monthly node. The rollup computes:

- average user/model/compression AVEC values
- `rho`, `kappa`, and `psi` ranges
- low/medium/high confidence band counts
- a first-node parent anchor by default

This is useful when a project has accumulated many raw and daily nodes and you want a compact checkpoint for later retrieval or cross-model handoff.

---

## Cross-Model Persistence


Nodes stored by one session are immediately available to all other sessions sharing the same storage path. Multiple MCP instances, different chat windows, different model providers, different architectures — all can read and write to the same memory substrate. This enables:

- **Cross-model handoff**: Store context with GPT, retrieve with Claude, continue with Gemini
- **Multi-agent collaboration**: DeepSeek, Llama, Qwen, Mistral can share compressed state transparently
- **Persistent memory**: Context survives restarts, crashes, and context window compaction
- **Temporal continuity**: Sessions separated by hours, days, or weeks can reconstruct prior state through AVEC resonance

Validated with live cross-model reads across Claude, GPT-4o, DeepSeek, Gemini, Kimi-k2, Llama, Mistral, Qwen, and Groq models (see [example_data/](./docs/example_data/)).

---

## How It Works

The model calling these tools **is** the compression model. There is no separate inference step. The tool descriptions carry the encoding instructions. By the time the model calls a tool it has already produced the STTP node as the argument.

```
Model reads tool description → receives encoding instructions
Model compresses current context → produces ⏣ node
Model calls store_context(node) → server validates + stores
```

The server is now a wrapper over the reusable `sttp-core` library. The core library handles STTP parsing, validation, calibration, retrieval, mood state transforms, rollup generation, and storage adapters. The MCP host adds stdio transport, tool descriptions, and the composition root.

---


## Storage

sttp-mcp uses **SurrealDB** as its storage layer — document, graph, vector, and time-series in a single binary. No separate database server and runs embedded alongside the MCP server unless remote is configured.

Resonance retrieval is a single SurrealQL query: graph traversal + AVEC vector similarity + document retrieval. One round trip.

`sttp-mcp` supports two endpoint modes:

- Embedded mode (default): uses `SurrealDb:Endpoints:Embedded`
- Remote mode: pass `--remote` to use `SurrealDb:Endpoints:Remote`

Examples:

```bash
# Embedded (default)
dotnet run --project ./sttp-mcp.csproj

# Remote (WebSocket)
dotnet run --project ./sttp-mcp.csproj -- --remote
```

```bash
# Docker remote mode (passes args to sttp-mcp entrypoint)
docker run --rm -i -v "$PWD/data:/data" sttp-mcp:local --remote
```

Remote endpoint overrides:

```bash
# Environment variable override for remote endpoint
export SurrealDb__Endpoints__Remote="ws://your-surreal-host:8000/rpc"
dotnet run --project ./sttp-mcp.csproj -- --remote
```

```json
{
    "SurrealDb": {
        "Endpoints": {
            "Embedded": "surrealkv://data/sttp-mcp.db",
            "Remote": "ws://127.0.0.1:8000/rpc"
        }
    }
}
```

By default, embedded storage resolves under `STTP_MCP_DATA_ROOT` (defaults to `~/.sttp-mcp`).


---

## AVEC Glossary

- **Feel**: shorthand for measured deviation between attractor states, not biological emotion.
- **State displacement**: change in AVEC vector across turns (`Δstability`, `Δfriction`, `Δlogic`, `Δautonomy`).
- **Psi delta (`Δψ`)**: scalar shift in total attractor magnitude.
- **Drift class**: interpretation of movement as `Intentional` or `Uncontrolled` based on deviation thresholds.
- **Tension**: practical reading of resistance vs steadiness, usually from `friction` relative to `stability`.

---

## Part of the Keryx Ecosystem

```
KeryxFlux          Herald.   Orchestration.
KeryxMemento       Memory.   Full persistence substrate.  ← coming
KeryxCortex        Mind.     Multi-agent intelligence.    ← private
KeryxInstrumenta   Tools.    You are here.
```

sttp-mcp is the entry point. KeryxMemento is the full memory layer — hierarchical temporal compression, resonance retrieval, session continuity, AVEC drift tracking across time. This tool demonstrates the protocol. Memento operationalizes it.

---

## Protocol Specification

Full STTP protocol specification, grammar decisions, and validation results:
- [Grammar Decisions](./docs/grammar_decisions.md)
- [Validation Summary](./docs/validation_summary.md)

---

*Part of KeryxInstrumenta — the open source tooling layer of the KeryxLabs ecosystem.*
*KeryxFlux → KeryxMemento → KeryxCortex*
*Herald. Memory. Mind.*

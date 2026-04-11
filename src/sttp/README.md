# STTP - Spatio-Temporal Transfer Protocol

> Language models are stateless. Every session starts cold. STTP gives conversational state somewhere to go.

**STTP** is a typed intermediate representation that encodes conversational state into a compressed, confidence-weighted structure any model can reconstruct. Not a summary. Not a transcript. A mathematical representation of what remains true when everything surface-level is stripped away.

Licensed under Apache-2.0. See [LICENSE](../../LICENSE).

---

## The Problem

Every AI conversation dies when the session ends. The context, the reasoning state, the accumulated understanding — gone. The next session starts from zero.

Existing workarounds (long context windows, RAG, conversation history injection) patch the symptom. They pass raw text around and hope the model reconstructs meaning from it.

STTP encodes the meaning directly.

---

## What a Node Is

Every STTP node is a four-layer structure:

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

Every node carries dual AVEC state — the attractor vectors that describe the cognitive geometry of the conversation at the moment of compression:

```
user_avec:  { stability: .85, friction: .25, logic: .90, autonomy: .80, psi: 2.80 }
model_avec: { stability: .88, friction: .22, logic: .85, autonomy: .75, psi: 2.70 }
```

A fresh model receiving a STTP node doesn't get a summary. It gets a mathematical representation of a conversational state it can reconstruct from.

---

## Architecture

```
src/sttp/
├── sttp-core/          — C# core library: models, services, storage, sync-ready primitives
├── sttp-core-rs/       — Rust core library with the same STTP and sync-ready semantics
├── sttp-mcp/           — MCP server exposing STTP tools over stdio
├── sttp-gateway/       — C# deployable HTTP + gRPC host
├── sttp-gateway-rs/    — Rust deployable HTTP + gRPC host
├── sttp-ui/            — Blazor Server mobile console
├── sttp-mcp.Tests/     — integration test suite
├── sttp-demo/          — usage examples
└── docker-compose.yml  — gateway + ui stack
```

The C# and Rust cores are intended to stay behaviorally aligned. The transport hosts reuse those cores directly so parsing, storage, retrieval, and sync-related storage semantics stay consistent across transports.

---

## Components

### `sttp-core`

The reusable C# application layer. Contains:

- Domain models (nodes, AVEC state, tiers, envelopes)
- STTP node parser
- Core services: calibration, store, context retrieval, list, moods, monthly rollups
- Storage adapters: in-memory and SurrealDB (embedded SurrealKv)
- Sync-ready primitives: deterministic sync keys, idempotent upserts, incremental change queries, checkpoints, and a narrow coordinator surface for host apps

It is not a runnable host. It is the reusable layer consumed by `sttp-mcp` and `sttp-gateway`.

Important boundary: the core owns sync mechanics, not sync policy. That means the core is ready for cloud/local sync scenarios without forcing connector logic, conflict rules, or scheduling decisions on every app.

---

### `sttp-core-rs`

The reusable Rust application layer.

- Same STTP data model and storage semantics as the C# core
- In-memory and SurrealDB-backed storage
- The same sync-ready foundation: sync keys, checkpoints, typed connector metadata, and opt-in coordination hooks

This is the Rust building block for `sttp-gateway-rs` and any Rust-based STTP host or service.

---

### `sttp-mcp`

MCP server that exposes STTP as tools over stdio. Designed to run inside any MCP-compatible client (VS Code, Claude Desktop, Cursor, etc.).

For most users, this is the simplest entry point: store context, retrieve context, list nodes, calibrate sessions, and create rollups without thinking about cloud/local sync at all.

**Tools:**

| Tool | Purpose |
|------|---------|
| `calibrate_session` | Measure current AVEC state; receive drift from last stored node |
| `store_context` | Compress and persist the current conversational state as a node |
| `get_context` | Retrieve the most recent node for a session |
| `list_nodes` | List stored nodes, optionally filtered by session |
| `get_moods` | Return recent mood/AVEC profile for a session |
| `create_monthly_rollup` | Synthesize a tier-3 monthly node from daily nodes |

**Run via Docker:**

```bash
mkdir -p "$PWD/sttp-data"
docker run --rm -i \
  -v "$PWD/sttp-data:/data" \
  ghcr.io/keryxlabs/sttp-mcp:1.2.1
```

**MCP client config:**

```json
{
  "mcpServers": {
    "sttp-mcp": {
      "command": "docker",
      "args": [
        "run", "--rm", "-i",
        "-v", "/absolute/path/to/sttp-data:/data",
        "ghcr.io/keryxlabs/sttp-mcp:1.2.1"
      ]
    }
  }
}
```

**Binary releases** are published per platform:

```bash
VERSION="1.2.1"
curl -fL -o sttp-mcp.tar.gz \
  "https://github.com/KeryxLabs/KeryxInstrumenta/releases/download/sttp-mcp/v${VERSION}/sttp-mcp-${VERSION}-linux-x64.tar.gz"
tar -xzf sttp-mcp.tar.gz && chmod +x sttp-mcp
./sttp-mcp
```

Platforms: `linux-x64`, `linux-arm64`, `linux-musl-x64`, `macos-x64`, `macos-arm64`, `win-x64`, `win-arm64`

---

### `sttp-gateway`

Deployable network host with two transports in one process:

- **HTTP Minimal API** on `8080` — REST-style integration
- **gRPC (h2c)** on `8081` — typed low-latency service calls

Supports embedded SurrealKv storage (default) or remote SurrealDB.

Like `sttp-mcp`, the gateway is sync-ready at the storage layer but does not force a cloud/local sync strategy on callers. If you never add sync adapters, it continues to behave like a normal STTP host.

**Run locally:**

```bash
dotnet run --project sttp-gateway/sttp-gateway.csproj
```

**Run against remote SurrealDB:**

```bash
dotnet run --project sttp-gateway/sttp-gateway.csproj -- \
  --remote \
  --remote-endpoint "ws://10.12.0.11:9096/rpc" \
  --username root --password root \
  --database sttp_mcp
```

**Docker:**

```bash
docker run --rm \
  -p 8080:8080 \
  -p 8081:8081 \
  ghcr.io/keryxlabs/sttp-gateway:1.2.1 \
  --remote --remote-endpoint "ws://10.12.0.11:9096/rpc" \
  --username root --password root --database sttp_mcp
```

**HTTP endpoints:**

```
GET  /health
POST /api/v1/calibrate
POST /api/v1/store
POST /api/v1/context
GET  /api/v1/nodes?limit=50&sessionId=...
GET  /api/v1/graph?limit=...&sessionId=...
GET  /api/v1/moods?targetMood=focused&blend=1
POST /api/v1/rollups/monthly
```

**gRPC:** `sttp.v1.SttpGatewayService` — see `sttp-gateway/Protos/sttp.proto`

---

### `sttp-ui`

Blazor Server mobile console. Designed to run on a local server and be accessed from a phone over LAN or VPN.

- Session deck with swipe navigation (older/newer sessions and nodes)
- Graph view — Cytoscape.js session graph with similarity, lineage, membership, and timeline edges
- Compose modal — store new context nodes without leaving the session view
- Node detail with copy-to-clipboard raw STTP output

**Connects to `sttp-gateway`** via `Gateway__BaseUrl` environment variable.

**Run locally:**

```bash
dotnet run --project sttp-ui/sttp-ui.csproj
# set Gateway:BaseUrl in appsettings or env
```

**Docker:**

```bash
docker run --rm \
  -p 5000:8080 \
  -e Gateway__BaseUrl=http://gateway:8080 \
  ghcr.io/keryxlabs/sttp-ui:1.2.1
```

---

## Running the Full Stack

The included `docker-compose.yml` runs gateway and UI together:

```bash
# From src/sttp/
docker compose up -d
```

The gateway connects to a remote SurrealDB instance. The UI is available at `http://localhost:5257`.

## Sync-Ready, But Optional

Recent STTP releases added a sync-ready storage model across the C# and Rust cores:

- nodes can carry deterministic sync identity
- updates can be queried incrementally with cursors
- connectors can persist checkpoints
- provenance can be stored as typed connector metadata

Most users do not need to think about any of this.

If you are only using `sttp-mcp` or `sttp-gateway` for persistent conversational memory, everything still works the same way. Existing nodes remain readable, and the new schema fields are additive.

If you are building cloud/local sync later, the core libraries already provide the mechanical pieces. The application layer is still responsible for connector implementations, filtering, conflict policy, retry behavior, and scheduling.

To build both images locally first:

```bash
bash build-and-up.sh
```

---

## Building from Source

Each component has its own `build.sh` for multi-platform release packaging:

```bash
# build + package all platforms
bash sttp-mcp/build.sh

# build + package + upload to GitHub release
bash sttp-mcp/build.sh --publish
```

Same pattern applies to `sttp-gateway/build.sh` and `sttp-ui/build.sh`.

Docker image builds:

```bash
bash sttp-gateway/build-image.sh sttp-gateway:local
bash sttp-ui/build-image.sh sttp-ui:local
```

**Requirements:** .NET 10 SDK, Docker (for image builds), `gh` CLI (for `--publish`)

---

## Changelog

See [CHANGELOG.md](CHANGELOG.md) for release history across all STTP components.

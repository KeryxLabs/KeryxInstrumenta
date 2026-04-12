# KeryxInstrumenta
<img width="892" height="848" alt="image" src="https://github.com/user-attachments/assets/185d4675-a7cc-4784-bec1-136d79f61df2" />

> Protocol-first infrastructure for persistent AI memory and adaptive code intelligence.

Language models are stateless by default, and codebases are harder to understand than they should be. KeryxInstrumenta is a collection of open instruments built to address both problems without hiding the machinery behind vague magic.

Some parts of this repository help conversations survive across sessions and models. Other parts help developers and agents reason about real codebases as living systems.

Licensed under Apache-2.0. See [LICENSE](LICENSE).

## What This Repo Contains

KeryxInstrumenta currently ships two independent instruments:

- [`sttp`](./src/sttp): Spatio-Temporal Transfer Protocol, a full stack for persistent AI memory.
- [`acc`](./src/acc): Adaptive Codec Context, a dimensional code-intelligence system for repository indexing and analysis.

Each instrument is standalone and can be used independently.

## Instrument Overview

| Instrument | Current Line | What It Does |
| --- | --- | --- |
| [`sttp`](./src/sttp) | `1.2.x` | Persistent AI memory across MCP, network services, shared cores, and UI |
| [`acc`](./src/acc) | `0.3.x` | Turns codebases into a dimensional graph you can query for risk, structure, and patterns |

## Start Here

If you only read one extra document, read the one for the instrument you care about most:

- Want the STTP overview: [src/sttp/README.md](./src/sttp/README.md)
- Want STTP inside an AI assistant through MCP: [src/sttp/sttp-mcp/README.md](./src/sttp/sttp-mcp/README.md)
- Want STTP as an HTTP/gRPC service: [src/sttp/sttp-gateway/README.md](./src/sttp/sttp-gateway/README.md)
- Want the Rust STTP core: [src/sttp/sttp-core-rs/README.md](./src/sttp/sttp-core-rs/README.md)
- Want the ACC overview: [src/acc/README.md](./src/acc/README.md)
- Want ACC in VS Code: [src/acc/acc-vscode/README.md](./src/acc/acc-vscode/README.md)
- Want ACC in Neovim: [src/acc/acc-nvim/README.md](./src/acc/acc-nvim/README.md)
- Want ACC via CLI or MCP: [src/acc/acc-cli/AccCli/README.md](./src/acc/acc-cli/AccCli/README.md) and [src/acc/acc-mcp/AccMcpServer/README.md](./src/acc/acc-mcp/AccMcpServer/README.md)

This README is meant to orient you, not exhaust you.

## STTP At A Glance

STTP is a memory substrate for AI conversations.

The short version:

- it lets models store compressed state instead of raw chat logs
- it lets later sessions retrieve that state by relevance to the current reasoning posture
- it works across transports: MCP, HTTP, gRPC, UI, and shared libraries
- it is now sync-ready at the storage layer, but sync is still optional

If you are curious but not ready to dive deep, the main thing to understand is this: STTP is trying to preserve what remains true about a conversation, not just what was said.

### STTP Components

| Component | What It Is |
| --- | --- |
| [src/sttp/sttp-core](./src/sttp/sttp-core) | Reusable C# core library for parsing, storage, retrieval, rollups, and sync-ready primitives |
| [src/sttp/sttp-core-rs](./src/sttp/sttp-core-rs) | Reusable Rust core library with the same STTP and sync-ready semantics |
| [src/sttp/sttp-mcp](./src/sttp/sttp-mcp) | MCP server for AI clients that want memory over stdio |
| [src/sttp/sttp-gateway](./src/sttp/sttp-gateway) | Deployable C# HTTP + gRPC host |
| [src/sttp/sttp-gateway-rs](./src/sttp/sttp-gateway-rs) | Deployable Rust HTTP + gRPC host |
| [src/sttp/sttp-ui](./src/sttp/sttp-ui) | Mobile-friendly browser UI for browsing sessions and nodes |

### STTP Boundary

One architectural line matters a lot:

- the STTP cores own storage and sync mechanics
- the host application owns sync policy

That means the cores can handle things like deterministic node identity, incremental changes, checkpoints, and typed provenance metadata without forcing every consumer to adopt cloud/local sync, conflict resolution rules, or connector logic.

If you never need synchronization, STTP still behaves like a normal persistent memory stack.

## ACC At A Glance

ACC is a code-intelligence system built around a dimensional graph model.

The short version:

- it indexes code entities and relationships into a structured graph
- it measures code along four AVEC dimensions: stability, logic, friction, and autonomy
- it exposes that graph through editor integrations, CLI tools, MCP, and dashboards
- it is built to help people and tools reason about codebases instead of treating them like opaque blobs

ACC is useful when you want answers like:

- what parts of this codebase are risky to touch?
- what depends on this symbol?
- where is complexity concentrated?
- which nodes look structurally similar?

### ACC Surfaces

| Surface | What It Is |
| --- | --- |
| [src/acc/acc-vscode](./src/acc/acc-vscode) | VS Code extension for graph workflows inside the editor |
| [src/acc/acc-nvim](./src/acc/acc-nvim) | Neovim plugin for terminal-native workflows |
| [src/acc/acc-cli](./src/acc/acc-cli) | CLI for scripts, automation, and direct querying |
| [src/acc/acc-mcp](./src/acc/acc-mcp) | MCP server for agent integrations |

## If You Want To Try Something Today

You do not need to understand the whole repository before getting value from it.

### Option 1: Use STTP through MCP

If you want persistent conversational memory inside an MCP-capable AI client, start with [src/sttp/sttp-mcp/README.md](./src/sttp/sttp-mcp/README.md).

Fast path:

```bash
docker run --rm -i -v "$PWD/sttp-data:/data" ghcr.io/keryxlabs/sttp-mcp:1.2.1
```

### Option 2: Run the STTP stack in a browser

If you want a network host and UI, start with [src/sttp/README.md](./src/sttp/README.md).

Fast path:

```bash
cd src/sttp
docker compose up
```

That brings up the gateway and UI together.

### Option 3: Explore ACC for code intelligence

If you want repository analysis, start with [src/acc/README.md](./src/acc/README.md).

That doc will point you to the best entry path depending on whether you want VS Code, Neovim, CLI, or MCP.

## Quick Start

### 1. Clone the repository

```bash
git clone https://github.com/KeryxLabs/KeryxInstrumenta.git
cd KeryxInstrumenta
```

### 2. Choose an instrument

- For persistent AI memory: go to [src/sttp/README.md](./src/sttp/README.md)
- For code intelligence: go to [src/acc/README.md](./src/acc/README.md)

### 3. Use the fast path if you want to test quickly

STTP full stack:

```bash
cd src/sttp
docker compose up
```

STTP MCP only:

```bash
docker run --rm -i -v "$PWD/sttp-data:/data" ghcr.io/keryxlabs/sttp-mcp:1.2.1
```

For anything deeper than that, the component readmes are the better source of truth.

## Release Tags And Artifacts

KeryxInstrumenta uses namespaced release tags per component so each stream can evolve independently.

- `sttp-mcp/v...`
- `sttp-gateway/v...`
- `sttp-ui/v...`
- `acc-engine/v...`
- `acc-cli/v...`
- `acc-mcp/v...`
- `acc-vscode/v...`

Published examples:

- STTP images are published on GHCR
- ACC binaries and VS Code extension are published through GitHub Releases

## How People Usually Use STTP

There are three common paths:

### Through an MCP client

Use [src/sttp/sttp-mcp/README.md](./src/sttp/sttp-mcp/README.md) when you want an assistant to:

- calibrate its current reasoning state
- store context checkpoints
- retrieve prior context later
- list memory nodes and create rollups

### Through a network API

Use [src/sttp/sttp-gateway/README.md](./src/sttp/sttp-gateway/README.md) or [src/sttp/sttp-gateway-rs/README.md](./src/sttp/sttp-gateway-rs/README.md) when you want STTP as a service behind HTTP or gRPC.

### Through a browser UI

Use [src/sttp/sttp-ui/README.md](./src/sttp/sttp-ui/README.md) when you want a session browser and node viewer on top of the gateway.

## How People Usually Use ACC

ACC has four main usage surfaces:

- VS Code for in-editor exploration
- Neovim for terminal-native workflows
- CLI for scripting and automation
- MCP for agent-facing queries

Start at [src/acc/README.md](./src/acc/README.md), then branch into the surface you care about.

## Demo

### STTP-UI 
**Track your sessions on the go:**
<img width="1867" height="982" alt="image" src="https://github.com/user-attachments/assets/92309f55-1f4b-4272-8c67-d8a3fc5106fa" />

**Visualize your thoughts as a spider web**
<img width="1867" height="990" alt="image" src="https://github.com/user-attachments/assets/079cb12c-467f-4880-a9c0-79d540181eeb" />

**Add nodes on the go**
<img width="772" height="886" alt="image" src="https://github.com/user-attachments/assets/898515dc-9bca-4afc-9d75-d770fddefa2e" />

**Get a summary of where your mind was during that moment**
<img width="1299" height="885" alt="image" src="https://github.com/user-attachments/assets/3b54d22d-0e75-430a-9e68-fd60d74a10c1" />


Real-time ACC health tracking (Grafana):

<img width="1920" height="1080" alt="ACC Grafana Dashboard" src="https://github.com/user-attachments/assets/51b3d2a2-35e8-4eec-a8c3-cb91a1900122" />

## Why The Repo Looks This Way

KeryxInstrumenta is organized around instruments rather than one monolithic platform.

- STTP is about persistent conversational state.
- ACC is about queryable code understanding.
- They share some conceptual language, especially AVEC, but they are not forced into one deployment or one workflow.

That separation is deliberate. You should be able to adopt one without buying into the whole universe.

## Philosophy

- Protocol first: contracts are explicit and reusable.
- Model agnostic: the instruments are not tied to one provider or one interface.
- Infrastructure, not ideology: the goal is to give people and tools better substrate, not to bury decisions behind hype.

## Keryx Ecosystem

```text
KeryxFlux          Herald.   Orchestration layer.
KeryxMemento       Memory.   Persistence substrate.
KeryxCortex        Mind.     Multi-agent intelligence.
KeryxInstrumenta   Tools.    You are here.
```

## AVEC Glossary

AVEC is the shared dimensional vocabulary used across STTP and ACC.

- Stability: steadiness vs volatility
- Friction: resistance vs flow
- Logic: structured reasoning vs intuitive association
- Autonomy: self-directed vs guided execution
- Psi (`Psi`): a scalar derived from the vector, often used as a compact signal summary
- Drift class: interpretation of movement between attractor states

You do not need to master AVEC to use the tools, but it is the language the instruments use to talk about state in a compact way.

## Contributing And Release History

Contributions are welcome across instruments, adapters, docs, and integrations.

- Contributing guide: [CONTRIBUTING.md](./CONTRIBUTING.md)
- STTP changelog: [src/sttp/CHANGELOG.md](./src/sttp/CHANGELOG.md)
- Component-level docs live beside the code for each instrument

Part of the KeryxLabs ecosystem.

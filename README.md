# KeryxInstrumenta

> Protocol-first infrastructure for persistent AI memory and adaptive code intelligence.

Language models are stateless by default. KeryxInstrumenta provides open, production-oriented instruments that make state portable and code understanding queryable.

Licensed under Apache-2.0. See `LICENSE`.

## What This Repo Contains

KeryxInstrumenta currently ships two independent instruments:

- `sttp-mcp`: MCP server for STTP (Spatio-Temporal Transfer Protocol) memory persistence and retrieval.
- `acc`: Adaptive Codec Context, a dimensional code-intelligence system for repository indexing and analysis.

Each instrument is standalone and can be used independently.

## Instrument Overview

| Instrument | Current Line | What It Does |
|---|---|---|
| [`sttp-mcp`](./src/sttp-mcp) | `1.0.0` | Stores and retrieves compressed conversational state as STTP nodes via MCP tools |
| [`acc`](./src/acc) | `0.3.x` | Indexes repositories into AVEC-space and exposes dependency/risk/pattern queries |

## Quick Start

### 1. Clone

```bash
git clone https://github.com/KeryxLabs/KeryxInstrumenta.git
cd KeryxInstrumenta
```

### 2. STTP-MCP (Docker)

```bash
cd src/sttp-mcp

docker build -t sttp-mcp:local .
docker run --rm -i -v "$PWD/data:/data" sttp-mcp:local
```

Minimal MCP client config:

```json
{
  "servers": {
    "sttp-mcp": {
      "type": "stdio",
      "command": "docker",
      "args": [
        "run",
        "--rm",
        "-i",
        "-v",
        "/absolute/path/to/sttp-data:/data",
        "sttp-mcp:local"
      ]
    }
  }
}
```

### 3. ACC

For full ACC setup and per-tool instructions:

- [`src/acc/README.md`](./src/acc/README.md)

## Release Tags

KeryxInstrumenta uses namespaced release tags per tool so each artifact stream is independent:

- `sttp-mcp/v...`
- `acc-engine/v...`
- `acc-cli/v...`
- `acc-mcp/v...`
- `acc-vscode/v...`

## Install from GitHub Releases

### STTP-MCP

Published images are available in GHCR (example):

```bash
docker run --rm -i -v "$PWD/data:/data" ghcr.io/keryxlabs/sttp-mcp:0.1.2-beta
```

### ACC Engine/CLI/MCP Binaries (example: linux-x64)

```bash
# ACC engine
curl -fsSL \
  https://github.com/KeryxLabs/KeryxInstrumenta/releases/download/acc-engine/v0.3.1/acc-0.3.1-linux-x64.tar.gz \
  | tar -xz

# ACC CLI
curl -fsSL \
  https://github.com/KeryxLabs/KeryxInstrumenta/releases/download/acc-cli/v0.1.0/acc-cli-0.1.0-linux-x64.tar.gz \
  | tar -xz

# ACC MCP server
curl -fsSL \
  https://github.com/KeryxLabs/KeryxInstrumenta/releases/download/acc-mcp/v0.1.0/acc-mcp-0.1.0-linux-x64.tar.gz \
  | tar -xz
```

### ACC VS Code Extension

Install from a release `.vsix`:

```bash
code --install-extension acc-vscode-0.3.1.vsix
```

## How To Use STTP-MCP

Typical flow in an MCP client session:

1. Calibrate at session start with `calibrate_session`.
2. Persist key milestones with `store_context`.
3. Rehydrate in new sessions with `get_context`.
4. Inspect stored state with `list_nodes`.
5. Shift reasoning posture with `get_moods`, then recalibrate.

Primary docs:

- [`src/sttp-mcp/README.md`](./src/sttp-mcp/README.md)

## How To Use ACC

ACC exposes four interaction surfaces:

- VS Code extension (`acc-vscode`)
- Neovim plugin (`acc.nvim`)
- CLI (`acc-cli`)
- MCP server (`acc-mcp`)

Typical ACC loop:

1. Build or update the dependency graph.
2. Query risk (`high friction`, `unstable`) and dependencies.
3. Run pattern searches in AVEC-space.
4. Feed structured results into planning/review loops.

Primary docs:

- [`src/acc/README.md`](./src/acc/README.md)
- [`src/acc/acc-vscode/README.md`](./src/acc/acc-vscode/README.md)
- [`src/acc/acc-nvim/README.md`](./src/acc/acc-nvim/README.md)
- [`src/acc/acc-cli/AccCli/README.md`](./src/acc/acc-cli/AccCli/README.md)
- [`src/acc/acc-mcp/AccMcpServer/README.md`](./src/acc/acc-mcp/AccMcpServer/README.md)

## Demo

Cross-model continuity demo:

- [dwhatsapp-demo.webm](https://github.com/user-attachments/assets/9dc532f6-fecc-4df1-bf19-dc050b548b86)

Real-time ACC health tracking (Grafana):

<img width="1920" height="1080" alt="ACC Grafana Dashboard" src="https://github.com/user-attachments/assets/51b3d2a2-35e8-4eec-a8c3-cb91a1900122" />

## Philosophy

- Protocol first: contracts are open and documented.
- Model agnostic: tools are provider- and model-independent.
- Infrastructure, not opinion: instruments provide substrate, not ideology.

## Keryx Ecosystem

```text
KeryxFlux          Herald.   Orchestration layer.
KeryxMemento       Memory.   Persistence substrate.
KeryxCortex        Mind.     Multi-agent intelligence.
KeryxInstrumenta   Tools.    You are here.
```

## AVEC Glossary

AVEC (Attractor Vector Encoding Configuration) is the state vector used across STTP/ACC dimensions:

- Stability: baseline steadiness vs volatility
- Friction: resistance vs flow
- Logic: structured reasoning vs intuitive association
- Autonomy: self-directed vs guided execution
- Psi (`Psi`): vector magnitude `sqrt(stability^2 + friction^2 + logic^2 + autonomy^2)`
- Drift class: interpretation of attractor movement (`Intentional` vs `Uncontrolled`)

## Contributing

Contributions are welcome across instruments, adapters, and docs.

- See [`CONTRIBUTING.md`](./CONTRIBUTING.md)
- See [`CHANGELOG.md`](./CHANGELOG.md)

Part of the KeryxLabs ecosystem.

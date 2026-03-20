# ACC - Adaptive Codec Context

> A continous dimensional indexing system for codebases that provides agents with compressed, queryable perception of code environments.


## What is ACC?

ACC (Adaptive Codec Context) transforms codebases into a **dimensional semantic graph** where every code entity (class, function, module) is measured across four dimensions:

- **Stability** - How often does this break? (git churn + contributors + test coverage)
- **Logic** - How much reasoning is packed here? (cyclomatic complexity + LOC)
- **Friction** - How many things depend on this? (incoming edges + centrality)
- **Autonomy** - How self-directed is this code? (outgoing dependencies inverse)

These dimensions form **AVEC** (Attractor Vector Encoding Configuration) - coordinates in a 4D space that describe a node's architectural characteristics.

## Grafana Dashboard - Real Time Tracking Of Codebase Health
<img width="1884" height="1005" alt="image" src="https://github.com/user-attachments/assets/91705932-94bf-44ab-8a55-6bbf95c5d877" />

## Why ACC?

Current agent tools dump raw filesystem content and LSP output - a firehose of tokens that drowns small models and wastes context for large ones. ACC provides:

✅ **Compressed perception** - Dimensional profiles instead of raw code  
✅ **Queryable structure** - Graph traversal, pattern matching, impact analysis  
✅ **Agent learning** - Runtime AVEC updates based on interaction outcomes  
✅ **Language agnostic** - Works with any LSP + git repo  
✅ **Local-first** - Embedded SurrealDB, no cloud dependency  

## Architecture
```
┌─────────────────────────────────────────────────────────────┐
│                         ACC Core                            │
│                                                             │
│  ┌──────────┐    ┌──────────┐    ┌─────────────────────┐    │
│  │   LSP    │───▶│  Metrics │───▶│   SurrealDB Graph   │    │
│  │  Watcher │    │Collector │    │  (nodes + edges)    │    │
│  └──────────┘    └──────────┘    └─────────────────────┘    │
│                       │                    │                │
│  ┌──────────┐         │                    │                │
│  │   Git    │─────────┘                    │                │
│  │  Watcher │                              │                │
│  └──────────┘                              │                │
│                                            │                │
│  ┌──────────┐                              │                │
│  │  Lizard  │──────────────────────────────┘                │
│  │ Analyzer │                                               │
│  └──────────┘                                               │
│                                                             │
│                    ┌─────────────┐                          │
│                    │ AVEC Formula│                          │
│                    │  Calculator │                          │ 
│                    └─────────────┘                          │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
                    ┌──────────────────┐
                    │    Query SDK     │
                    │  (FFI/stdio)     │
                    └──────────────────┘
                              │
              ┌───────────────┼───────────────┐
              ▼               ▼               ▼
        ┌─────────┐   ┌─────────┐   ┌─────────┐
        │ Neovim  │   │ VSCode  │   │ Agents  │
        │ Plugin  │   │Extension│   │         │
        └─────────┘   └─────────┘   └─────────┘
```

## Core Queries

ACC exposes three fundamental query patterns:

### 1. Relations
"Show me this node and its immediate connections"
```csharp
QueryRelations(nodeId: "UserService.cs:AuthenticateAsync:23")
// Returns: node + incoming/outgoing edges
```

### 2. Dependencies
"What breaks if I change this?" (impact analysis)
```csharp
QueryDependencies(
    nodeId: "CacheManager.cs:InvalidateAll:15",
    direction: Incoming,  // what calls this?
    depth: 3
)
// Returns: transitive dependency graph
```

### 3. Patterns
"Find nodes with similar dimensional profiles"
```csharp
QueryPatterns(
    avec: { stability: 0.3, logic: 0.2, friction: 0.8, autonomy: 0.3 },
    threshold: 0.8
)
// Returns: nodes clustered in 4D space
// (e.g., find other fragile, high-friction chokepoints)
```

## AVEC Scoring

Nodes are scored formulaically (not via ML) using observable metrics:
```csharp
stability = f(git_churn, contributors, test_coverage)
logic = f(cyclomatic_complexity, LOC, parameters)
friction = f(incoming_edges, centrality)
autonomy = f(outgoing_edges_inverse)
```

Scores are **configurable** via weights in `appsettings.json`:
```json
{
  "AvecWeights": {
    "Stability": {
      "ChurnWeight": 0.4,
      "ContributorWeight": 0.3,
      "TestWeight": 0.3
    },
    "Logic": {
      "ComplexityWeight": 0.7,
      "ParameterWeight": 0.3
    }
  }
}
```

Different project types get different defaults:
- **Existing legacy system**: Heavily penalize churn, prioritize stability
- **New greenfield project**: Favor autonomy, expect low test coverage initially

## Installation

### Prerequisites

- .NET 10.0 SDK
- SurrealDB (`brew install surrealdb/tap/surreal` or [download](https://surrealdb.com/install))
- Lizard (`pip install lizard`) — **installed automatically by the VSCode extension if not present**
- LSP for your language (e.g., OmniSharp for C#)

### Build
```bash
# Clone
git clone https://github.com/KeryxLabs/KeryxInstrumenta.git
cd acc

# Build
dotnet build

# Or publish as AOT binary
dotnet publish -c Release -r linux-x64
```

### Run
```bash
# Start SurrealDB
surreal start --log debug --user root --pass root file:///tmp/acc.db

# Configure ACC
cat > appsettings.json <<EOF
{
  "Acc": {
    "RepositoryPath": "/path/to/your/repo",
    "SurrealDbConnection": "ws://localhost:8000",
    "Language": "csharp"
  }
}
EOF

# Run ACC
dotnet run

# Or use the AOT binary
./bin/Release/net8.0/linux-x64/publish/ACC
```

## Configuration

### appsettings.json
```json
{
  "Acc": {
    "RepositoryPath": "/absolute/path/to/repo",
    "SurrealDbConnection": "ws://localhost:8000",
    "Language": "csharp",
    "Target": {
      "Stability": 0.95,
      "Logic": 0.81,
      "Friction": 0.19,
      "Autonomy": 0.99
    }
  },
  "AvecWeights": {
    "Stability": {
      "ChurnWeight": 0.4,
      "ContributorWeight": 0.3,
      "TestWeight": 0.3,
      "ChurnNormalize": 10,
      "ContributorCap": 5
    },
    "Logic": {
      "ComplexityWeight": 0.7,
      "ParameterWeight": 0.3,
      "LocDivisor": 10,
      "ParameterCap": 5
    },
    "Friction": {
      "CentralityWeight": 0.6,
      "DependencyWeight": 0.4,
      "IncomingCap": 10
    }
  }
}
```

### Environment-specific configs

Create `appsettings.Development.json` or `appsettings.Production.json` to override defaults:
```json
{
  "Acc": {
    "Target": {
      "Stability": 0.85,
      "Friction": 0.60
    }
  }
}
```

## Supported Languages

ACC works with any language that has an LSP server:

| Language   | LSP Server              | Install                          |
|------------|-------------------------|----------------------------------|
| C#         | OmniSharp               | `brew install omnisharp`         |
| TypeScript | typescript-language-server | `npm i -g typescript-language-server` |
| Python     | pylsp                   | `pip install python-lsp-server`  |
| Go         | gopls                   | `go install golang.org/x/tools/gopls@latest` |
| Rust       | rust-analyzer           | `rustup component add rust-analyzer` |

## Query Examples

### Find fragile infrastructure
```csharp
// High friction + low stability = risky chokepoints
QueryPatterns(
    avec: { stability: 0.3, friction: 0.8 },
    threshold: 0.7
)
```

### Impact analysis before refactoring
```csharp
// What depends on this authentication method?
QueryDependencies(
    nodeId: "AuthService.cs:Authenticate:45",
    direction: Incoming,
    depth: -1  // unlimited
)
```

### Find similar implementations
```csharp
// Show me other validation logic like this one
var targetNode = GetNode("OrderValidator.cs:Validate:12");
QueryPatterns(avec: targetNode.Avec, threshold: 0.85)
```

## Agent Integration

Agents can query ACC for dimensional context and **update** AVEC scores based on runtime learning:
```csharp
// Query
var node = QueryRelations("UserService.cs:Login:34");
Console.WriteLine($"Friction: {node.Avec.Friction}");

// Agent learns this node is actually more stable than metrics suggest
UpdateLearnedAvec(
    nodeId: "UserService.cs:Login:34",
    avec: { stability: 0.95, logic: 0.8, friction: 0.4, autonomy: 0.85 }
);

// Divergence becomes visible
var updated = GetNode("UserService.cs:Login:34");
Console.WriteLine($"Delta: {updated.AvecDelta.Stability}");
// Output: +0.20 (learned is higher than computed)
```

## Roadmap

- [x] Core indexing (LSP + Git + Lizard)
- [x] AVEC formula calculation
- [x] SurrealDB graph storage
- [x] Three core query types
- [x] **VSCode extension** - auto-downloads ACC server binary; auto-installs `lizard` via pip if not present
- [ ] **Neovim plugin (Cognoscere)** - in progress
- [ ] HTTP/gRPC query API
- [ ] MCP server adapter
- [ ] Language-specific complexity analyzers (beyond Lizard)
- [ ] Real-time change propagation (WebSocket events)

## Part of KeryxLabs Ecosystem

ACC is the perception layer for the broader KeryxLabs infrastructure:

- **KeryxFlux (Herald)** - Data/message orchestration
- **KeryxMemento (Memory)** - Persistence substrate
- **KeryxCortex (Mind)** - Intelligence layer (private)
- **KeryxInstrumenta** - Tool suite:
  - **STTP** - Spatio-Temporal Transfer Protocol (context persistence)
  - **RCP** - Reasoning Construction Protocol (reasoning graphs)
  - **ACC** - Adaptive Codec Context (this project)
  - **Symphonia** - MCP orchestration (coming)
- **Cognoscere** - Neovim plugin (builds on ACC + STTP)

## License

MIT License - see [LICENSE](LICENSE)

## Contributing

Contributions welcome! ACC is designed to be extensible:

- Add language-specific analyzers
- Implement additional query patterns
- Tune AVEC formulas for specific domains
- Build editor integrations

See [CONTRIBUTING.md](CONTRIBUTING.md) for guidelines.

## Philosophy

> "Real people want AI to power them, not replace them."

ACC provides **exoskeleton architecture** - amplifying developer ability to reason about complex systems rather than automating them away. The codebase becomes queryable, navigable, and measurable through dimensional lenses that compress complexity without losing signal.

---

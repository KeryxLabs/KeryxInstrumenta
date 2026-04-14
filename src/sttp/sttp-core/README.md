# Sttp.Core

Sttp.Core is the reusable .NET application layer for STTP (Spatio-Temporal Transfer Protocol).

It provides the shared services and storage contracts used by STTP hosts such as sttp-mcp and sttp-gateway.

## Included Capabilities

- STTP domain models and parser/validation pipeline.
- Application services for:
  - calibration
  - context retrieval
  - context storage
  - mood presets and AVEC blending
  - monthly rollup generation
  - batch rekey support
  - sync coordinator plumbing
- Storage adapters for in-memory and SurrealDB-backed persistence.
- Sync-ready primitives: deterministic sync keys, incremental change queries, checkpoints, and typed connector metadata.

## Installation

```bash
dotnet add package Sttp.Core
```

## Basic Registration

```csharp
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();

services.AddSttpCore();
services.AddSttpSurrealDbStorage(configuration, args, ".sttp-mcp");
```

## Notes

- The core library is sync-ready, not sync-opinionated.
- Existing usage remains straightforward for standard store/query flows.
- Hosts decide sync policy, scheduling, and conflict behavior.

## Repository

- Source: https://github.com/KeryxLabs/KeryxInstrumenta
- Project path: src/sttp/sttp-core
- License: Apache-2.0

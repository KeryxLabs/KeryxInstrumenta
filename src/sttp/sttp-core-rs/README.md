# sttp-core-rs

Core Rust library for STTP domain modeling, validation, parsing, and storage-backed services.

This crate is designed to be embedded in apps, MCP servers, and gateways that need to store and retrieve STTP nodes with AVEC-aware semantics.

## What You Get

- Domain contracts and models for STTP and AVEC.
- Application services:
  - calibration,
  - context retrieval,
  - context storage,
  - mood catalog and blend preview,
  - monthly rollup generation,
  - batch scope rekey.
- Storage implementations:
  - in-memory store,
  - SurrealDB-backed store.
- Parser and validator primitives for raw STTP node text.

## Installation

```toml
[dependencies]
sttp-core-rs = "0.1"
```

## Quick Start

```rust,no_run
use std::sync::Arc;

use sttp_core_rs::{
    CalibrationService, ContextQueryService, InMemoryNodeStore, MoodCatalogService,
    NodeStoreInitializer,
};

# fn main() -> anyhow::Result<()> {
let runtime = tokio::runtime::Builder::new_current_thread()
    .enable_all()
    .build()?;

runtime.block_on(async {
    let store = Arc::new(InMemoryNodeStore::new());
    let initializer: Arc<dyn NodeStoreInitializer> = store.clone();
    initializer.initialize_async().await?;

    let calibration = CalibrationService::new(store.clone());
    let context = ContextQueryService::new(store.clone());
    let moods = MoodCatalogService::new();

    let calibration_result = calibration
        .calibrate_async("session-1", 0.9, 0.2, 0.9, 0.85, "manual")
        .await?;

    let retrieved = context
        .get_context_async("session-1", 0.9, 0.2, 0.9, 0.85, 5)
        .await;

    let catalog = moods.get(Some("focused"), 1.0, Some(0.8), Some(0.2), Some(0.8), Some(0.8));

    println!("delta={} retrieved={} presets={}", calibration_result.delta, retrieved.retrieved, catalog.presets.len());

    Ok::<(), anyhow::Error>(())
})?;
# Ok(())
# }
```

## Public API Surface

- Services: `CalibrationService`, `ContextQueryService`, `StoreContextService`, `MoodCatalogService`, `MonthlyRollupService`, `RekeyScopeService`.
- Validation: `TreeSitterValidator`.
- Storage: `InMemoryNodeStore`, `SurrealDbNodeStore`, `SurrealDbRuntimeOptions`, `SurrealDbSettings`.
- Contracts: `NodeStore`, `NodeStoreInitializer`, `NodeValidator`.
- Core models: `SttpNode`, `AvecState`, `NodeQuery`, `MonthlyRollupRequest`, `BatchRekeyResult`.

## Build And Test

```bash
cargo check --manifest-path src/sttp/sttp-core-rs/Cargo.toml
cargo test --manifest-path src/sttp/sttp-core-rs/Cargo.toml
```

## Build Package Artifact

Create a local `.crate` artifact for verification:

```bash
cargo package --manifest-path src/sttp/sttp-core-rs/Cargo.toml --allow-dirty
```

Inspect package contents:

```bash
cargo package --manifest-path src/sttp/sttp-core-rs/Cargo.toml --allow-dirty --list
```

The artifact is written under `src/sttp/sttp-core-rs/target/package`.

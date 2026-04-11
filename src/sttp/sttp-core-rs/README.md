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

## First Publish To crates.io (Step-By-Step)

If this is your first crates.io release, use this sequence.

### 1. Verify Name Availability

```bash
cargo search sttp-core-rs --limit 5
```

If the exact crate name is already taken, update `[package].name` in `Cargo.toml`.

### 2. Create crates.io Token

1. Sign in at crates.io.
2. Go to Account Settings -> API Tokens.
3. Create a token with publish scope.

Login locally:

```bash
cargo login <YOUR_CRATES_IO_TOKEN>
```

Alternative for CI/non-interactive use:

```bash
export CARGO_REGISTRY_TOKEN=<YOUR_CRATES_IO_TOKEN>
```

### 3. Release Readiness Checks

From repository root:

```bash
cargo check --manifest-path src/sttp/sttp-core-rs/Cargo.toml
cargo test --manifest-path src/sttp/sttp-core-rs/Cargo.toml
```

Check exact package contents:

```bash
cargo package --manifest-path src/sttp/sttp-core-rs/Cargo.toml --list
```

### 4. Dry-Run Publish (Required)

```bash
cargo publish --manifest-path src/sttp/sttp-core-rs/Cargo.toml --dry-run
```

### 5. Publish

```bash
cargo publish --manifest-path src/sttp/sttp-core-rs/Cargo.toml
```

### 6. Post-Publish Verification

```bash
cargo info sttp-core-rs
```

Then verify docs build on docs.rs (can take a few minutes).

## Optional: One-Command Release Preflight

Use the helper script:

```bash
./src/sttp/sttp-core-rs/publish-crates.sh
```

To run publish after preflight:

```bash
./src/sttp/sttp-core-rs/publish-crates.sh --publish
```

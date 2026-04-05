# STTP Changelog

All notable changes across STTP components are documented in this file.

## [0.2.0-beta] - 2026-04-04

### Added

- New deployable dual-transport host: `sttp-gateway`
  - HTTP Minimal API endpoints for health, calibration, store, context retrieval, list, moods, and monthly rollups
  - gRPC service (`sttp.v1.SttpGatewayService`) with equivalent operations
  - gRPC reflection enabled for runtime discovery
- Gateway deployment assets
  - `Dockerfile`
  - `.dockerignore`
  - `build-image.sh`
  - `build.sh` multi-RID packaging script
- New Blazor mobile console: `sttp-ui`
  - server-rendered UI optimized for phone usage over local network/VPN
  - gateway integrations for health, store context, calibrate session, and list nodes
  - configurable gateway target via `Gateway:BaseUrl`

### Changed

- Storage wiring generalized in `sttp-core`:
  - `AddSttpSurrealDbStorage(...)` now supports an optional `rootDirectoryName` override
  - `sttp-mcp` behavior remains unchanged by default (`.sttp-mcp`)
  - `sttp-gateway` uses `.sttp-gateway`
- Gateway host transport configuration updated to reliable non-TLS dual mode:
  - HTTP/1.1 on `8080`
  - gRPC HTTP/2 (h2c) on `8081`
- `sttp-core`/host surface split and reuse pattern expanded:
  - core services exposed as reusable application layer
  - transport hosts (`sttp-mcp`, `sttp-gateway`) consume shared core behavior

### Fixed

- Embedded SurrealKv runtime startup in `sttp-gateway` by copying `libsurreal_surrealkv.so` into build output (matching `sttp-mcp` runtime behavior)
- `sttp-core` storage identity fix:
  - `SurrealDbNodeStore.StoreAsync` now returns a real persisted node record ID instead of session identifier-like output
  - Surreal record creation uses escaped GUID-based record IDs for parser-safe inserts
- `create_monthly_rollup` input boundary hardening:
  - replaced unsafe date parse path with validated parse and structured invalid-date error behavior at tool boundary
- `sttp-mcp` container build path correction after directory relocation:
  - Docker build context depth and `COPY` paths aligned with `src/sttp/sttp-mcp` layout
- test suite hygiene:
  - removed empty placeholder test artifact that inflated reported test counts

### Validated

- `dotnet build` success for `sttp-gateway`
- Local Docker image build success: `sttp-gateway:local`
- Container smoke test success:
  - `GET /health` returns expected status payload
  - listeners active on `8080` and `8081`
- STTP regression validation from core-fix pass:
  - tests passing after fixes (`10/10`)
  - persisted node integrity and monthly rollup continuity verified across runtime swaps

### Documentation

- Added gateway usage docs for local run, Docker build/run, and dual-port behavior
- Changelog moved from `src/sttp/sttp-mcp/CHANGELOG.md` to `src/sttp/CHANGELOG.md` to cover shared STTP invariants

### Reviewed Coverage

- Reviewed all currently available persisted STTP nodes via `list_nodes` export:
  - total nodes: 29
  - sessions represented: 23
  - tier distribution: 18 raw, 10 daily, 1 monthly
  - time span: 2026-03-05 to 2026-04-04
- Confirmed changelog scope now accounts for parallel workstreams present in node history:
  - STTP core refactor/fixes and gateway dual-transport implementation
  - monthly rollup synthesis/checkpoint flow
  - ACC-related exploration sessions (`acc-*`, `adaptive-lsp-*`, `acc-vscode-extension-piping`)
  - design-side protocol ideation (`dmc-protocol-design-2026-04-04`)
  - adjacent product/prototype session (`portfolio-build-tom-vazquez-2026-04-04`)

## [0.1.1-beta] - 2026-03-07

### Changed

- Improved MCP tool parameter metadata with explicit descriptions across:
  - `calibrate_session`
  - `get_context`
  - `store_context`
  - `get_moods`
- Added numeric guidance for AVEC inputs (0.0 to 1.0, decimal values) to improve model-side argument generation.
- Aligned `store_context` schema description with grammar decisions:
  - `response_format` now documented as `temporal_node|natural_language|hybrid`
  - `schema_version` documented as optional in envelope
  - mandatory/static preamble requirement explicitly documented

### Validated

- Build diagnostics clean in updated tool files
- Tests passed: 9 of 9

## [0.1.0-beta] - 2026-03-05

### Added

- `sttp-mcp` MCP server with tools:
  - `calibrate_session`
  - `store_context`
  - `get_context`
  - `list_nodes`
  - `get_moods` tool endpoint
  - Mood catalog models
  - Dockerfile and .dockerignore
  - CI workflow
  - OSS policy docs
- Persistent embedded SurrealKV storage path handling
- Docker support for stdio-based MCP runtime
- Test project for parsing, validation, storage, and integration behaviors

### Changed

- License changed: MIT to Apache 2.0
- README license visibility (root and tool)
- Root README instrument path fix
- MCP manifest placeholder replacement

### Validated

- Tests passed: 9 of 9
- Docker build and startup smoke success

### Documentation

- STTP protocol overview and architecture docs
- Docker-first getting started instructions
- Initial OSS repository policy files

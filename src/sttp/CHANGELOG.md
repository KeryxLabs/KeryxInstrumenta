# STTP Changelog

Historical STTP-wide changes are documented in this file.
Project-specific changes are now tracked in per-service changelogs.

## Changelog Index

- `sttp-core/CHANGELOG.md`
- `sttp-core-rs/CHANGELOG.md`
- `sttp-gateway/CHANGELOG.md`
- `sttp-gateway-rs/CHANGELOG.md`
- `sttp-mcp/CHANGELOG.md`
- `sttp-ui/CHANGELOG.md`

## [1.2.3] - 2026-04-14

### Fixed

- **`sttp-core` / `sttp-mcp` - SurrealDB mutation and metadata robustness**
  - Added strict mutation response verification in C# Surreal upsert/checkpoint paths.
  - Added explicit mutation success/failure logging for easier runtime diagnosis.
  - Updated metadata transport records to decode via untyped object payloads first, then map safely into typed metadata.
  - Prevented `option<object>` coercion failures by avoiding `NULL` assignments and preserving `NONE` semantics when metadata is missing.
- **`sttp-core-rs` - metadata write parity for optional object fields**
  - Added `NONE`-aware query generation for optional connector metadata/source metadata writes.
- **`sttp-gateway-rs` - query observability**
  - Added explicit query outcome logging (success/failure and row counts) in the Surreal client.

### Changed

- **Release/version alignment for packages, services, scripts, and image examples**
  - .NET services/tooling aligned to `1.2.3`.
  - `sttp-gateway-rs` crate aligned to `1.2.3`.
  - `sttp-core-rs` crate bumped to `0.1.4`.
  - Build scripts, compose tags, and README examples updated to the same release set.
- **`sttp-core` NuGet packaging metadata**
  - Added package metadata and packaged README support for cleaner NuGet consumption.

## [1.2.2] - 2026-04-12

### Fixed

- **`sttp-core-rs` — legacy temporal sync-field coercion regression under SCHEMAFULL**
  - Startup backfill now repairs legacy `temporal_node` rows missing persisted sync fields before tenant backfill writes.
  - Legacy `updated_at` fallback order: persisted `updated_at` -> `timestamp` -> current UTC.
  - Legacy `sync_key` fallback for blank/missing rows: `legacy:<node_id>`.
  - Prevents SurrealDB write failures such as: `Expected datetime but found NONE` when mutating legacy rows.

### Changed

- **STTP changelog layout**
  - Added per-project changelog files under each active STTP service/project directory.
  - This consolidated file remains as the historical cross-project release stream.

## [1.2.1] - 2026-04-11

### Added

- **`sttp-core` / `sttp-core-rs` — sync primitives and safe coordinator boundary**
  - Added deterministic `sync_key` generation for STTP nodes in both runtimes
  - Added idempotent upsert semantics with created / updated / duplicate / skipped outcomes
  - Added incremental change-query support with cursor-based pagination
  - Added connector checkpoint persistence for sync state tracking
  - Added typed `ConnectorMetadata` envelopes for node provenance and checkpoint metadata instead of opaque metadata blobs
  - Added narrow sync coordinator surfaces that own mechanics only:
    - Rust: `SyncChangeSource`, `SyncCoordinatorPolicy`, `SyncPullRequest`, `SyncPullResult`, `SyncCoordinatorService`
    - C#: `ISyncChangeSource`, `ISyncCoordinatorPolicy`, `SyncPullRequest`, `SyncPullResult`, `SyncCoordinatorService`

### Fixed

- **`sttp-core` / `sttp-core-rs` — typed metadata and sync-schema completion**
  - Sync metadata fields are now additive and backward-compatible for existing rows and existing callers
  - Node provenance and checkpoint metadata now use typed `ConnectorMetadata` envelopes instead of opaque blobs in both runtimes
  - Legacy rows without persisted sync fields continue to read correctly via fallback handling for `updated_at`, `sync_key`, and legacy tenant values

## [1.2.0] - 2026-04-11

### Fixed

- **`sttp-core` / `sttp-mcp` — SurrealDB tenant-schema compatibility regression**
  - Resolved MCP tool failures observed in:
    - `calibrate_session`
    - `list_nodes`
  - Root cause: stricter tenant-aware schema (`tenant_id` required under `SCHEMAFULL`) after newer gateway/storage migrations.
  - `SurrealDbNodeStore` updated to:
    - define `tenant_id` fields for `temporal_node` and `calibration`
    - define tenant+session composite indexes
    - write `tenant_id` for node and calibration inserts (`default` tenant path)
    - apply tenant-compatible read predicates with legacy fallback (`tenant_id = NONE` or empty)
    - use datetime-cast predicates for range filters in query paths

### Validated

- `dotnet build` passes for:
  - `src/sttp/sttp-core/sttp-core.csproj`
  - `src/sttp/sttp-mcp/sttp-mcp.csproj`
- Live MCP smoke checks against the rebuilt image:
  - `calibrate_session` returns successful calibration payload
  - `list_nodes` succeeds for both filtered and unfiltered queries

## [1.1.0] - 2026-04-06

### Added

- **`sttp-ui` — Psych Layer UI (session-state mirror)**
  - New in-card psych layer combining:
    - dynamic **vibe orb** (hue/energy/pulse driven from AVEC-derived state)
    - 5-axis **radar state shape** (`Curiosity`, `Discipline`, `Social Energy`, `Flexibility`, `Stress Load`)
    - **session reflection** readout with archetype, interpretation, and next nudge
  - Radar legend added with per-axis bar + percent for faster glanceability on mobile
  - Explicit non-identity scope language added: this is a session-state snapshot, not a fixed trait label

### Changed

- **`sttp-ui` — Psych mapping and narrative behavior**
  - Added deterministic AVEC-to-psych mapping with compression-first source selection and user/model fallback averaging
  - Added archetype resolver and reflection generator tuned for session momentum framing
  - Nudge logic refined by stress/coherence bands for more actionable copy
- **`sttp-ui` — Mobile psych layer footprint**
  - Reduced mobile orb size (`72px -> 66px`)
  - Reduced mobile radar footprint (`min(100%, 280px) -> min(92%, 258px)`)
- **`sttp-ui` — Motion pass**
  - Meaningful, staggered animation choreography for psych layer:
    - panel rise-in
    - orb float + pulse
    - radar ring/axis reveal, shape pop-in, node reveal
    - legend row + fill progression
    - reflection delayed reveal
  - `prefers-reduced-motion` support extended to disable all new psych-layer animations while preserving visibility

### Validated

- `dotnet build` passed after each major patch (`src/sttp/sttp-ui/sttp-ui.csproj`)

### Session Coverage

- Session: `sttp_ui_psych_layer_design` (2026-04-06)
- Stored checkpoints:
  - `a6c8f094d144423694808162d404f232` (psych layer + mobile sizing)
  - `246e6a8ead014f0eb40f0fbf02685715` (motion pass)
- Changed files:
  - `src/sttp/sttp-ui/Components/Pages/Home.razor`
  - `src/sttp/sttp-ui/Components/Pages/Home.razor.css`

## [1.0.0] - 2026-04-05

### Added

- **`sttp-ui` — AI Summary via Ollama**
  - `OllamaService` (typed `HttpClient`) integrates a local Ollama instance for per-node AI summarization
  - `OllamaOptions` config section (`BaseUrl`, `Model`) registered in `Program.cs`
  - UI surface: trigger pill with dot-pulse loading indicator, dismissable error state, five-section summary card
  - Per-node in-memory cache keyed by session + timestamp; cache cleared on swipe navigation, node select, and explicit clear-all
  - Structured `ILogger` instrumentation covering request dispatch, elapsed time, parse failures, and missing sections
- **`sttp-ui` — Unwinder helper (`Helpers/Unwinder.cs`)**
  - Deterministic signal interpreter; introduces no new information, only surfaces what is present in the node
  - Score formula: `(logic + stability + autonomy) / 3 − friction`
  - Status levels: **Great** (≥ 0.75), **Good** (≥ 0.50), **Friction** (≥ 0.25), **Stuck** (< 0.25)
  - Outputs: status icon, label, CSS class, plain-language summary, interpretation, and next-action recommendation
  - Interpretation matrix: 4-quadrant friction × logic grid
  - Next-action decision tree: 4 cases
  - Summary extraction via regex against raw node content; fallback converts session ID (snake_case → sentence)
- **`sttp-ui` — Session Directory**
  - "Browse Sessions" button triggers a popup listing up to 50 sessions from the gateway graph endpoint
  - Columns: session ID, node count, avg score, last modified
  - Tap behavior: jump to session if already loaded; otherwise filter and reload

### Changed

- **`sttp-ui` — Non-technical UX layer**
  - Graph terms replaced with plain language: "session map", "moments", "signal strength", "consistency", "overall score"
  - Score-related technical sections renamed to "Advanced" / "Score Breakdown"
  - Error messages rewritten as action-oriented, friendly copy
  - Quick actions added to cards: "Show Moment List" and "Show Details"
  - Navigation arrows made always-visible
- **`sttp-ui` — Visual & color system**
  - Unwinder status pill treatment applied globally: 18% fill / 40% border per level; `great` → `#2d6b49`, `good` → `#7a5a1e`, `friction` → `#9c5410`, `stuck` → `#a83820`
  - Tag pills: node count = blue, date = amber, tier = green; tag fills at 26% bg / 52% border globally
  - `rho` → amber, `kappa` → blue full pill treatment everywhere
  - Timestamps: amber (`#8a6828`) across all surfaces
  - Panel labels elevated to `ink2` (no longer muted)
  - AI summary readability: smaller gray supporting-context style aligned to unwinder visual language
- **`sttp-ui` — Detail panel**
  - Unwind card leads with: status → psi → summary → interpretation → next action
  - Tech details hidden behind `showTechDetails` toggle (`false` by default); metrics grid and AVEC grid collapsed when toggled off
- **`sttp-ui` — Responsive layout**
  - Desktop: flow grid, full-width, single column
  - Widescreen (≥ 1400 px): detail body two-column dense mode
  - Mobile: card-top stacked, node aside reflows; rho/kappa wrap with fixed spacing
- **`sttp-ui` — Overflow fixes**
  - Score breakdown, metrics, and AVEC grids switch to `minmax(0, 1fr)` + `overflow-wrap: anywhere` to eliminate horizontal blowout when metrics are visible
- **`sttp-ui` — AI summary cache key strategy**
  - Cache key changed from a reuse-prone strategy to a deterministic fingerprint: `session + timestamp + tier + depth + SHA-256(raw)`
- **`docker-compose.yml`**
  - Compose updated to reference published images: `ghcr.io/keryxlabs/sttp-gateway:1.0.0` + `ghcr.io/keryxlabs/sttp-ui:1.0.0`
  - Both services placed on `sttp-bridge` network; `sttp-ui` exposed on port `5257`
  - Gateway invoked with `--remote` + `--remote-endpoint` args for SurrealDB connectivity

### Fixed

- **`appsettings.Development.json` override bug**: development config was overriding the real Ollama server URL; synced to match `appsettings.json` (server: `http://10.27.27.57:11434`, model: `sttp-encoder`)
- **Blazor 10.6 publish artifact blowout**: excluded publish output directory via `<Content Remove="..." />` in `sttp-ui.csproj` to prevent spurious files being included in build output

### Validated

- `dotnet build` passed after each major patch (`src/sttp/sttp-ui/sttp-ui.csproj`)
- Docker image build: `ghcr.io/keryxlabs/sttp-ui:1.2.0`

### Documentation

- `src/sttp/README.md` expanded:
  - Four-layer format overview
  - AVEC architecture diagram/tree
  - Per-component docs section
  - Build-from-source instructions

### Session Coverage

- Session: `sttp-ui-improvements` (2026-04-05)
- 3 nodes retrieved across 3 compression passes (rho: 0.95–0.97, kappa: 0.97–0.98)
- Changed files confirmed across nodes:
  - `src/sttp/sttp-ui/Helpers/Unwinder.cs`
  - `src/sttp/sttp-ui/Services/OllamaService.cs`
  - `src/sttp/sttp-ui/Services/OllamaOptions.cs`
  - `src/sttp/sttp-ui/Models/GatewayDtos.cs`
  - `src/sttp/sttp-ui/Components/Pages/Home.razor`
  - `src/sttp/sttp-ui/Components/Pages/Home.razor.css`
  - `src/sttp/sttp-ui/Program.cs`
  - `src/sttp/sttp-ui/appsettings.json`
  - `src/sttp/sttp-ui/appsettings.Development.json`
  - `src/sttp/sttp-ui/sttp-ui.csproj`
  - `src/sttp/docker-compose.yml`
  - `src/sttp/README.md`

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

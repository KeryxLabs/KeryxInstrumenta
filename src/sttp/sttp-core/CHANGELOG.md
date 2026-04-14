# sttp-core Changelog

All notable changes specific to sttp-core are documented in this file.
For historical entries before this split, see ../CHANGELOG.md.

## [Unreleased]

## [1.2.3] - 2026-04-14

### Added

- Added NuGet package metadata for `Sttp.Core` (package ID, authors, license, repository/project URLs, tags).
- Added packaged README support for NuGet consumers.

### Fixed

- Added explicit SurrealDB mutation response verification/logging in `SurrealDbNodeStore` upsert flow.
- `StoreAsync` now logs upsert outcome (`Created`/`Updated`/`Duplicate`) with node and sync context.
- Upsert now treats non-OK query responses as failures instead of silently returning a node ID.
- Metadata payload transport records now decode as untyped objects first, then map safely to typed connector metadata.
- Optional metadata writes now preserve `NONE` semantics and avoid `NULL` coercion failures in SurrealDB.

### Changed

- Package/service version metadata aligned to `1.2.3`.

## Historical Highlights

- 1.2.1 (2026-04-11): Added sync primitives and safe coordinator boundary work shared with sttp-core-rs.
- 1.2.0 (2026-04-11): Fixed SurrealDB tenant-schema compatibility regression affecting calibration and list flows in sttp-core/sttp-mcp.
- 0.2.0-beta (2026-04-04): Expanded core surface reuse across hosts and generalized storage wiring for host-specific root directories.

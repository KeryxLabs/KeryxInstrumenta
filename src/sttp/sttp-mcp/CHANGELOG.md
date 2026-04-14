# sttp-mcp Changelog

All notable changes specific to sttp-mcp are documented in this file.
For historical entries before this split, see ../CHANGELOG.md.

## [Unreleased]

## [1.2.3] - 2026-04-14

### Fixed

- Store/mutation paths now validate Surreal query response statuses and fail fast on non-OK results.
- Added explicit mutation result logging so runtime insert/upsert failures are visible in logs.
- Metadata transport records now decode through object-based payloads before typed mapping, preventing CBOR typed payload coercion failures.
- Optional `source_metadata`/checkpoint metadata writes now preserve `NONE` semantics instead of sending `NULL` into `option<object>` fields.

### Changed

- MCP package/project version metadata bumped to `1.2.3`.
- Build/release script examples updated to `1.2.3`.

## Historical Highlights

- 1.2.0 (2026-04-11): Fixed SurrealDB tenant-schema compatibility regression impacting MCP `calibrate_session` and `list_nodes` flows.
- 0.2.0-beta (2026-04-04): Continued host reuse over shared sttp-core behavior and dual-runtime packaging assets.

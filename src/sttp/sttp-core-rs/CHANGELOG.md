# sttp-core-rs Changelog

All notable changes specific to sttp-core-rs are documented in this file.
For historical entries before this split, see ../CHANGELOG.md.

## [Unreleased]

## [0.1.4] - 2026-04-14

### Fixed

- SurrealDB startup backfill now repairs legacy temporal_node rows that are missing persisted sync fields before tenant backfill writes.
- Legacy updated_at fallback order: existing updated_at -> timestamp -> current UTC.
- Legacy sync_key fallback for blank or missing rows: legacy:<node_id>.
- Prevents SCHEMAFULL write failures such as Expected datetime but found NONE when mutating legacy rows.
- Optional connector metadata and source metadata writes now use `NONE`-aware query paths instead of sending `NULL` to `option<object>` fields.

### Changed

- Crate version bumped to `0.1.4`.

## Historical Highlights

- 1.2.1 (2026-04-11): Added sync primitives, typed ConnectorMetadata envelopes, idempotent upserts, cursor-based change queries, and checkpoint persistence.
- 1.2.1 (2026-04-11): Confirmed backward-compatible reads for legacy rows missing sync_key, updated_at, and legacy tenant values.

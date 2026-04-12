# sttp-gateway-rs Changelog

All notable changes specific to sttp-gateway-rs are documented in this file.
For historical entries before this split, see ../CHANGELOG.md.

## [Unreleased]

### Changed

- Clarified backend selection behavior in documentation and troubleshooting.
- Added explicit guidance that --remote or --surreal-remote-endpoint do not switch the backend by themselves.
- Documented that --backend surreal (or STTP_GATEWAY_BACKEND=surreal) is required to read/write SurrealDB data.

## Historical Highlights

- 2026-04-12: Documented and validated default backend behavior (`in-memory`) for `/api/v1/nodes` troubleshooting.
- 2026-04-12: Added explicit startup guidance for Surreal mode activation via `--backend surreal` or `STTP_GATEWAY_BACKEND=surreal`.

# sttp-gateway-rs Changelog

All notable changes specific to sttp-gateway-rs are documented in this file.
For historical entries before this split, see ../CHANGELOG.md.

## [Unreleased]

### Changed

- Added Resonantia BYO Node Store compatibility aliases for HTTP endpoints:
	- `POST /api/store`, `POST /store` -> `POST /api/v1/store`
	- `GET /api/nodes`, `GET /nodes` -> `GET /api/v1/nodes`
	- `GET /api/graph`, `GET /graph` -> `GET /api/v1/graph`
- Added BYO CORS support and preflight handling with permissive defaults.
- Added tenant header aliases for HTTP resolution (`x-resonantia-tenant`, `x-tenant-id`, `x-tenant`).
- Updated Node Store HTTP response compatibility:
	- list nodes now includes `syncKey` and `syntheticId`
	- store response now includes `duplicateSkipped` and `upsertStatus`
- Added BYO session rename endpoint support:
	- `POST /api/v1/session/rename`
	- aliases: `POST /api/session/rename`, `POST /session/rename`
	- request fields: `sourceSessionId`, `targetSessionId`, `allowMerge`
	- response fields: `sourceSessionId`, `targetSessionId`, `movedNodes`, `movedCalibrations`, `scopesApplied`

## [1.2.3] - 2026-04-14

### Changed

- Clarified backend selection behavior in documentation and troubleshooting.
- Added explicit guidance that --remote or --surreal-remote-endpoint do not switch the backend by themselves.
- Documented that --backend surreal (or STTP_GATEWAY_BACKEND=surreal) is required to read/write SurrealDB data.
- Added explicit query outcome logging in the runtime Surreal client for success/failure visibility across read and mutation operations.
- Crate and default image tag references updated to `1.2.3`.

## Historical Highlights

- 2026-04-12: Documented and validated default backend behavior (`in-memory`) for `/api/v1/nodes` troubleshooting.
- 2026-04-12: Added explicit startup guidance for Surreal mode activation via `--backend surreal` or `STTP_GATEWAY_BACKEND=surreal`.

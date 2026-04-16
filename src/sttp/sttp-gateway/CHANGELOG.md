# sttp-gateway Changelog

All notable changes specific to sttp-gateway are documented in this file.
For historical entries before this split, see ../CHANGELOG.md.

## [Unreleased]

### Changed

- Added Resonantia BYO Node Store compatibility aliases for HTTP endpoints:
	- `POST /api/store`, `POST /store` -> `POST /api/v1/store`
	- `GET /api/nodes`, `GET /nodes` -> `GET /api/v1/nodes`
	- `GET /api/graph`, `GET /graph` -> `GET /api/v1/graph`
- Added permissive BYO CORS support with preflight handling (`AllowAnyOrigin`, `AllowAnyMethod`, `AllowAnyHeader`).
- Added tenant header aliases for HTTP and gRPC resolution (`x-resonantia-tenant`, `x-tenant-id`, `x-tenant`).
- Updated Node Store HTTP response compatibility:
	- list nodes now includes `syncKey` and `syntheticId`
	- store response now includes `duplicateSkipped` and `upsertStatus`

## [1.2.3] - 2026-04-14

### Changed

- Added explicit project version metadata (`Version`, `AssemblyVersion`, `FileVersion`) to `sttp-gateway.csproj`.
- Updated release/build script version to `1.2.3`.

## Historical Highlights

- 0.2.0-beta (2026-04-04): Introduced deployable dual-transport sttp-gateway host with HTTP and gRPC surfaces.
- 0.2.0-beta (2026-04-04): Added packaging assets (Dockerfile, build-image.sh, build.sh) and stabilized embedded runtime startup behavior.

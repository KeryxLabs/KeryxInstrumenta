# Changelog

All notable changes to this project are documented in this file.

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

### Constraints

- Kept existing style
- No fabrication
- Concise bullets
- No unrelated refactor
# ACC for VS Code

ACC for VS Code auto-launches the ACC engine for your workspace and gives you in-editor lookups for indexed code entities.

It is designed to reduce "search by grep + memory" workflows by providing dimensional code intelligence directly from the command palette.

## What It Does

- Auto-checks for an ACC server binary on startup
- Offers one-click binary download when missing
- Auto-starts ACC engine for the current workspace
- Builds dependency graph by tapping symbols/references from VS Code providers
- Supports quick lookups and navigation to matching nodes
- Surfaces high-friction and unstable nodes for architectural triage

## Commands

Open the command palette and run:

- `ACC: Download Server Binary`
- `ACC: Build Dependency Graph`
- `ACC: Search Nodes`
- `ACC: Show Project Stats`
- `ACC: Show High-Friction Nodes`
- `ACC: Show Unstable Nodes`

## First Run

1. Install the extension.
2. Open a workspace folder.
3. If ACC binary is missing, approve `ACC: Download Server Binary`.
4. Run `ACC: Build Dependency Graph` once to index the workspace.
5. Use `ACC: Search Nodes` to verify lookups and jump to source.

## Configuration

Settings are available under `ACC` in VS Code settings.

- `acc.serverPath`
  - Optional explicit path to an ACC binary.
  - Leave empty to use auto-download.

- `acc.rpcPort` (default `9339`)
  - JSON-RPC port used for ACC query calls.

- `acc.lspPort` (default `9340`)
  - LSP stream forwarding port used by ACC ingestion.

- `acc.database.remote` (default `false`)
  - Enables writing/indexing to remote SurrealDB.

- `acc.database.remoteEndpoint` (default `ws://localhost:8000/rpc`)
  - Remote SurrealDB endpoint used when remote mode is enabled.

- `acc.telemetry.use` (default `false`)
  - Enables ACC telemetry output.

- `acc.telemetry.endpoint` (default `localhost:4317`)
  - OTEL collector endpoint used when telemetry is enabled.

## How Lookups Work

When you save files or switch editors, the extension taps symbol/reference information from VS Code language features and forwards it to ACC.

`ACC: Build Dependency Graph` runs a full workspace pass over common source file types:

- `cs`, `ts`, `js`, `py`, `go`, `rs`

Search and analysis commands then query ACC over JSON-RPC and present navigable results in quick picks.

## Requirements

- VS Code `^1.110.0`
- A supported project language with symbol/reference support via VS Code providers
- Network access for first-time binary download (unless `acc.serverPath` is set)

## Troubleshooting

- Open `View -> Output`, then select `ACC` channel.
- If server startup fails:
  - verify workspace is open
  - verify `acc.serverPath` points to an executable (if set)
  - verify `acc.rpcPort` is free
- If searches return empty:
  - run `ACC: Build Dependency Graph` first
  - ensure language tooling in VS Code is available for your files

## Development

```bash
cd src/acc/acc-vscode
npm install
npm run compile
```

Package extension:

```bash
vsce package
```

## Related

- ACC engine docs: `../README.md`
- Repository: https://github.com/KeryxLabs/KeryxInstrumenta

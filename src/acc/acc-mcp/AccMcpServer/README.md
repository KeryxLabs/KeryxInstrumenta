# ACC MCP Server (`acc-mcp`)

`acc-mcp` is the MCP server for ACC (Adaptive Codec Context).

It exposes ACC graph and AVEC analysis tools over stdio so MCP-compatible clients can query indexed code intelligence without embedding ACC directly.

## What This Server Exposes

`acc-mcp` currently provides these MCP tools:

- `get_acc_node`
- `query_relations`
- `query_dependencies`
- `query_patterns`
- `search_by_name`
- `get_high_friction_nodes`
- `get_unstable_nodes`
- `get_project_stats`

These map to ACC engine JSON-RPC methods and return JSON payloads from the engine.

## Important Dependency

`acc-mcp` is a bridge. It requires the ACC engine to be running and reachable.

- Default endpoint: `localhost:9339`
- Override via environment variables:
  - `AccEngine__Host`
  - `AccEngine__Port`

Before starting `acc-mcp`, start ACC engine and build/index your graph.

## Getting Started

### Option A: Run from GHCR (fastest)

```bash
docker run --rm -i \
  -e AccEngine__Host=host.docker.internal \
  -e AccEngine__Port=9339 \
  ghcr.io/keryxlabs/acc-mcp:<version>
```

On Linux, if `host.docker.internal` is not available, use your host IP.

### Option B: Download release binary (no Docker)

Linux x64 example:

```bash
VERSION="0.1.0"
curl -fL -o acc-mcp.tar.gz \
  "https://github.com/KeryxLabs/KeryxInstrumenta/releases/download/acc-mcp/v${VERSION}/acc-mcp-${VERSION}-linux-x64.tar.gz"
tar -xzf acc-mcp.tar.gz
chmod +x AccMcpServer
./AccMcpServer
```

### Option C: Run from source

```bash
dotnet restore
dotnet run --project ./AccMcpServer.csproj
```

## MCP Client Configuration

### Docker (GHCR)

```json
{
  "servers": {
    "AccMcp": {
      "type": "stdio",
      "command": "docker",
      "args": [
        "run",
        "--rm",
        "-i",
        "-e",
        "AccEngine__Host=host.docker.internal",
        "-e",
        "AccEngine__Port=9339",
        "ghcr.io/keryxlabs/acc-mcp:<version>"
      ]
    }
  }
}
```

### Local binary

```json
{
  "servers": {
    "AccMcp": {
      "type": "stdio",
      "command": "/absolute/path/to/AccMcpServer"
    }
  }
}
```

### Local source checkout

```json
{
  "servers": {
    "AccMcp": {
      "type": "stdio",
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "/absolute/path/to/src/acc/acc-mcp/AccMcpServer/AccMcpServer.csproj"
      ]
    }
  }
}
```

## Example Agent Prompts

- "Use `get_project_stats` and summarize codebase AVEC health."
- "Run `get_high_friction_nodes` with `minFriction: 0.8` and show top 10 risks."
- "Find `Authenticate` with `search_by_name`, then run `query_dependencies` incoming depth 3."
- "For `UserService.cs:AuthenticateAsync:23`, run `query_relations` and explain direct callers and callees."

## Build And Release Artifacts

Use `build.sh` in this directory to publish self-contained binaries per RID and optionally upload to namespaced release tags:

- Tag format: `acc-mcp/v<version>`
- Artifact format: `acc-mcp-<version>-<platform>.tar.gz`

```bash
./build.sh
./build.sh --publish
```

Supported platforms:

- `linux-x64`
- `linux-arm64`
- `linux-musl-x64`
- `macos-x64`
- `macos-arm64`
- `win-x64`
- `win-arm64`

## Troubleshooting

- Connection refused / timeout:
  - Verify ACC engine is running and listening on `AccEngine__Host:AccEngine__Port`.
- Empty or low-quality results:
  - Rebuild ACC graph/index before querying from MCP.
- Docker cannot reach host engine:
  - Use `AccEngine__Host=host.docker.internal` (or host IP on Linux).

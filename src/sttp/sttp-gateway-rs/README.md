# sttp-gateway-rs

Rust STTP gateway exposing a unified HTTP + gRPC surface over `sttp-core-rs`.

It is intended to be deployable as a single binary in front of STTP storage backends.

## What It Provides

- HTTP API using axum.
- gRPC API using tonic with reflection enabled.
- Backend selection:
  - in-memory backend for local/dev scenarios.
  - SurrealDB backend for persistent deployments.
- Multi-tenant scoping with backward-compatible default tenant behavior.
- Batch scope rekey operation (dry-run + apply), anchored by node IDs.

## Quick Start

Run with defaults (in-memory backend, HTTP 8080, gRPC 8081):

```bash
cargo run --manifest-path src/sttp/sttp-gateway-rs/Cargo.toml
```

Run with Surreal remote backend:

```bash
cargo run --manifest-path src/sttp/sttp-gateway-rs/Cargo.toml -- \
  --backend surreal \
  --remote \
  --surreal-remote-endpoint ws://127.0.0.1:8000/rpc \
  --surreal-namespace keryx \
  --surreal-database sttp_mcp \
  --surreal-user root \
  --surreal-password root
```

Check health:

```bash
curl -s http://127.0.0.1:8080/health
```

## Runtime Configuration

All flags have environment variable equivalents.

| Flag | Env Var | Default | Notes |
| --- | --- | --- | --- |
| `--http-port` | `STTP_GATEWAY_HTTP_PORT` | `8080` | HTTP listener |
| `--grpc-port` | `STTP_GATEWAY_GRPC_PORT` | `8081` | gRPC listener (h2c) |
| `--backend` | `STTP_GATEWAY_BACKEND` | `in-memory` | `in-memory` or `surreal` |
| `--root-dir-name` | `STTP_GATEWAY_ROOT_DIR_NAME` | `.sttp-gateway` | Local runtime data root name |
| `--remote` | `STTP_GATEWAY_REMOTE` | `false` | Surreal remote mode toggle |
| `--surreal-embedded-endpoint` | `STTP_SURREAL_EMBEDDED_ENDPOINT` | unset | Embedded endpoint override |
| `--surreal-remote-endpoint` | `STTP_SURREAL_REMOTE_ENDPOINT` | unset | Remote endpoint override |
| `--surreal-namespace` | `STTP_SURREAL_NAMESPACE` | `keryx` | Surreal namespace |
| `--surreal-database` | `STTP_SURREAL_DATABASE` | `sttp-mcp` | Surreal database |
| `--surreal-user` | `STTP_SURREAL_USER` | `root` | Surreal username |
| `--surreal-password` | `STTP_SURREAL_PASSWORD` | `root` | Surreal password |

## Tenant Scoping

### Resolution Rules

- Default tenant is `default`.
- HTTP requests resolve tenant from:
  1. request field (`tenantId`) when present and valid,
  2. `x-tenant-id` header,
  3. fallback to `default`.
- gRPC requests resolve tenant from metadata key `x-tenant-id`, fallback `default`.

### Session Scoping

- Non-default tenants are internally represented as:

  `tenant:<tenant>::session:<sessionId>`

- Default tenant remains unscoped for backward compatibility.
- API responses normalize and return unscoped `sessionId` values.

### Practical Examples

Store with tenant header:

```bash
curl -s -X POST http://127.0.0.1:8080/api/v1/store \
  -H 'content-type: application/json' \
  -H 'x-tenant-id: acme' \
  -d '{
    "sessionId":"gateway-rust-port",
    "node":"⊕⟨ ... ⟩"
  }'
```

List scoped nodes:

```bash
curl -s 'http://127.0.0.1:8080/api/v1/nodes?limit=50&sessionId=gateway-rust-port&tenantId=acme'
```

## HTTP API

### Endpoints

- `GET /health`
- `POST /api/v1/calibrate`
- `POST /api/v1/store`
- `POST /api/v1/context`
- `GET /api/v1/nodes?limit=50&sessionId=...&tenantId=...`
- `GET /api/v1/graph?limit=1000&sessionId=...&tenantId=...`
- `GET /api/v1/moods?targetMood=focused&blend=1`
- `POST /api/v1/rekey`
- `POST /api/v1/rollups/monthly`

### Example: Calibrate

```bash
curl -s -X POST http://127.0.0.1:8080/api/v1/calibrate \
  -H 'content-type: application/json' \
  -d '{
    "sessionId":"gateway-rust-port",
    "stability":0.9,
    "friction":0.2,
    "logic":0.95,
    "autonomy":0.9,
    "trigger":"manual"
  }'
```

### Example: Context Retrieval

```bash
curl -s -X POST http://127.0.0.1:8080/api/v1/context \
  -H 'content-type: application/json' \
  -d '{
    "sessionId":"gateway-rust-port",
    "stability":0.9,
    "friction":0.2,
    "logic":0.95,
    "autonomy":0.9,
    "limit":5
  }'
```

## Batch Rekey (HTTP)

Endpoint: `POST /api/v1/rekey`

Purpose: re-scope data from one tenant/session scope to another across both `temporal_node` and `calibration`, anchored by provided node IDs.

### Request Fields

- `nodeIds`: array of anchor IDs (required).
  - Supports bare node ID (`abc123`) or prefixed form (`temporal_node:abc123`).
- `targetSessionId`: destination session ID (required).
- `targetTenantId`: destination tenant (optional).
  - Falls back to tenant header or default tenant.
- `dryRun`: optional, defaults to `true`.
- `allowMerge`: optional, defaults to `false`.

### Recommended Workflow

1. Call with `dryRun=true`.
2. Inspect `scopes`, `conflictScopes`, and row counts.
3. Re-run with `dryRun=false` only when output looks correct.
4. If target already has rows and merge is intended, set `allowMerge=true`.

### Dry-Run Example

```bash
curl -s -X POST http://127.0.0.1:8080/api/v1/rekey \
  -H 'content-type: application/json' \
  -d '{
    "nodeIds": ["05ee92706d2b44e8a040e3db2f58175a"],
    "targetTenantId": "acme",
    "targetSessionId": "gateway-rust-port",
    "dryRun": true,
    "allowMerge": false
  }'
```

### Apply Example

```bash
curl -s -X POST http://127.0.0.1:8080/api/v1/rekey \
  -H 'content-type: application/json' \
  -d '{
    "nodeIds": ["05ee92706d2b44e8a040e3db2f58175a"],
    "targetTenantId": "acme",
    "targetSessionId": "gateway-rust-port",
    "dryRun": false,
    "allowMerge": true
  }'
```

## gRPC API

Proto file: `proto/sttp.proto`

Service: `sttp.v1.SttpGatewayService`

Methods:

- `CalibrateSession`
- `StoreContext`
- `GetContext`
- `ListNodes`
- `GetMoods`
- `BatchRekey`
- `CreateMonthlyRollup`

gRPC reflection is enabled.

### grpcurl Examples

List services:

```bash
grpcurl -plaintext 127.0.0.1:8081 list
```

Calibrate with tenant metadata:

```bash
grpcurl -plaintext \
  -H 'x-tenant-id: acme' \
  -d '{"sessionId":"gateway-rust-port","stability":0.9,"friction":0.2,"logic":0.95,"autonomy":0.9,"trigger":"manual"}' \
  127.0.0.1:8081 sttp.v1.SttpGatewayService/CalibrateSession
```

Batch rekey dry-run:

```bash
grpcurl -plaintext \
  -H 'x-tenant-id: acme' \
  -d '{"nodeIds":["05ee92706d2b44e8a040e3db2f58175a"],"targetSessionId":"gateway-rust-port","targetTenantId":"acme","dryRun":true,"allowMerge":false}' \
  127.0.0.1:8081 sttp.v1.SttpGatewayService/BatchRekey
```

## Docker (Host Build + Minimal Runtime Image)

To keep Docker builds fast and cool, this image follows the same pattern as the C# services:

1. Build the binary on the host.
2. Copy publish output into a tiny runtime image.

No Rust toolchain is installed in the container image.

### Prerequisites On Host

- Rust toolchain with cargo.
- Docker.

### One-Command Build And Package

```bash
./src/sttp/sttp-gateway-rs/build-image.sh
```

Custom tag:

```bash
./src/sttp/sttp-gateway-rs/build-image.sh ghcr.io/keryxlabs/sttp-gateway-rs:latest
```

### Manual Build And Package

Build locally:

```bash
cargo build --release --locked --manifest-path src/sttp/sttp-gateway-rs/Cargo.toml
```

Prepare publish folder:

```bash
mkdir -p src/sttp/sttp-gateway-rs/publish
cp src/sttp/sttp-gateway-rs/target/release/sttp-gateway-rs src/sttp/sttp-gateway-rs/publish/sttp-gateway-rs
chmod +x src/sttp/sttp-gateway-rs/publish/sttp-gateway-rs
```

Build image from repository root:

```bash
docker build \
  -f src/sttp/sttp-gateway-rs/Dockerfile \
  -t sttp-gateway-rs:latest \
  .
```

### Runtime Image Details

- Base image: debian:bookworm-slim.
- Runtime dependency installed: ca-certificates.
- Binary location: /app/sttp-gateway-rs.
- Persistent volume: /data.
- HOME is set to /data so embedded storage state persists when /data is mounted.

### Run: In-Memory Backend

```bash
docker run --rm \
  -p 8080:8080 \
  -p 8081:8081 \
  sttp-gateway-rs:latest
```

### Run: Surreal Remote Backend

```bash
docker run --rm \
  -p 8080:8080 \
  -p 8081:8081 \
  -e STTP_GATEWAY_BACKEND=surreal \
  -e STTP_GATEWAY_REMOTE=true \
  -e STTP_SURREAL_REMOTE_ENDPOINT=ws://host.docker.internal:8000/rpc \
  -e STTP_SURREAL_NAMESPACE=keryx \
  -e STTP_SURREAL_DATABASE=sttp_mcp \
  -e STTP_SURREAL_USER=root \
  -e STTP_SURREAL_PASSWORD=root \
  sttp-gateway-rs:latest
```

### Run: Embedded Mode With Persistent Volume

```bash
docker run --rm \
  -p 8080:8080 \
  -p 8081:8081 \
  -v sttp-gateway-data:/data \
  sttp-gateway-rs:latest
```

## Build And Test

```bash
cargo check --manifest-path src/sttp/sttp-gateway-rs/Cargo.toml
cargo test --manifest-path src/sttp/sttp-gateway-rs/Cargo.toml
```

## Troubleshooting

- If HTTP `404` appears for newly added endpoints, restart the gateway binary to load the latest build.
- If gRPC methods are missing in reflection, rebuild and restart after proto changes.
- If tenant-scoped queries return no data, verify `x-tenant-id`/`tenantId` and session ID pairing.
- For rekey operations, always run dry-run first and inspect conflicts before apply mode.
- If Docker still feels slow, run `build-image.sh` so only packaging runs in Docker and compilation stays on the host cache.
- If container startup fails with `GLIBC_2.38 not found`, rebuild with the updated runtime base (`ubuntu:24.04`) and republish the image tag.

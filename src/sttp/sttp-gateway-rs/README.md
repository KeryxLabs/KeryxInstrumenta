# sttp-gateway-rs

Deployable Rust STTP gateway with dual transport in one process:

- HTTP API (axum)
- gRPC service (tonic + reflection)

The gateway is wired to `sttp-core-rs` services so request semantics align with MCP/core behavior.

## Run

```bash
cargo run --manifest-path src/sttp/sttp-gateway-rs/Cargo.toml
```

Default listeners:

- HTTP: `0.0.0.0:8080`
- gRPC (h2c): `0.0.0.0:8081`

## Options

```bash
cargo run --manifest-path src/sttp/sttp-gateway-rs/Cargo.toml -- \
  --http-port 8080 \
  --grpc-port 8081 \
  --backend in-memory
```

Environment variables:

- `STTP_GATEWAY_HTTP_PORT`
- `STTP_GATEWAY_GRPC_PORT`
- `STTP_GATEWAY_BACKEND` (`in-memory` or `surreal`)
- `STTP_GATEWAY_ROOT_DIR_NAME`
- `STTP_GATEWAY_REMOTE`
- `STTP_SURREAL_EMBEDDED_ENDPOINT`
- `STTP_SURREAL_REMOTE_ENDPOINT`
- `STTP_SURREAL_NAMESPACE`
- `STTP_SURREAL_DATABASE`
- `STTP_SURREAL_USER`
- `STTP_SURREAL_PASSWORD`

## Backend Modes

- `in-memory`: fully supported today.
- `surreal`: fully supported with a concrete Rust `SurrealDbClient` adapter.

Example Surreal remote mode:

```bash
cargo run --manifest-path src/sttp/sttp-gateway-rs/Cargo.toml -- \
  --backend surreal \
  --remote true \
  --surreal-remote-endpoint ws://127.0.0.1:8000/rpc \
  --surreal-namespace keryx \
  --surreal-database sttp-mcp \
  --surreal-user root \
  --surreal-password root
```

## HTTP Endpoints

- `GET /health`
- `POST /api/v1/calibrate`
- `POST /api/v1/store`
- `POST /api/v1/context`
- `GET /api/v1/nodes?limit=50&sessionId=...`
- `GET /api/v1/graph?limit=1000&sessionId=...`
- `GET /api/v1/moods?targetMood=focused&blend=1`
- `POST /api/v1/rollups/monthly`

Health check:

```bash
curl -s http://127.0.0.1:8080/health
```

## gRPC

Proto: `proto/sttp.proto`

Service: `sttp.v1.SttpGatewayService`

Methods:

- `CalibrateSession`
- `StoreContext`
- `GetContext`
- `ListNodes`
- `GetMoods`
- `CreateMonthlyRollup`

gRPC reflection is enabled.

## Build And Test

```bash
cargo check --manifest-path src/sttp/sttp-gateway-rs/Cargo.toml
cargo test --manifest-path src/sttp/sttp-gateway-rs/Cargo.toml
```

# sttp-gateway

Deployable STTP network host with dual transport in one process:

- HTTP Minimal API for simple REST-style integration
- gRPC for typed low-latency service-to-service calls

This gateway reuses `sttp-core` services directly, so behavior matches `sttp-mcp` tooling semantics.

## Run

```bash
dotnet run --project ./sttp-gateway.csproj
```

### Docker

Build local image:

```bash
bash build-image.sh sttp-gateway:local
```

Run container:

```bash
docker run --rm \
  -p 8080:8080 \
  -p 8081:8081 \
  -v "$PWD/data:/data" \
  sttp-gateway:local
```

Default listen addresses:

- HTTP Minimal API: `http://0.0.0.0:8080`
- gRPC: `http://0.0.0.0:8081` (h2c)

Use remote SurrealDB mode:

```bash
dotnet run --project ./sttp-gateway.csproj -- --remote
```

Override options:

```bash
dotnet run --project ./sttp-gateway.csproj -- \
  --http-port 8082 \
  --grpc-port 8083 \
  --database sttp_gateway \
  --namespace keryx
```

Optional local config file:

- `~/.sttp-gateway/appsettings.json`

## HTTP Endpoints

- `GET /health`
- `POST /api/v1/calibrate`
- `POST /api/v1/store`
- `POST /api/v1/context`
- `GET /api/v1/nodes?limit=50&sessionId=...`
- `GET /api/v1/moods?targetMood=focused&blend=1`
- `POST /api/v1/rollups/monthly`

Example health check:

```bash
curl -s http://127.0.0.1:8080/health
```

## gRPC

Proto file: `Protos/sttp.proto`

Service: `sttp.v1.SttpGatewayService`

Methods:

- `CalibrateSession`
- `StoreContext`
- `GetContext`
- `ListNodes`
- `GetMoods`
- `CreateMonthlyRollup`

Reflection is enabled, so tooling like `grpcurl` can discover methods at runtime.

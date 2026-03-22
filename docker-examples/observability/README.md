# Observability Platform

This repository contains a docker compose file used to set up a simple LGTP observabiltiy stack.

- Loki for structured logging
- Prometheus for time series data (Metrics)
- Tempo for distributed traces
- Grafana for visualization

This platform utilizes the otel-collector to aggregate observabiltiy data from external sources on the following ports:

- 4317 for gRPC
- 4318 for HTTP/protobuf

When using HTTP/protobuf, your application should send data to the following endpoints:

- http://host.docker.internal:4318/v1/traces
- http://host.docker.internal:4318/v1/metrics
- http://host.docker.internal:4318/v1/logs

## Execution

From the root of the observability directory, execute:

```ps
docker compose up -d
```

Once it is running, navigate to the Grafana front-end at `localhost:3000`.

- Username: admin
- Password: grafana

To stop the platform, and remove the volumes (clearing all data) execute:

```ps
docker compose down -v
```

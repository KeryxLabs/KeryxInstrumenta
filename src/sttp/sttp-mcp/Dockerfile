FROM mcr.microsoft.com/dotnet/runtime-deps:10.0
WORKDIR /app

ENV STTP_MCP_DATA_ROOT=/

# Binaries are pre-built on the host by build-image.sh before docker build runs.
COPY src/sttp/sttp-mcp/publish/ ./

# App resolves config from $HOME/.sttp-mcp/appsettings.json at startup.
RUN mkdir -p /root/.sttp-mcp
COPY src/sttp/sttp-mcp/appsettings.json /root/.sttp-mcp/appsettings.json

RUN mkdir -p /data

VOLUME ["/data"]

ENTRYPOINT ["/app/sttp-mcp"]
FROM mcr.microsoft.com/dotnet/runtime-deps:10.0
WORKDIR /app

ENV STTP_GATEWAY_DATA_ROOT=/

# Binaries are pre-built on the host by build-image.sh before docker build runs.
COPY src/sttp/sttp-gateway/publish/ ./

# App resolves config from $HOME/.sttp-gateway/appsettings.json at startup.
RUN mkdir -p /root/.sttp-gateway
COPY src/sttp/sttp-gateway/appsettings.json /root/.sttp-gateway/appsettings.json

RUN mkdir -p /data

VOLUME ["/data"]

EXPOSE 8080
EXPOSE 8081

ENTRYPOINT ["/app/sttp-gateway"]

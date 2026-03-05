FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY sttp-mcp.csproj ./
RUN dotnet restore sttp-mcp.csproj

COPY . ./
RUN dotnet publish sttp-mcp.csproj -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -o /app/publish

FROM mcr.microsoft.com/dotnet/runtime-deps:10.0 AS runtime
WORKDIR /app

ENV STTP_MCP_DATA_ROOT=/

COPY --from=build /app/publish ./
RUN mkdir -p /data

VOLUME ["/data"]

ENTRYPOINT ["/app/sttp-mcp"]
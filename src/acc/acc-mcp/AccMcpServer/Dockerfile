FROM mcr.microsoft.com/dotnet/runtime-deps:10.0
WORKDIR /app

# Override the ACC engine endpoint at runtime via environment variables:
#   AccEngine__Host=<host>   (default: localhost)
#   AccEngine__Port=<port>   (default: 9339)
ENV AccEngine__Host=localhost
ENV AccEngine__Port=9339

# Binaries are pre-built on the host by build-image.sh before docker build runs.
# appsettings.json is included in the publish output and is picked up automatically
# from the working directory by Microsoft.Extensions.Hosting.
COPY publish/ ./

ENTRYPOINT ["/app/AccMcpServer"]

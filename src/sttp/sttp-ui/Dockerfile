FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app

# Binaries are pre-built on the host by build-image.sh before docker build runs.
COPY src/sttp/sttp-ui/publish/ ./

ENV ASPNETCORE_HTTP_PORTS=8080
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 8080

ENTRYPOINT ["dotnet", "sttp-ui.dll"]

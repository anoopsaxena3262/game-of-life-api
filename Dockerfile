FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/ src/
RUN dotnet publish src/GameOfLife.Api/GameOfLife.Api.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/* \
    && useradd --uid 10001 --create-home appuser \
    && mkdir -p /data \
    && chown appuser:appuser /data
WORKDIR /app
COPY --from=build /app ./
USER appuser
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Development \
    ConnectionStrings__Default="Data Source=/data/game-of-life.db;Default Timeout=5"
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 \
    CMD curl --fail http://localhost:8080/health/live || exit 1
ENTRYPOINT ["dotnet", "GameOfLife.Api.dll"]

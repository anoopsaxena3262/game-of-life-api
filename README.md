# Game of Life API

Conway's Game of Life as a small HTTP service on .NET 10.

This repository is the scaffold. Domain rules, persistence behaviour, and the four endpoints throw `NotImplementedException` and the API returns **501** for them. Liveness, readiness, OpenAPI, and the project boundaries are wired.

## Run

Setup from a machine that has never used .NET, and how to call the service, is in [docs/developer-guide.md](docs/developer-guide.md).

```bash
dotnet run --project src/GameOfLife.Api --launch-profile http
```

The HTTP profile listens on `http://localhost:8080`. Scalar is at `/scalar` in Development. SQLite is created at `data/game-of-life.db` under the repository root, with its tables, on startup. Command-line steps are in the developer guide.

```bash
dotnet test
```

Tests use SQLite only. Integration tests use a temporary database file.

## Layout

| Project | Role |
|---|---|
| `src/GameOfLife.Domain` | Rules and grid. No I/O. |
| `src/GameOfLife.Application` | Use cases, ports, options. |
| `src/GameOfLife.Infrastructure` | SQLite via `Microsoft.Data.Sqlite`. |
| `src/GameOfLife.Api` | Minimal API, problem details, health, OpenAPI, Serilog, OpenTelemetry. |

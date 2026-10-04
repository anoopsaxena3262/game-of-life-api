# Game of Life API

Conway's Game of Life as a small HTTP service on .NET 10.

Upload a board, read any generation, and walk it to its final state: extinct, a fixed point, or a cycle. Boards and computed generations are stored in SQLite and survive a restart or a crash. Errors are RFC 7807 problem documents.

## Run

Setup from a machine that has never used .NET, and how to call the service, is in [docs/developer-guide.md](docs/developer-guide.md). The design and the reasons behind it are in [docs/design.md](docs/design.md).

```bash
dotnet run --project src/GameOfLife.Api --launch-profile http
```

The service listens on `http://localhost:8080`. Scalar is at `/scalar` in Development. On startup it creates `data/game-of-life.db` under the repository root, with its tables. `./restart.sh` asks whether to keep or delete that file first.

In a second terminal:

```bash
./try-it.sh
./try-all.sh
```

`try-it.sh` walks a blinker through every endpoint. `try-all.sh` runs every scenario in `scripts/` and stops at the first failure. `./try-restart.sh save`, then stop the service, start it again, then `./try-restart.sh check` shows a board surviving a restart. `requests.http` has the same calls one at a time.

## Test

```bash
dotnet test
```

Every test uses its own temporary SQLite file. No Docker or running service is needed.

## API

| Method | Path | Returns |
|---|---|---|
| `POST` | `/api/v1/boards` | `201`, `Location`, the board at generation 0 |
| `GET` | `/api/v1/boards/{id}` | The board at generation 0 |
| `GET` | `/api/v1/boards/{id}/next` | Generation 1 |
| `GET` | `/api/v1/boards/{id}/generations/{n}` | Generation n, up to 10000 |
| `GET` | `/api/v1/boards/{id}/final?maxGenerations=` | `EXTINCT`, `FIXED_POINT` or `CYCLE`, or `422` if the board does not conclude within the limit |

## Layout

| Path | Role |
|---|---|
| `src/GameOfLife.Domain` | Rules, state encoding, termination detection. No packages, no I/O. |
| `src/GameOfLife.Application` | `BoardService`, limits and their validation, the `IBoardRepository` port. |
| `src/GameOfLife.Infrastructure` | SQLite via `Microsoft.Data.Sqlite`: schema at startup, repository, health check. |
| `src/GameOfLife.Api` | Minimal API endpoints, problem details, request-size limit, health, OpenAPI, Serilog, OpenTelemetry. |
| `tests/` | One test project per layer, plus `GameOfLife.Benchmarks` for the engine step. |
| `try-it.sh`, `try-all.sh`, `scripts/` | HTTP scenarios against a running service. |
| `try-restart.sh`, `restart.sh` | The restart demo, and a start that asks about the existing database. |
| `docs/` | Developer guide and design. |

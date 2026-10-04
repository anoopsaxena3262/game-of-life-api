# Developer guide

Set up a machine that has never used C# or .NET, then run the Game of Life API.

The Game of Life API is a small web service. You show it from the command line by calling the API.

The service stores boards in SQLite and serves the board endpoints. The design and the reasons behind it are in [design.md](design.md). Day-to-day commands are here.

## What you are installing

Three pieces, and only the first is required to run the service:

| Piece | What it is | Required? |
|---|---|---|
| .NET SDK 10 | The compiler and the `dotnet` command. It includes the runtime that executes the program. | Yes |
| Git | Copies the repository onto your machine. | Only if you do not already have the folder |
| An editor | Visual Studio Code is enough. | Helpful, not required to run |

You do not need a database server, a container runtime or a paid IDE to run or test the service.

This repository asks for SDK **10.0.x**. `global.json` accepts any 10.0 patch. An older SDK, such as 8 or 9, will not build it.

## 1. Install the .NET 10 SDK

Pick the block for your system. Then come back to [Check the install](#2-check-the-install).

### macOS

Install the SDK package from [https://dotnet.microsoft.com/download/dotnet/10.0](https://dotnet.microsoft.com/download/dotnet/10.0). Choose **SDK**, not the runtime-only installer, and choose the build that matches your Mac (Apple silicon or Intel).

Homebrew does the same install:

```bash
brew install --cask dotnet-sdk
```

That installer asks for your Mac password. When it finishes, open a **new** terminal window so `dotnet` is on your path.

If the installer cannot get administrator rights, install it into your home directory instead:

```bash
curl -fsSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh
bash /tmp/dotnet-install.sh --channel 10.0 --install-dir "$HOME/.dotnet"
```

Add these two lines to `~/.zshrc`. The second line puts both `dotnet` and any global .NET tools on your path:

```bash
export DOTNET_ROOT="$HOME/.dotnet"
export PATH="$DOTNET_ROOT:$DOTNET_ROOT/tools:$PATH"
```

A terminal that is already open still has the old path. In that window run:

```bash
source ~/.zshrc
```

A new terminal loads `~/.zshrc` on its own.

### Windows

Download the **.NET SDK 10** x64 installer from the same page and run it. Close and reopen PowerShell or Terminal afterwards. The installer puts `dotnet` on your PATH.

Visual Studio 2026 also includes the SDK. You do not need Visual Studio if you only want to run the service.

### Linux

Follow the distro steps on [https://learn.microsoft.com/dotnet/core/install/linux](https://learn.microsoft.com/dotnet/core/install/linux) and install the **SDK 10** package, not only the runtime. On Ubuntu the package feed is published by Microsoft; the `dotnet-sdk-10.0` package name is the one you want.

## 2. Check the install

```bash
dotnet --version
```

You want a number that starts with `10.0`, for example `10.0.401`.

If the shell says `command not found` or `dotnet is not recognized`:

- The window you already have open still has the old path. Run `source ~/.zshrc`, or close it and open a new one.
- On macOS, if you used the home-directory installer, confirm `~/.zshrc` contains the two `export` lines above. `echo $DOTNET_ROOT` should print a folder that contains the `dotnet` program.

This extra check shows that the SDK can compile:

```bash
dotnet --list-sdks
```

A line containing `10.0` is the one this repo uses.

## 3. Get the code

If the project folder is already on your machine, skip this step. From the folder that should contain it:

```bash
git clone <repository-url> game-of-life-api
cd game-of-life-api
```

Every command after this one is run from that folder, the one that contains `GameOfLife.slnx` and `src/`.

## 4. Optional editor

Visual Studio Code plus two extensions is a comfortable setup if you have not used C# before:

1. Install [Visual Studio Code](https://code.visualstudio.com/).
2. Open this folder with **File → Open Folder**.
3. Install the **C#** extension (publisher Microsoft) when Visual Studio Code offers it. The C# Dev Kit is fine too.

You can ignore the editor and use only the terminal commands in the next section.

## Words you will see

| Word | Meaning here |
|---|---|
| Solution | `GameOfLife.slnx`. The list of projects that build together. |
| Project | One `.csproj` folder, such as `src/GameOfLife.Api`. A project compiles to one program or one library. |
| SDK | The tools. `dotnet build` and `dotnet run` come from it. |
| Restore | Downloads the libraries listed for the projects. The first build does this for you. |
| Build | Compiles C# into a program you can run. |
| Run | Starts the web service and leaves it running until you stop it. |

## Build the service

From the repository root, compile without starting the process:

```bash
dotnet build GameOfLife.slnx
```

The first build downloads libraries. That can take a minute. Later builds are faster. The last lines should say the build succeeded. This does not start the API. The next section does that, and `dotnet run` compiles again if anything changed.

## Run the service

From the repository root:

```bash
dotnet run --project src/GameOfLife.Api --launch-profile http
```

The first run downloads libraries and compiles. That can take a minute. Later runs are faster. Leave this terminal open. The service is running when the log contains:

```text
Now listening on: http://localhost:8080
```

Stop it with **Ctrl+C** in that same terminal.

What starts:

- The API listens on [http://localhost:8080](http://localhost:8080), under `/api/v1/boards`.
- Before it listens, the app logs the limits it uses, then creates `data/game-of-life.db` at the repository root (the `http` launch profile points there) and the `board` and `generation` tables if they are missing. The log shows `schema initialised successfully`. Existing boards are kept.

Leave that terminal running. The command-line demo and the scripts use this process.

To be asked whether to keep or delete the existing database first, start it with `./restart.sh` instead.

## Demo from the command line

Two terminals. The first one stays on the server. The second one is where you call the API. Do not type `curl` into the terminal that is printing logs.

Open a **second** terminal. Stay in the repository root.

Confirm the process is answering. An id that was never uploaded is a `404` problem document:

```bash
curl -i http://localhost:8080/api/v1/boards/00000000-0000-0000-0000-000000000000
```

### Try it

```bash
./try-it.sh
```

The script uploads a 3×3 blinker, reads the id from the `Location` header, and then calls `/next` twice, generation 10, `/final`, and `/final?maxGenerations=1`. You do not copy the id yourself.

What the output should show:

- Upload status `201` and the id.
- Both `/next` calls with the same JSON, `"generation": 1`, middle column live.
- Generation 10 matching the uploaded board (a blinker at an even index is back to generation 0).
- `/final` with `"terminationKind": "CYCLE"` and `"period": 2`.
- The last call with `HTTP 422`.

The first terminal prints one log line per call while the script runs. That is the server log, not a second prompt.

If the script says nothing is listening, the service in the first terminal has not printed `Now listening on: http://localhost:8080` yet.

### By hand

Upload a blinker (three live cells in a row):

```bash
curl -i -X POST http://localhost:8080/api/v1/boards \
  -H 'Content-Type: application/json' \
  -d '{"width":3,"height":3,"cells":[[false,false,false],[true,true,true],[false,false,false]]}'
```

The response is `201` with a `Location` header and a body that contains the `id`. Use it in the next calls:

```bash
ID=<the id from the upload>

curl -i http://localhost:8080/api/v1/boards/$ID
curl -i http://localhost:8080/api/v1/boards/$ID/next
curl -i http://localhost:8080/api/v1/boards/$ID/generations/10
curl -i http://localhost:8080/api/v1/boards/$ID/final
curl -i "http://localhost:8080/api/v1/boards/$ID/final?maxGenerations=1"
```

On PowerShell, use `curl.exe` so you get the same program. `requests.http` has the same calls one at a time; Visual Studio, Rider and the VS Code REST Client can send each block. Paste the id into its `@id` line.

`cells` is row-major: the outer array is rows, `true` is a live cell.

| Call | Status | What to look at |
|---|---|---|
| `POST /api/v1/boards` | 201 | `Location: /api/v1/boards/<id>`. Body is generation 0. |
| `GET /api/v1/boards/<id>` | 200 | Same cells as the upload. `generation` is 0. |
| `GET .../next` twice | 200 both times | Same body both times. `generation` is 1. For the blinker, the middle column is live. |
| `GET .../generations/10` | 200 | A blinker at an even index matches generation 0. |
| `GET .../final` | 200 | `terminationKind` is `CYCLE`, `period` is 2, `generationsLimit` is 1000. |
| `GET .../final?maxGenerations=1` | 422 | Problem title `No conclusion`, and `generationsAttempted` is 1. |
| `GET .../final?maxGenerations=0` | 400 | `maxGenerations` has to be at least 1. A value above 10000 is clamped, not rejected. |
| Unknown UUID | 404 | Title `Board not found`. |
| `"width": 0` | 400 | Title `Validation failed`, and the detail names `width`. A grid whose rows do not match the declared size is title `Invalid board`. A body that is not JSON is title `Bad Request`. |

Every error is a problem document (`application/problem+json`) with `type`, `title`, `status`, `detail` and `instance`.

No GET changes the stored board. Calling `/next` twice returns generation 1 both times. `/final` does not fill the generation cache.

## Scripts

These hit a service that is already running on port 8080. They are the demo. They are not a substitute for `dotnet test`. Set `BASE_URL` to point them at another address.

To run every scenario in one go:

```bash
./try-all.sh
```

That runs the happy path and then each file below, in that order. It prints `PASS` after each one and stops at the first failure. CI runs the same command against a freshly started service.

Each script reads the id from the upload itself. It prints the JSON and exits non-zero if the status, the cells, or the termination fields are wrong. `scripts/common.sh` is the shared curl helper. Do not run that file on its own. The scripts need `bash`, `curl` and `python3`.

| Script | Feature | What you should see |
|---|---|---|
| `./try-it.sh` | Upload, next, N generations, final, no conclusion | `CYCLE`, period 2, and `HTTP 422` when the limit is 1 |
| `./scripts/try-fixed-point.sh` | Still life | `/next` matches the upload. `FIXED_POINT`, period 1 |
| `./scripts/try-toad.sh` | Period-2 oscillator | `CYCLE`, period 2. Generation 2 matches the upload |
| `./scripts/try-beacon.sh` | Period-2 oscillator | `CYCLE`, period 2. Generation 2 matches the upload |
| `./scripts/try-glider.sh` | Finite board, dead borders | Generation 4 has moved. `/final` is `FIXED_POINT` |
| `./scripts/try-large-glider.sh` | Cell-generation budget, and a glider that outlives the default walk | `/generations/56` is `HTTP 400`. Default `/final` is `HTTP 422` with `generationsAttempted` 1000. `maxGenerations=10000` is `FIXED_POINT` at generation 1192, `generationsLimit` 10000 |
| `./scripts/try-empty.sh` | Extinction | `EXTINCT` |
| `./scripts/try-single-cell.sh` | Underpopulation | `/next` is all dead. `EXTINCT` |
| `./scripts/try-full-board.sh` | Collapse to extinction | `/next` is the four corners. `EXTINCT` |
| `./scripts/try-one-by-one.sh` | 1×1 grids | Both end `EXTINCT`. The live cell's `/next` is dead |
| `./scripts/try-one-by-n.sh` | 1×N grids | `/next` kills the cells at each end |
| `./scripts/try-not-found.sh` | Unknown id | `HTTP 404`, title `Board not found` |
| `./scripts/try-invalid-board.sh` | Malformed upload | `"width": 0` returns `HTTP 400`, title `Validation failed` |
| `./scripts/try-mismatched-board.sh` | Cells do not match the declared size | `HTTP 400`, title `Invalid board` |
| `./scripts/try-bad-generation.sh` | Generation index below 0 | `HTTP 400` |
| `./scripts/try-bad-limit.sh` | Non-positive `maxGenerations` | `HTTP 400`, not 422 |
| `./scripts/try-bad-id.sh` | Path id is not a UUID | `HTTP 400` |
| `./scripts/try-limit-clamped.sh` | Caller limit above the ceiling | Still `CYCLE`. `generationsLimit` is 10000 |
| `./scripts/try-plus.sh` | Cycle with a lead-in | `firstOccurrenceGeneration` 4, `generationsComputed` 6 |
| `./scripts/try-oversized.sh` | Board over `MaxCells` | `HTTP 400`, title `Invalid board` |
| `./scripts/try-generation-ceiling.sh` | `/generations/10001` | `HTTP 400` |
| `./scripts/try-bad-json.sh` | Body is not JSON | `HTTP 400`, title `Bad Request` |
| `./scripts/try-missing-cells.sh` | No `cells` field | `HTTP 400`, title `Validation failed` |
| `./scripts/try-null-cell.sh` | A null cell | `HTTP 400`. Not stored as dead |

`./try-all.sh` does not stop the server. Restart and crash are a separate two-step demo, because the process has to die in between.

### Restart and crash

With the service running:

```bash
./try-restart.sh save
```

That uploads a blinker and asks for generation 10, so the file holds the board and generations 0 through 10. Then stop the service. Ctrl+C is a normal shutdown. `kill -9` of the process on port 8080 is the crash. Start it again with `dotnet run --project src/GameOfLife.Api --launch-profile http`, then:

```bash
./try-restart.sh check
```

The same id returns the uploaded blinker. If `sqlite3` is installed, the script also reads `data/game-of-life.db` and requires indexes 0 through 10 to still be there. It does that before any call that would recompute a missing generation. The id is stored in `data/restart-demo.id`, which is not source.

`RestartPersistenceTests` in `tests/GameOfLife.Api.IntegrationTests` is the same proof with no manual stop. It uses its own database file, so it does not need the server on 8080 to be down.

### Start fresh

```bash
./restart.sh
```

It stops whatever is listening on 8080 (after asking), asks whether to keep or delete `data/game-of-life.db`, then starts the service in that terminal.

## Run the tests

Stop the service first, or use another terminal. From the repository root:

```bash
dotnet test
```

That builds every project and runs the four test projects. They do not use the server you started with `dotnet run`, and they do not touch `data/game-of-life.db`: every test that needs a database uses its own temporary file. Every test is expected to pass; none are skipped.

Run one class, or one test, when a failure is in a single layer:

```bash
dotnet test --filter "FullyQualifiedName~StateCodecTests"
dotnet test --filter "FullyQualifiedName~LifeEngineTests.Block_is_a_still_life_unchanged_after_a_step"
dotnet test --filter "FullyQualifiedName~BoardApiTests.Get_final_returns_422_when_the_generation_limit_is_exceeded"
```

Coverage, with the collector the test projects already reference:

```bash
dotnet test --collect:"XPlat Code Coverage"
```

Each test project writes a `coverage.cobertura.xml` under its `TestResults` folder.

The suite is layered. Read a failure in this order, because a red HTTP test can be a rules bug, a SQL bug, or an HTTP bug, and the lower layer tells you which:

1. `GameOfLife.Domain.Tests`: `StateCodecTests`, `LifeEngineTests`, `TerminationDetectorTests`. No host, no database. These pin the `0`/`1` encoding, B3/S23, and fixed point / cycle / extinction / give-up, including a forced hash collision and a cycle confirmed from a checkpoint.
2. `GameOfLife.Infrastructure.Tests`: SQLite in a temporary file. Save, read, idempotent generation insert, highest cached index, the stored schema, foreign keys, and a restart of the storage services on the same file.
3. `GameOfLife.Application.Tests`: `BoardServiceTests` (validation, cache hit, resume, limit clamp, non-positive `maxGenerations`) over an in-memory fake repository, and `GameOfLifeOptionsTests`.
4. `GameOfLife.Api.IntegrationTests`:
   - `ApiProblemsTests`: status, title and the `generationsAttempted` field for each error. No host.
   - `BoardApiTests` and `ApiParityTests`: the whole API in memory with its own temporary database file. Every endpoint, plus 400, 404, 415, 422, the problem document shape, a body over the size cap, and a chunked body that crosses the cap inside `cells`.
   - `RestartPersistenceTests`: start the API host, write a board and generations 0 through 4, dispose it, start a second host on the same file, and read the rows back.

The API tests and the restart test boot the application. They are the wrong place to learn a single rule or a single SQL statement.

## Configuration

Limits are in the `GameOfLife` section of `src/GameOfLife.Api/appsettings.json`:

| Key | Default | What it limits |
|---|---|---|
| `MaxGenerations` | 1000 | The `/final` walk when the caller does not pass `maxGenerations`. |
| `MaxGenerationsCeiling` | 10000 | The largest `/generations/{n}` index and the largest `maxGenerations`. A larger `maxGenerations` is clamped. Must be at least `MaxGenerations`. |
| `MaxCells` | 90000 | `width * height` of an upload (300 per side). |
| `MaxCellGenerations` | 5000000 | Cells × generations for one `/generations/{n}` computation. |
| `MaxRequestBytes` | 2000000 | The request body, checked before the grid is built. |

A value below 1, or a ceiling below `MaxGenerations`, stops startup with an `OptionsValidationException` that names the key. The startup log prints the limits in use.

Override any of them for one run on the command line, or with an environment variable (`GameOfLife__MaxCells=10000`):

```bash
dotnet run --project src/GameOfLife.Api --launch-profile http -- --GameOfLife:MaxGenerations=5000
```

The connection string is `ConnectionStrings:Default`. The launch profiles set it to `Data Source=../../data/game-of-life.db;Default Timeout=5`, which is `data/game-of-life.db` under the repository root, because `dotnet run` starts in the project folder. `Default Timeout=5` is how long a write waits for another writer before it fails.

## SQLite

The application does not use a separate database server. It opens a file.

The file appears the first time the app starts. The tests do not open it. Open the shell from the repository root:

```bash
sqlite3 data/game-of-life.db
```

Useful commands inside the prompt:

```sql
.tables
.schema
.headers on
.mode column
```

`.tables` lists names. `.schema` prints the `CREATE` statements, with a comment on every column. Leave with `.quit`.

The schema is two tables:

`board`: one row per upload.

| Column | Meaning |
|---|---|
| `id` | UUID, stored as lowercase text. Same value the API returns. |
| `width`, `height` | Fixed at upload. |
| `initial_state` | Generation 0 as a flat `0`/`1` string, row by row. |
| `created_at` | ISO-8601 UTC text, for example `2026-01-02T03:04:05.12Z`. |
| `max_generations` | Optional stored cap. The upload API leaves this NULL. NULL means `/final` uses the query parameter or `GameOfLife:MaxGenerations` at request time. |

`generation`: memoised states. Primary key is `(board_id, idx)`.

| Column | Meaning |
|---|---|
| `board_id` | Foreign key to `board.id`. |
| `idx` | Generation number. `0` is the upload. |
| `state` | Same `0`/`1` encoding as `initial_state`. |

A 3×3 horizontal blinker is the string `000111000`.

A board and every cached generation:

```sql
SELECT b.id, b.width, b.height, g.idx, g.state
FROM board b
LEFT JOIN generation g ON g.board_id = b.id
ORDER BY b.created_at, g.idx;
```

`SELECT MAX(idx) FROM generation WHERE board_id = '…' AND idx > 0` is how the service finds where to resume. `NULL` means nothing beyond generation 0 is cached.

`PRAGMA journal_mode;` reports `wal`. That setting is stored in the file. `PRAGMA foreign_keys;` in this shell reports `0`. That is expected: foreign keys are off on every new SQLite connection, and the shell is not the application's connection. The application turns them on for each connection it opens.

Do not edit rows by hand while the service is running. Generation rows are immutable for a given board and index, and the service inserts them with `INSERT OR IGNORE`.

To wipe local data, stop the service, then:

```bash
rm -f data/game-of-life.db data/game-of-life.db-wal data/game-of-life.db-shm
```

Or run `./restart.sh` and choose 2.

## Logging

Logs go to the console, one line per entry. Levels are in the `Logging:LogLevel` section of `appsettings.json`; `GameOfLife` is the service's own code. The default is Information. Grids are not logged; open the database if you need the `0`/`1` string.

| Level | What you get |
|---|---|
| Information | Startup, the limits, schema ready, each HTTP call, a saved board, and how `/final` concluded (kind, period, generations). A 422 is Information. It is a normal result. |
| Warning | A 400 or 404, a caller limit clamped to the ceiling, or a generation cache that has a later row but not the one requested. |
| Debug | Repository calls with ids and indexes, cache hits, and the index a walk resumed from. |

Debug for the service's own code, for one run:

```bash
dotnet run --project src/GameOfLife.Api --launch-profile http -- --Logging:LogLevel:GameOfLife=Debug
```

Or set `"GameOfLife": "Debug"` under `Logging:LogLevel` in `appsettings.json`.

## When something fails

**`dotnet` is not found.** The shell prints `zsh: command not found: dotnet` when `~/.dotnet` is not on `PATH`. On a home-directory install, add the two `export` lines from the macOS section to `~/.zshrc`, then run `source ~/.zshrc` in that same terminal. `echo $DOTNET_ROOT` should print a folder that contains `dotnet`.

**The SDK is older than 10.** `dotnet --version` must start with `10.0`. Install the SDK 10 package beside the older one. `dotnet --list-sdks` can show several versions; this repo selects 10.

**Address already in use, port 8080.** Another copy is still running. Go to that terminal and press Ctrl+C, or find the process and stop it: `lsof -nP -iTCP:8080 -sTCP:LISTEN`. `./restart.sh` offers to stop it for you.

**A script says nothing is listening.** The `dotnet run` terminal must still be open and must say `Now listening on: http://localhost:8080`. Use `http://localhost:8080`.

**Startup fails before `Now listening`.** Read the `dotnet run` log above the error. The usual cause is that the process cannot create `data/` or cannot write `data/game-of-life.db`.

**Startup stops with `OptionsValidationException`.** A `GameOfLife` limit is below 1, or `MaxGenerationsCeiling` is below `MaxGenerations`. The message names the key.

**Restore or build cannot download packages.** The first build needs a network connection to NuGet. Retry `dotnet build` from the repository root.

**`/final` returned 422.** The board did not reach a fixed point or a cycle within the limit. The body's `generationsAttempted` says how far the walk went. Retry with a larger `maxGenerations`, up to 10000.

## Where to look next

[design.md](design.md) records the design: section 3 for the project boundaries, section 5 for the HTTP contract, section 6 for which test covers which requirement, section 10 for every difference from the Java implementation. Change `GameOfLife.Domain` when the cells are wrong. Change `BoardService` when a cache or a limit is wrong. Change `BoardEndpoints` or `ApiProblems` when the status or the JSON shape is wrong.

# Developer guide

Set up a machine that has never used C# or .NET, then run the Game of Life API.

The Game of Life API is a small web service. You show it from the command line by calling the API.

The service stores boards in SQLite and serves the board endpoints, the health checks and the API docs page. The sections below tell you what to expect from each.

## What you are installing

Three pieces, and only the first is required to run the service:

| Piece | What it is | Required? |
|---|---|---|
| .NET SDK 10 | The compiler and the `dotnet` command. It includes the runtime that executes the program. | Yes |
| Git | Copies the repository onto your machine. | Only if you do not already have the folder |
| An editor | Visual Studio Code is enough. | Helpful, not required to run |

You do not need Docker or a paid IDE to run or test the service.

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

- The API listens on [http://localhost:8080](http://localhost:8080), under `/api/v1` and `/health`.
- Before it listens, the app creates `data/game-of-life.db` at the repository root (the `http` and `https` launch profiles point there) and the `board` and `generation` tables if they are missing. The log shows `schema initialised successfully`. Existing boards are kept.
- API docs (Scalar) are at [http://localhost:8080/scalar](http://localhost:8080/scalar).
- The raw OpenAPI document is at [http://localhost:8080/openapi/v1.json](http://localhost:8080/openapi/v1.json).

Leave that terminal running. The command-line demo uses this process.

A second profile also listens on `https://localhost:7001`. Skip it until you need HTTPS. The first time you use it, trust the local development certificate:

```bash
dotnet dev-certs https --trust
```

macOS asks for your keychain password. The demos below use `http://localhost:8080`.

## Demo from the command line

Open a **second** terminal. Stay in the repository root.

Confirm the process and the database:

```bash
curl -i http://localhost:8080/health/live
curl -i http://localhost:8080/health/ready
```

Both should return `200` and the body `Healthy`.

`/health/live` means the process is up. `/health/ready` means it can open the database. A ready check that is not `200` means the database file could not be opened.

The four board calls are below. Every error is a problem document (`application/problem+json`) with `type`, `title`, `status`, `detail` and `instance`.

Upload a blinker (three live cells in a row):

```bash
curl -i -X POST http://localhost:8080/api/v1/boards \
  -H 'Content-Type: application/json' \
  -d '{"width":3,"height":3,"cells":[[false,false,false],[true,true,true],[false,false,false]]}'
```

The response is `201` with a `Location` header and a body that contains the `id`. Use it in the next three calls:

```bash
ID=<the id from the upload>

curl -i http://localhost:8080/api/v1/boards/$ID/next
curl -i http://localhost:8080/api/v1/boards/$ID/generations/4
curl -i http://localhost:8080/api/v1/boards/$ID/final
```

On PowerShell, use `curl.exe` so you get the same program, or send the requests from `requests.http`. In Visual Studio Code, the REST Client extension can send each block in that file.

`cells` is row-major: the outer array is rows, `true` is a live cell. The example above is a 3 by 3 board.

| Call | What it asks |
|---|---|
| `POST /api/v1/boards` | Store this starting board and return its id. |
| `GET /api/v1/boards/{id}/next` | The board one generation later. |
| `GET /api/v1/boards/{id}/generations/4` | The board four generations after upload. |
| `GET /api/v1/boards/{id}` | The uploaded board, generation 0. |
| `GET /api/v1/boards/{id}/final` | The state where the board concludes, with `terminationKind` (`EXTINCT`, `FIXED_POINT` or `CYCLE`), `period` and `generationsLimit`. `422` if it does not conclude within the limit. A blinker is a `CYCLE` with period 2. Pass `maxGenerations` to change the limit, up to 10000. |

## Run the tests

Stop the service first, or use another terminal. From the repository root:

```bash
dotnet test
```

That builds every project and runs the test projects. The default run uses SQLite in a temporary file. It does not need Docker. Every test is expected to pass; none are skipped.

## Optional: run it in Docker

Install Docker Desktop, then from the repository root:

```bash
docker compose up --build api
```

The API is again at [http://localhost:8080](http://localhost:8080). The database file lives in a Docker volume named `game-of-life-data`, so it survives a container restart. The service runs on SQLite only.

## When something fails

**`dotnet` is not found.** The shell prints `zsh: command not found: dotnet` when `~/.dotnet` is not on `PATH`. On a home-directory install, add the two `export` lines from the macOS section to `~/.zshrc`, then run `source ~/.zshrc` in that same terminal. `echo $DOTNET_ROOT` should print a folder that contains `dotnet`.

**The SDK is older than 10.** `dotnet --version` must start with `10.0`. Install the SDK 10 package beside the older one. `dotnet --list-sdks` can show several versions; this repo selects 10.

**Address already in use, port 8080.** Another copy is still running. Go to that terminal and press Ctrl+C, or find the process and stop it. On macOS or Linux: `lsof -i :8080`.

**The browser cannot connect.** The `dotnet run` terminal must still be open and must say it is listening on port 8080. Use `http://` not `https://` for that profile.

**`/health/ready` is not Healthy.** Read the `dotnet run` log above the error. The usual cause is that the process cannot create `data/` or cannot write `data/game-of-life.db`.

**Restore or build cannot download packages.** The first build needs a network connection to NuGet. Retry `dotnet build` from the repository root.

**`/final` returned 422.** The board did not reach a fixed point or a cycle within the limit. The body's `generationsAttempted` says how far the walk went. Retry with a larger `maxGenerations`, up to 10000.

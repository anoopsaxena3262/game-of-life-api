# Porting plan: Java to .NET

How this service was ported from the Java 25 / Spring Boot implementation, what was decided along the way, the risks, and how each was verified. It records the plan as it was carried out, not as it was first drafted. The design itself is in [design.md](design.md); every remaining difference from the Java implementation is in its section 10.

## Goal

A .NET 10 / C# version of the Game of Life service that:

- lives in this repository, built from its original .NET scaffold;
- uses the same SQLite schema, with each service keeping its own database file;
- answers every HTTP request the same way: endpoints, JSON, status codes, problem titles and details;
- keeps the demo scripts, copied into this repository and adapted for .NET;
- has no dependency on the Java repository: no shared files, links, submodules or scripts.

The working rule: match the Java implementation, and deviate only where .NET cannot do the same thing; document each deviation and why.

## Phases

| Phase | Pull request | What it delivered |
|---|---|---|
| 0. Baseline | [#1](https://github.com/anoopsaxena3262/game-of-life-api/pull/1) | Scaffold committed as a baseline. EF Core, migrations and the Postgres provider removed; SQLite only. |
| 1. Domain | [#2](https://github.com/anoopsaxena3262/game-of-life-api/pull/2) | `StateCodec`, `LifeEngine` (B3/S23, dead borders), `TerminationDetector` (fingerprint plus checkpoint confirmation), `Board`. 28 tests. |
| 2. Persistence | [#3](https://github.com/anoopsaxena3262/game-of-life-api/pull/3) | `SchemaInitializer` (`board` and `generation`, created at startup), `SqliteBoardRepository`, foreign keys and busy timeout on every connection, database at `data/` under the repository root. 13 tests. |
| 3. Service | [#4](https://github.com/anoopsaxena3262/game-of-life-api/pull/4) | `BoardService` (cache, resume, limits), the three domain exceptions, `GameOfLifeOptions` with startup validation, an application-level restart test. |
| 4. API | [#5](https://github.com/anoopsaxena3262/game-of-life-api/pull/5) | The five endpoints, problem documents, request-size limit, `terminationKind` as `CYCLE` and so on. |
| 5. Scripts and docs | [#6](https://github.com/anoopsaxena3262/game-of-life-api/pull/6) | `try-it.sh`, `try-all.sh`, `try-restart.sh`, `restart.sh`, `scripts/`, `requests.http` adapted; CI runs them; developer guide and design. |
| Scaffold clean-up | [#7](https://github.com/anoopsaxena3262/game-of-life-api/pull/7) | Docker, OpenTelemetry, OpenAPI/Scalar, health checks, the HTTPS profile, Serilog, Benchmarks and FsCheck removed; design section 10. |
| 6. Verification | — | A fresh clone of `main` built, tested and demonstrated; no fixes needed. |
| Review | [#8](https://github.com/anoopsaxena3262/game-of-life-api/pull/8) | A paired run against the Java service (below) found and fixed the gaps listed under "Found in review". |
| Parity report | this change | A fresh, stricter side-by-side run, written up request by request in [parity-report.md](parity-report.md). It found two more differences in `instance`, both fixed. |

## Decisions that changed during the work

| Planned | Done | Why |
|---|---|---|
| Dapper for data access | Plain ADO.NET (`Microsoft.Data.Sqlite`) | Five short statements did not justify another dependency. |
| Scripts unchanged; run against either service with `BASE_URL` | Scripts copied and adapted; only start commands, doc paths and config names in comments changed | No relationship between the two repositories. |
| Keep the scaffold's Docker, telemetry, OpenAPI and health checks | Removed | Not in the Java implementation, and the telemetry console exporter was about 84% of the development console output. |
| Serilog | Built-in console logger | The platform equivalent of Spring Boot's default logging; `Logging:LogLevel` then works like `logging.level`. |
| Framework binding for path, query and body | Values parsed in the endpoints with the reference rules | Framework binding answers differently (404 for a malformed GUID, an empty 400 for a bad body) and is stricter about JSON. |
| Restart test at the infrastructure level | Both: infrastructure-level and application-level | The application-level test needed `BoardService` (phase 3). |

## Risk register

| Risk | Status | Mitigation | Verified by |
|---|---|---|---|
| Error responses differ from the Java service (status, title, detail, instance, content type) | Closed | Endpoints parse values themselves; `ProblemWriter` writes every error; framework 404/405/406/415 details reproduced | [parity-report.md](parity-report.md); `ApiParityTests`, `EdgeCaseParityTests`, `ProblemWriterTests` |
| Lenient input accepted by Java is rejected by .NET (or the reverse) | Closed | `SpringConversions`, `LenientBooleanConverter`, `LenientInt32Converter`, trailing JSON content ignored | Same paired run; `SpringConversionsTests`, `EdgeCaseParityTests` |
| Stored data differs although the DDL matches (id case, timestamp format, NULL handling) | Closed | Ids bound as lowercase text, `created_at` as ISO-8601 UTC with `Z`, NULL kept as NULL | `SqliteBoardRepositoryTests`; Java and .NET databases compared column by column: identical structure, DDL text identical except one comment |
| Board lost on restart or crash | Closed | WAL, transaction for board plus generation 0, schema created before listening | Both restart tests; `./try-restart.sh` with `kill -9` locally and in CI on every push |
| Concurrent writes fail with `SQLITE_BUSY` | Closed | `Default Timeout=5`, `INSERT OR IGNORE` for cache rows | 40 parallel reads of one uncached generation: one identical body, no errors; 20 parallel uploads: 20 ids. Same result from the Java service |
| `/final` too slow, or slower than Java | Accepted | Same algorithm; fingerprint map plus checkpoints | 300×300 glider to the ceiling: 1.5 s in Release, 2.5 s in the Java service. `dotnet run` builds Debug (about 5.5 s); documented |
| CPU-bound `/final` blocks threads under load | Accepted | Same exposure as the Java servlet threads | 4 parallel ceiling walks completed with no errors in both |
| Database created in the wrong folder | Closed | Launch profile points at `../../data/` | Fresh clone: `data/game-of-life.db` at the repository root; scripts read it there |
| Scripts behave differently on Linux | Closed | Request bodies sent to curl on stdin (a 300×300 board is over Linux's 128 KB argument limit) | CI on `ubuntu-latest` runs all 24 scenarios and the restart |
| Hidden dependencies after removing packages | Closed | `GameOfLife.Infrastructure` declares the abstractions it uses | Release build with warnings as errors |
| The .NET repository depends on the Java one | Closed | Scripts and docs copied and adapted | No paths, links or scripts refer to the Java repository; it is named only where differences are documented |
| A difference nobody tested | Open, small | Paired run covered the HTTP surface broadly | See "Not verified" |

## Found in review

The paired run against the Java service found these, all fixed in this change:

- **A 500 that leaked the exception text.** An error response for a client whose `Accept` header excluded JSON (for example `Accept: application/xml` on an unknown id) failed inside the problem-details writer and returned 500 with the exception message. Every error is now written as `application/problem+json`, as the Java service does.
- **Lenient input.** The Java service accepts `1`/`0` and `"true"` as cells, `1.0` and `"1"` as a width, content after the JSON object, hexadecimal and space-padded numbers in the path and query, short-form UUIDs, and the first of a repeated `maxGenerations`. A missing `width` is `Bad Request`, not `Validation failed`.
- **HTTP behaviour.** `HEAD` and `OPTIONS`, the `Allow` header on a 405, case-sensitive routes, a trailing slash as 404, `;parameters` in a path, 406 for an unacceptable `Accept`, and the `detail` texts for 404, 405 and 415.
- **Listening address.** The Java service listens on every interface on port 8080. The .NET service listened on loopback only, and on port 5000 when started without the launch profile. It now listens on `*:8080` from `appsettings.json`.
- **Stored DDL indentation**, now identical.
- **`instance` in a problem document** (found by the parity-report run): it is now the path as the client sent it, still percent-encoded, and it is left out of the size-limit answer for a declared `Content-Length`, as in the Java service.

## Verification record

| Check | Result |
|---|---|
| `dotnet build -c Release` (warnings are errors) | 0 warnings |
| `dotnet test` | 218 passed, none skipped: Domain 28, Application 23, Infrastructure 13, API 154 |
| `./try-all.sh` against `dotnet run` | 24 of 24 scenarios, locally and in CI |
| `./try-restart.sh` across `kill -9` | Board and generations 0–10 survive, locally and in CI |
| `./restart.sh` keep / delete | Board kept (200) / removed (404) |
| Paired run against the Java service ([parity-report.md](parity-report.md)) | 81 of 81 requests identical, 152 of 153 probes (the other is a Java `detail` that cannot be reproduced), 33 of 33 scenario reads. Compared: status, media type, problem `type`, `title`, `detail`, `instance` and extensions, success body, `Allow` |
| Schema compared with the Java database | Columns, types, constraints, keys, indexes and journal mode identical |
| Concurrency and timing | As in the risk register |
| Coverage (`dotnet test --collect:"XPlat Code Coverage"`) | Domain 99%, Application 100%, Infrastructure 97% |
| Fresh clone of `main` | Builds, tests and demonstrates with no local files |

The paired run was a one-off check, made against a scratch copy of the Java repository. Nothing in this repository depends on it; the regression tests carry the expected values.

## Not verified

- **The 500 response shape.** The only 500 path, a cache row missing under a stored board, cannot be reached through the API, so it was not compared. The .NET service answers with a problem document; Spring Boot's default error body is shaped differently (design section 10).
- **Non-ASCII digits** in `n` and `maxGenerations`, and a percent-encoded `;` in a path. The Java service may accept inputs the .NET one rejects; no client sends them by accident.
- **Windows.** The scripts need `bash`, `curl` and `python3`, as they did for the Java service.

## Operating notes

- The service listens on every network interface on port 8080, as the Java service does. Set `Urls` to `http://localhost:8080` to restrict it to this machine.
- `dotnet run` builds Debug. Use `dotnet run -c Release` for timing.

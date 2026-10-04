# Design: Game of Life API

**Stack:** C# · .NET 10 · ASP.NET Core minimal APIs · SQLite (`Microsoft.Data.Sqlite`) · xUnit

This document records the design of the service and the reasoning behind each decision. Read it alongside the code. Day-to-day commands are in [developer-guide.md](developer-guide.md).

---

## 1. Requirements

### 1.1 Functional

| # | Capability | Notes |
|---|---|---|
| F1 | Upload a new board state, return board id | Id is server-generated |
| F2 | Get the next state for a board | One generation forward |
| F3 | Get the state N generations away | N supplied by caller |
| F4 | Get the final state | Error if no conclusion within a bounded number of attempts |

### 1.2 Non-functional

The service has to survive a restart or a crash and still have the boards. The code has to be complete, and that completeness has to be something you can show. Authentication and authorisation are not part of this service.

- **Durability.** Board rows and cached generations are committed to a SQLite file in WAL mode. Stopping the process with Ctrl+C, or killing it, leaves that file. The next process opens the same file. `RestartPersistenceTests` starts the API host, writes a board and generations 0 through 4, disposes the host, starts a second host on the same file, and reads the rows back. It does not recompute a missing cache, so a lost write fails the test. `./try-restart.sh save`, then stop the server, then `./try-restart.sh check`, is the same proof against a process started with `dotnet run`. CI runs it with `kill -9`.
- **Completeness.** Every behaviour in this document has a named test in §6. `./try-all.sh` runs the HTTP scenarios against a live process and stops on the first failure; CI runs it on every push. `dotnet test` is the full suite, including rules, storage, and the restart.
- **No authentication or authorisation.** There is no login and no token on any endpoint. Callers who can reach the port can call the API.
- Deterministic results, predictable performance on boards up to 300 cells per side, and clear error semantics. The caps that enforce that are in §1.4.

### 1.3 Out of scope

Authentication, authorisation, multi-tenancy, board deletion and listing, UI, horizontal scale-out, streaming updates. Each omission is deliberate; §7 notes where the extension points are.

### 1.4 Resource bounds

`/generations/{n}` and `/final` spend different resources, so they have different caps. All five are in the `GameOfLife` configuration section and are validated at startup.

| Limit | Default | What it stops |
|---|---|---|
| `MaxCells` | 90,000 (300 per side) | An upload whose `width * height` is larger. Checked after the body is parsed. |
| `MaxRequestBytes` | 2,000,000 | A body larger than that, before the grid is built. A declared `Content-Length` over the cap is rejected without reading the body. A body with no declared length is counted as it is read, so a chunked upload is stopped as soon as it crosses the cap. Both are `Request too large`. |
| `MaxGenerations` | 1,000 | The default `/final` walk when the caller does not pass `maxGenerations` and the board has no stored cap. |
| `MaxGenerationsCeiling` | 10,000 | The largest index `/generations/{n}` will compute, and the largest `maxGenerations` a caller can ask for. A higher `maxGenerations` is clamped, not rejected. The ceiling must be at least the default, so a mis-set ceiling cannot silently shrink the default. |
| `MaxCellGenerations` | 5,000,000 | Cells × generations for one `/generations/{n}` computation. That endpoint writes one state string per step. A 300×300 board can store `floor(5_000_000 / 90_000) = 55` generations. |

A state is a string of length `width * height`. `/final` writes nothing. It keeps a fingerprint of each step and compares full strings only when two fingerprints match (§4.3). Confirming a match replays from the nearest checkpoint, every 256 generations, rather than from generation 0. Its cap is the generation ceiling: a caller who asks for more than 10,000 is clamped to 10,000, and a caller who asks for nothing walks the default of 1,000. A glider in the corner of a 300×300 board is still moving at generation 1,000, so the default `/final` is a 422; asking for up to the ceiling reaches the corner still life at generation 1,192. That walk takes about 1.5 s in a Release build and about 5.5 s under `dotnet run`, which builds Debug (§8). The cell-generation budget does not apply to `/final`.

There is no request-processing timeout. A `/generations` walk that fits the budget, and a `/final` walk inside the generation ceiling, run to the end.

A null cell is rejected. `[[null, true]]` does not deserialise into `bool[][]`, so it is a 400 rather than a board with that cell stored as dead.

---

## 2. Design decisions

The specification leaves several behaviours open. This section records how each was resolved and why.

| Question | Decision | Rationale |
|---|---|---|
| Grid topology: finite, infinite, or toroidal? | **Finite, fixed-size grid with dead borders.** Width and height are fixed at upload. | Bounded memory and a bounded state space, which makes cycle detection tractable. |
| What constitutes a "final state"? | A board has concluded when it reaches a **fixed point** (the next generation is identical) or enters a **cycle** (any previously seen generation recurs). The response says which. | Oscillators such as the blinker never become still. Defining conclusion as stillness alone would classify them as non-terminating. |
| Gliders, which translate indefinitely on an unbounded grid | On a finite grid with dead borders a glider reaches the edge and settles, producing a fixed point. | A consequence of the topology decision, recorded so the behaviour is not surprising. |
| How many attempts before giving up? | A configured `MaxGenerations` (default 1000), overridable per request up to a server ceiling. Exceeding it returns 422. | Reaching the limit is a documented outcome, not a server fault, so it is a 4xx rather than a 500. |
| Do generation queries mutate the board? | **No.** Fetching generation N is a pure read; the stored board is never advanced by a query. | Keeps the API idempotent and safely cacheable. |
| Board id format | Server-generated random UUID. Never client-supplied. | Avoids collisions and enumeration of other callers' boards. |
| Input format | JSON with `width`, `height`, and `cells` as a row-array of booleans. | One fully specified format, readable in tests and in the example requests. |

---

## 3. Architecture

One ASP.NET Core service in four projects. The simulation engine is independent of the web framework and of persistence.

```
┌──────────────────────────────────────────────┐
│ GameOfLife.Api        minimal API endpoints, │
│                       DTOs, problem details, │
│                       request-size limit     │
├──────────────────────────────────────────────┤
│ GameOfLife.Application BoardService:         │
│                       orchestration, cache,  │
│                       limits; options;       │
│                       IBoardRepository port  │
├──────────────────────────────────────────────┤
│ GameOfLife.Domain     Board, LifeEngine,     │
│                       StateCodec,            │
│                       TerminationDetector.   │
│                       No packages, no I/O.   │
├──────────────────────────────────────────────┤
│ GameOfLife.Infrastructure                    │
│                       SqliteBoardRepository, │
│                       SchemaInitializer      │
└──────────────────────────────────────────────┘
                      │
                 SQLite file
```

`GameOfLife.Domain` references no packages and does not log. The rules are testable in microseconds with no host. `Directory.Build.props` turns off transitive project references, so each project names exactly what it uses.

`IBoardRepository` is the only single-implementation interface. It exists because storage is the stated extension point (§3.1), not as a reflex.

### 3.1 Persistence: SQLite

The deciding factor was what it costs to run this project. A server-backed database would mean installing it, starting a container, and configuring credentials before a single endpoint could be exercised. SQLite is a file. Clone the repository, run one command, and the service is up.

That is not a compromise on the durability requirement. SQLite is ACID, with WAL mode enabled here; killing the process mid-request leaves the last committed state on disk.

**Known limitation:** SQLite is single-writer and file-local, so this design does not scale horizontally as written. `IBoardRepository` is the seam. Moving to a server-backed store means a new implementation of that interface and a registration change, with no impact on the domain, the service or the endpoints.

Data access is plain ADO.NET through `Microsoft.Data.Sqlite`. Two tables and five statements do not justify an ORM, and `INSERT OR IGNORE` is the write the cache needs.

### 3.2 Data model

**`board`**

| column | type | notes |
|---|---|---|
| `id` | TEXT PK | UUID, lowercase, hyphenated |
| `width`, `height` | INTEGER | immutable after creation |
| `initial_state` | TEXT | generation 0, flat `0`/`1` string |
| `created_at` | TEXT | ISO-8601 UTC with a trailing `Z` |
| `max_generations` | INTEGER NULL | honoured when set; the upload API always stores NULL |

**`generation`**: the memoisation cache

| column | type | notes |
|---|---|---|
| `board_id` | TEXT FK → `board.id` | |
| `idx` | INTEGER | generation number |
| `state` | TEXT | flat `0`/`1` string |
| | | PK `(board_id, idx)` |

Caching computed generations makes repeated reads a lookup after the first computation, and means work already done is not repeated after a restart. `SaveAsync` writes the board and its generation 0 in one transaction, so a crash cannot leave a board without its generation 0 row.

Termination is not stored. `GET /final` always walks from generation 0 in memory. It does not read or write the generation table, so calling `/final` twice walks twice. The walk is bounded by the generation cap (§1.4).

`max_generations` is a stored cap the service honours when the request does not pass its own. `POST /boards` does not accept one, so the column is NULL for every board created through the API. NULL means "use `GameOfLife:MaxGenerations` at request time". Changing that setting changes `/final` for boards already stored.

`SchemaInitializer` creates the schema at startup with `CREATE TABLE IF NOT EXISTS`, before the server listens. Each column carries a comment in the stored DDL, so `.schema` in the `sqlite3` shell explains the file. A migration tool was considered and rejected: at two tables with no versioned history it would add a dependency without solving a problem this project has.

`PRAGMA journal_mode=WAL` is stored in the database file, so it is set once at startup. `PRAGMA foreign_keys` is not; SQLite defaults it to off on every connection. `SqliteConnectionFactory` sets `Foreign Keys=True` on the connection string, and the driver turns the pragma on as each connection opens. The connection strings set `Default Timeout=5`: a writer that finds the database locked retries for five seconds before failing. See §8.

### 3.3 State encoding

`StateCodec` converts between a `bool[][]` grid and a flat string, read row-major, with `'1'` for a live cell and `'0'` for a dead one. A 3×3 blinker in its horizontal phase is `"000111000"`.

- **The stored form is readable.** Opening the database during debugging shows the grid directly.
- **The string is what `/final` compares when two fingerprints match** (§4.3).
- **One column holds the whole grid**, so advancing a generation is one row write rather than one write per cell.

Bit-packing was considered and rejected. At these board sizes the storage saving is irrelevant, and it trades a readable column and a trivially testable codec for one whose defects corrupt stored state silently.

---

## 4. Simulation engine

### 4.1 Rules

B3/S23. A live cell with two or three live neighbours survives; a dead cell with exactly three live neighbours becomes live; all other cells die or stay dead. The neighbourhood is the eight surrounding cells, and out-of-bounds positions count as dead.

### 4.2 Implementation

The grid is a `bool[][]` indexed `[row][column]`, the same layout `StateCodec` uses. Each step allocates the next grid and writes into it; the input is never changed. Interior and border cells use the same bounds-checked neighbour count. One step on a 300×300 board takes about 2 ms.

### 4.3 Termination detection

Generations are walked forward with a map from a fingerprint of the encoded state to the generation indexes where that fingerprint appeared:

- The next state equals the current state → **fixed point**, period 1. An all-dead board is reported as `EXTINCT`.
- The next fingerprint is already in the map → rebuild that earlier generation and compare the full strings. Equal strings are a **cycle**, with `firstOccurrence` the stored index and `period` the difference. Unequal strings are a hash collision, and the walk continues.
- `maxGenerations` reached with neither condition met → **no conclusion**, returned as 422 with the number of generations attempted.

The fingerprint is two 64-bit FNV-1a lanes over the same characters. A collision does not change the answer, because the full string is the check. The map stores hashes and indexes, not grids, which is what lets `/final` use the generation ceiling on a 300×300 board.

A checkpoint of the encoded state is kept every 256 generations, including generation 0. Confirming a match replays at most 255 steps from the nearest checkpoint, rather than repeating the whole lead-in from generation 0.

---

## 5. API

Base path `/api/v1`. JSON throughout. Errors are RFC 7807 problem documents (`application/problem+json`).

| Method | Path | Purpose | Success | Errors |
|---|---|---|---|---|
| `POST` | `/boards` | Upload a board, return id | `201` + `Location` | `400` validation, malformed grid, oversized board, body too large, null cell, or bad JSON; `415` |
| `GET` | `/boards/{id}` | Metadata and generation 0 | `200` | `400` id is not a UUID, `404` |
| `GET` | `/boards/{id}/next` | One generation forward | `200` | `400` id is not a UUID, `404` |
| `GET` | `/boards/{id}/generations/{n}` | State n generations away | `200` | `404`; `400` when n is negative, not an integer, above the ceiling, or over the cell-generation budget |
| `GET` | `/boards/{id}/final` | Final state | `200` with termination metadata | `400` when `maxGenerations` is below 1 or not an integer, `404`, `422` |

- `/next` is the same read as `/generations/1`. It is a separate route because the specification names it as its own capability.
- `POST`, `GET /boards/{id}`, `/next`, and `/generations/{n}` return `id`, `width`, `height`, `generation`, and `cells`. `cells` is a row-array of booleans, the same shape as the upload.
- `/final` returns `id`, `width`, `height`, `cells`, `terminationKind` (`EXTINCT`, `FIXED_POINT` or `CYCLE`), `firstOccurrenceGeneration`, `period`, `generationsComputed`, and `generationsLimit`. The cells are the state that repeated. `generationsComputed` is how many steps the walk took. `generationsLimit` is the cap the walk used, after the ceiling clamp.
- Limit resolution for `/final`: the query parameter if present, otherwise `board.max_generations` if non-null, otherwise `GameOfLife:MaxGenerations`; then clamped to the ceiling. An empty `maxGenerations=` is treated as absent.
- Input is validated at the edge. `width` or `height` below 1, or a missing `cells`, is title `Validation failed`, with each field listed: `width: must be greater than or equal to 1; cells: must not be null`. A grid whose rows do not match the declared size, or over `MaxCells`, is title `Invalid board`.
- The endpoints parse the path id, `n`, `maxGenerations` and the body themselves rather than relying on framework binding. A value that does not convert is title `Bad Request` with detail `Failed to convert 'id' with value: 'not-a-uuid'`; a body that is not JSON is `Bad Request` with `Failed to read request`. A UUID that is not stored is `404`.
- Unknown routes (404, detail `No static resource api/v1/…`), a wrong method (405, detail `Method 'DELETE' is not supported.`, `Allow` listing the mapped methods), an unacceptable `Accept` header (406) and an unsupported media type (415, detail `Content-Type 'text/plain;charset=UTF-8' is not supported.`) are problem documents too. An error is always written as `application/problem+json`, whatever the `Accept` header says.
- `HEAD` answers like `GET` without a body. `OPTIONS` answers `200` with `Allow: GET,HEAD,OPTIONS`, or `POST,OPTIONS` on `/boards`.
- Routes are case-sensitive and do not match a trailing slash: `/API/v1/boards/{id}` and `/api/v1/boards/{id}/` are `404`. A `;name=value` parameter inside a path segment is removed before matching.
- A successful body is `application/json`, or the `application/*+json` type the `Accept` header names. An `Accept` header that allows no JSON type is `406` after the request has run, so an upload with such a header still stores the board.
- A missing resume row, when the board itself exists, is a 500. That is a broken cache, not an unknown board.
- No `GET` changes the stored board. The only writes outside `POST /boards` go to the cache, and only from `/next` and `/generations/{n}`.
- Resuming a generation read starts at the highest cached index at or below the one requested. If the cache is already past it and the requested row is missing, the walk starts again from generation 0. It never returns a later row as if it were the requested generation.

Every problem document has the same five fields; a 422 adds `generationsAttempted`:

```json
{
  "type": "about:blank",
  "title": "No conclusion",
  "status": 422,
  "detail": "No conclusion reached within 1 generations",
  "instance": "/api/v1/boards/2f1c0b7e-4a0e-4f1a-9c2d-6b7e8f901234/final",
  "generationsAttempted": 1
}
```

Upload, generation 0 of a horizontal blinker:

```json
{
  "id": "2f1c0b7e-4a0e-4f1a-9c2d-6b7e8f901234",
  "width": 3,
  "height": 3,
  "generation": 0,
  "cells": [[false, false, false], [true, true, true], [false, false, false]]
}
```

`/final` for that blinker:

```json
{
  "id": "2f1c0b7e-4a0e-4f1a-9c2d-6b7e8f901234",
  "width": 3,
  "height": 3,
  "cells": [[false, false, false], [true, true, true], [false, false, false]],
  "terminationKind": "CYCLE",
  "firstOccurrenceGeneration": 0,
  "period": 2,
  "generationsComputed": 2,
  "generationsLimit": 1000
}
```

#### Request leniency

Path, query and body values are read leniently, the same way as the reference implementation:

| Value | Accepted | Rejected (`400 Bad Request`) |
|---|---|---|
| A cell | `true`, `false`; any integer (`0` is false, any other is true); the strings `true`, `True`, `TRUE`, `false`, `False`, `FALSE`, trimmed | fractions, any other string, `null`, arrays |
| `width`, `height` | integers; fractions and exponents, truncated toward zero, within `int` range; strings holding a signed decimal integer, trimmed | a missing or `null` value, other strings, `true`/`false`, values outside `int` |
| The body | one JSON object; anything after it is ignored; a UTF-8 byte-order mark is skipped | comments, trailing commas, a body that does not start with an object |
| `n`, `maxGenerations` | decimal with an optional sign and leading zeros; hexadecimal with `0x`, `0X` or `#`; whitespace anywhere is removed first. A blank `maxGenerations` is absent. With a repeated `maxGenerations`, the first value wins; the name is case-sensitive | fractions, values outside `int` |
| `id` | a canonical UUID in either case; short forms such as `1-1-1-1-1` (each field may start with `+`, and a field longer than its slot keeps its low digits); surrounding spaces trimmed | more than 36 characters, fewer or more than five fields, an empty field, a non-hex character |

---

## 6. Testing

### 6.1 Coverage by layer

1. **Engine tests** (`GameOfLife.Domain.Tests`), no host: block, blinker, toad, beacon, glider translating and then settling at the boundary, empty board, single cell, fully live board, 1×1 and 1×N shapes.
2. **Codec tests**: random boards survive a round trip; wrong lengths, bad characters, null and jagged grids are rejected.
3. **Termination tests**: fixed point, cycle, extinction, no conclusion, a forced hash collision that is not a cycle, and a cycle confirmed from a checkpoint.
4. **Repository and schema tests** (`GameOfLife.Infrastructure.Tests`) against a temporary SQLite file, including the stored id and timestamp format, the foreign key, and the exact schema.
5. **Service tests** (`GameOfLife.Application.Tests`) over an in-memory fake repository and a fixed clock, plus options validation.
6. **API tests** (`GameOfLife.Api.IntegrationTests`): the whole API in memory, on its own temporary database file. Every endpoint in §5; the 404, 400, 415 and 422 bodies; the problem document shape; a body over the size cap and a chunked body that crosses it inside `cells`; and the exception-to-problem mapping on its own. `EdgeCaseParityTests` and `SpringConversionsTests` pin the request-leniency rules, HEAD and OPTIONS, route matching and content negotiation, with the reference service's answers as the expected values. `ProblemWriterTests` and `RequestSizeLimitMiddlewareTests` pin the problem `instance` and the declared-length size check.
7. **Restart test**: a board is created and advanced through `BoardService`, the host is disposed, a second host starts on the same file, and the board and its cached generations are asserted intact.

Line coverage on the last run: `GameOfLife.Domain` 99%, `GameOfLife.Application` 100%, `GameOfLife.Infrastructure` 97%. Collect it with `dotnet test --collect:"XPlat Code Coverage"`. The build does not fail below a threshold.

### 6.2 Traceability

| Requirement | Covered by |
|---|---|
| F1 Upload board | `BoardApiTests.Post_boards_returns_201_with_an_id_and_a_location_header` |
| GET board | `BoardApiTests.Get_board_returns_the_uploaded_board` |
| F2 Next state | `BoardApiTests.Get_next_returns_the_following_generation`, `BoardApiTests.Calling_next_twice_returns_the_same_generation_both_times`, `LifeEngineTests.Blinker_oscillates_with_period_2`, `BoardServiceTests.Repeated_reads_of_the_same_generation_return_the_same_state` |
| F3 N generations away | `BoardApiTests.Get_generations_n_returns_the_expected_state`, `BoardServiceTests.Resumes_from_the_highest_cached_generation_rather_than_from_zero` |
| F3 Cache ahead of the request | `BoardServiceTests.A_gap_below_the_highest_cached_index_is_not_answered_with_that_later_state` |
| F3 Index past the ceiling | `BoardServiceTests.Rejects_a_negative_index_and_an_index_past_the_ceiling`, `ApiParityTests.A_value_outside_the_limits_is_an_invalid_board` |
| F4 Final state | `BoardApiTests.Get_final_returns_the_state_and_termination_metadata`, `TerminationDetectorTests.Blinker_is_detected_as_a_cycle_with_period_2`, `BoardServiceTests.Uses_the_configured_default_and_returns_a_conclusion` |
| F4 Cycle entry point | `TerminationDetectorTests.Reports_the_generation_where_the_cycle_first_occurred`, `TerminationDetectorTests.A_cycle_past_a_checkpoint_still_reports_the_original_entry_generation` |
| F4 Hash collision is not a cycle | `TerminationDetectorTests.A_shared_hash_is_a_cycle_only_when_the_grids_match` |
| F4 No conclusion | `BoardApiTests.Get_final_returns_422_when_the_generation_limit_is_exceeded`, `TerminationDetectorTests.Returns_null_when_no_conclusion_is_reached_within_the_limit`, `ApiProblemsTests.No_conclusion_is_422_and_reports_how_far_the_walk_got` |
| F4 Caller limit clamped | `BoardServiceTests.Clamps_a_caller_supplied_generation_limit_to_the_configured_ceiling`, `ApiParityTests.A_limit_above_the_ceiling_is_clamped_and_still_concludes` |
| F4 Cell-generation budget on `/generations`; `/final` uses the generation cap | `BoardServiceTests.Rejects_a_walk_whose_cells_times_generations_exceed_the_budget`, `BoardServiceTests.Final_state_uses_the_generation_cap_not_the_cell_generation_budget`, `ApiParityTests.Generation_past_the_cell_generation_budget_is_rejected` |
| F4 Non-positive caller limit | `BoardServiceTests.Rejects_a_non_positive_max_generations_instead_of_reporting_422` |
| F4 Stored per-board cap | `BoardServiceTests.Uses_the_board_limit_when_the_caller_does_not_supply_one` |
| Unknown id | `BoardApiTests.Unknown_board_id_returns_404_with_a_problem_body` |
| Id or number that does not convert | `ApiParityTests.A_path_or_query_value_that_does_not_convert_is_a_bad_request` |
| Malformed upload | `BoardApiTests.Malformed_upload_returns_400_validation_failed_naming_the_field`, `BoardServiceTests.Rejects_a_grid_whose_dimensions_do_not_match_the_declared_width_and_height` |
| Null cell | `BoardApiTests.A_null_cell_is_rejected_instead_of_stored_as_dead` |
| Oversized board | `BoardServiceTests.Rejects_a_board_exceeding_the_configured_cell_cap`, `ApiParityTests.A_board_over_max_cells_is_an_invalid_board` |
| Request body over the byte cap | `BoardApiTests.A_body_over_the_request_size_limit_is_rejected`, `BoardApiTests.A_chunked_body_that_crosses_the_cap_inside_cells_is_request_too_large` |
| Problem document shape | `ApiParityTests.Problem_documents_have_the_rfc_7807_shape_without_a_trace_id`, `EdgeCaseParityTests.An_error_is_a_problem_document_whatever_the_accept_header` |
| Request leniency, HEAD, OPTIONS, route matching, Accept | `EdgeCaseParityTests`, `SpringConversionsTests` |
| Broken generation cache | `BoardServiceTests.Missing_board_and_missing_resume_point` |
| Durability, cache survives restart | `RestartPersistenceTests.Board_and_cached_generations_survive_an_application_restart` |
| Stored schema | `SchemaInitializerTests` |
| Rules correctness | `LifeEngineTests` |
| Encoding integrity | `StateCodecTests.Round_trip_preserves_an_arbitrary_board` |
| Contradictory limits stop startup | `GameOfLifeOptionsTests` |

### 6.3 Running the examples

`./try-all.sh` runs every HTTP scenario against a live process; `try-it.sh` is the blinker alone, and `scripts/try-*.sh` is one file per other board and error case. `requests.http` is the blinker walk, one request at a time. Restart is two steps, because the process has to stop in between: `./try-restart.sh save`, stop or kill the server, start it, `./try-restart.sh check`. CI runs `./try-all.sh` and the restart with `kill -9` against a freshly started service.

---

## 7. Possible extensions

- Optional per-board generation cap on upload. `Board.MaxGenerations` and the nullable `max_generations` column already exist, and `/final` already prefers that column. The upload body does not accept it yet.
- Infinite or toroidal topology as a per-board option.
- RLE pattern import.
- A sparse representation for large, mostly-dead grids, and HashLife for very deep generation counts.
- A server-backed datastore behind `IBoardRepository`, for horizontal scale.
- Rate limiting and request quotas.

## 8. Concurrency note

Two concurrent requests may compute the same uncached generation at once. The write race is benign: generation rows are immutable for a given `(board_id, idx)`, and inserts use `INSERT OR IGNORE`, so either writer produces the same row. `/final` does not take that path.

SQLite allows one writer at a time. Readers proceed concurrently because the file is in WAL mode. A writer that finds the database locked retries for `Default Timeout` (five seconds); past that it fails with `SQLITE_BUSY`, which surfaces as a 500. The cell-generation budget is what keeps a single write from running that long on the boards this service accepts.

`/final` is CPU-bound and runs on a thread-pool thread for its whole walk. At the generation ceiling on a 300×300 board that is about 1.5 s of one core in a Release build; the reference implementation takes about 2.5 s on the same machine. `dotnet run` builds Debug, which is roughly four times slower; use `dotnet run -c Release` when timing.

Concurrent load was checked against the reference implementation: 40 parallel reads of the same uncached generation return one identical body with no errors, and 20 parallel uploads return 20 distinct ids, in both.

## 9. Logging

Logs go to the console through the built-in .NET console logger, one line per entry. Levels are in `Logging:LogLevel`; the default is Information. Grids are not logged. A board can be 90,000 cells, and the stored `0`/`1` string is already visible in SQLite.

| Level | What is logged |
|---|---|
| Information | Startup, the limits from configuration, schema ready, each HTTP call, a board saved, and the outcome of `/final` (kind, period, generations). |
| Warning | 400 and 404, a caller `maxGenerations` clamped to the ceiling, and a generation cache that has a later row but not the one requested. |
| Debug | Repository calls with ids and indexes, cache hits, and the index a walk resumes from. Turn it on with `Logging:LogLevel:GameOfLife=Debug`. |

A 422 is Information. Reaching the generation cap is a documented result, not a fault. Unexpected failures are logged at Error with the exception and returned as a 500 problem document.

## 10. Differences from the Java implementation

This service is a port of a Java 25 / Spring Boot implementation. The API, the stored schema, the limits, the rules and the demo scripts are the same. The table lists every place the .NET version differs, and why. Anything not listed behaves the same.

| Area | Java | .NET | Why |
|---|---|---|---|
| Structure | One Maven module, packages `web`, `service`, `domain`, `repository` | Four projects: `Api`, `Application`, `Domain`, `Infrastructure` | Project references make the layer rules a compile-time check: `Domain` cannot reference ASP.NET or SQLite. In Java the same boundary is a convention. |
| Start command | `mvn spring-boot:run` | `dotnet run --project src/GameOfLife.Api --launch-profile http` | Platform tooling. |
| Database path | `data/game-of-life.db`, relative to the directory Maven runs in | The `http` launch profile sets `../../data/game-of-life.db` | `dotnet run` starts the app in the project folder, not the repository root. The relative path puts the file in the same place, `data/` under the repository root. |
| Configuration | `application.yml`, `game-of-life.max-cells`, … | `appsettings.json`, `GameOfLife:MaxCells`, … | .NET configuration conventions. Same five limits, same defaults, same startup checks. The stored `board` DDL is identical except the comment on `max_generations`, which names this setting. |
| Data access | `JdbcClient` over a Hikari pool | ADO.NET over `Microsoft.Data.Sqlite`, which pools connections itself | Platform equivalent. Same SQL. |
| Foreign keys and busy wait | Hikari `connection-init-sql: PRAGMA foreign_keys=ON`; `busy_timeout=5000` on the JDBC URL | `Foreign Keys=True` (set in code) and `Default Timeout=5` on the connection string | The .NET driver turns foreign keys on per connection from the connection string, and retries a locked database until `Default Timeout` (seconds) runs out. Same effect. |
| `created_at` precision | `Instant.toString()`: 0, 3, 6 or 9 fractional digits | Up to 7 fractional digits, trailing zeros dropped | .NET timestamps have 100 ns resolution. Both are ISO-8601 UTC ending in `Z`. |
| Async | Synchronous service and repository | `async` service and repository | Idiomatic for ASP.NET Core. The order of reads and writes is the same. |
| Request binding and validation | Spring conversion, Jackson, Bean Validation | `SpringConversions`, `LenientBooleanConverter`, `LenientInt32Converter`, `SpringPathRules`, `JsonNegotiation` reproduce the same rules (§5, Request leniency) | Neither ASP.NET Core binding nor System.Text.Json has these rules: binding answers a malformed GUID with 404 and a bad body with an empty 400, and System.Text.Json does not coerce numbers or strings to booleans. Each rule was checked against the reference service; [parity-report.md](parity-report.md) has the request-by-request result. |
| Unexpected errors (500) | Spring Boot's error JSON: `timestamp`, `status`, `error`, `path` | A problem document with title `Internal Server Error` | The 500 path (a cache row missing under a stored board) cannot be reached through the API, so the shapes were not compared. A problem document keeps every error in one format. |
| Order of validation messages | Bean Validation collects violations in an unordered set: the same body gives 2 to 4 different orders | Always `width`, `height`, `cells` | A fixed order is one of the orders the reference produces. Clients cannot rely on any order from either service. |
| Repeated query value that does not convert | `detail` names the internal id of a `String[]`, e.g. `'[Ljava.lang.String;@69affc32'`, which changes on every request | `detail` names the first value, e.g. `'abc'` | The Java text cannot be reproduced. Status and title match. |
| Non-ASCII digits | `Integer.valueOf` accepts any Unicode decimal digit in `n` and `maxGenerations` | ASCII digits only in decimal values; UUID fields accept Unicode digits | .NET integer parsing is ASCII-only. A path with such digits has to be percent-encoded, so no client sends one by accident. |
| Build configuration | `mvn spring-boot:run` runs optimised code | `dotnet run` builds Debug; the 300×300 `/final` walk takes about 5.5 s, against 1.5 s in Release and 2.5 s in the reference | Debug is the .NET default for local runs. Use `dotnet run -c Release` when timing. |
| Logging | Logback; levels under `logging.level.life.simulation.engine`; TRACE per cell inside `LifeEngine` | Built-in console logger; levels under `Logging:LogLevel:GameOfLife`; no logging inside `Domain` | .NET gets loggers through dependency injection. The domain is static functions with no container, and a static global logger would be the only one in the codebase. The service logs the same outcomes. There is no per-cell TRACE. |
| Tests | JUnit 5, AssertJ, Mockito; 72 tests | xUnit; 218 tests; a hand-written in-memory repository instead of mocks | No mocking library is needed for one interface. The extra tests pin the stored schema, the HTTP contract the scripts check, and the request-leniency rules with the reference service's answers. Java's "global validation message" handler test has no counterpart, because the .NET endpoints raise no object-level validation errors. |
| Coverage | JaCoCo report on every `mvn test` | `coverlet` on request: `dotnet test --collect:"XPlat Code Coverage"` | Platform tooling. |
| `requests.http` | Literal `{id}` placeholders | `@base` and `@id` variables | Variables work in Visual Studio, Rider and the VS Code REST Client. |
| CI | None | GitHub Actions: build and test, then `./try-all.sh` and the `kill -9` restart against a running service | An addition, not a difference in behaviour. It runs the same scripts on every push. |

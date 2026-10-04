# Side-by-side parity report: Java and .NET

The Java service and this .NET service were run next to each other and sent the same requests. This report records what was compared and the result of every request. The design differences that remain are in [design.md](design.md) section 10; how the port was done is in [porting-plan.md](porting-plan.md).

## Result

| Part | Compared | Identical | Notes |
|---|---|---|---|
| Paired requests | 81 | **81** | Normal path, every error path, and edge cases |
| Probes | 153 | **152** | One input per row: cell and width values, numbers, ids, query strings, `Accept`, methods, routes |
| Scenario boards | 33 | **33** | 11 patterns × `/next`, generation 4, `/final` |
| Stored schema | 13 structure rows | **all** | DDL text: 1 line differs (the comment naming the config setting) |
| Concurrency | 2 runs per service | **same outcome** | See [Concurrency and timing](#concurrency-and-timing) |

Every difference is explained: 1 known, listed under [Known differences](#known-differences). Nothing else differs.

A request is **identical** when both services return the same status, media type, problem `type`, `title`, `detail`, `instance` and extension fields, the same success body, and the same `Allow` header. Board ids differ between the services and are masked before comparing. Validation messages are compared as a set, because the Java service lists them in no fixed order (2 to 4 different orders over 8 identical requests).

## How it was run

| | Java | .NET |
|---|---|---|
| Code | `life-simulation-engine` at `896d82c`, built from a scratch copy (`git archive`); the repository was only read | `game-of-life-api` `main` at `1bb52eb` plus this change |
| Runtime | OpenJDK 25.0.4.1, Spring Boot 3.5.16, `java -jar` | .NET SDK 10.0.401, ASP.NET Core 10.0.12, `dotnet run -c Release` |
| Port | 8081 | 8080 |
| Database | Its own new file | Its own new file |

Machine: Apple M1 Max, macOS 15.7.9, SQLite 3.43.2. Run on 2026-10-04 01:44 PDT.

The comparison scripts are not in this repository: they need the Java service running, and this repository has no dependency on it. The expected values they found are pinned in `EdgeCaseParityTests`, `SpringConversionsTests`, `ProblemWriterTests` and `RequestSizeLimitMiddlewareTests`, which run without it.

## Known differences

| Request | Java | .NET | Why |
|---|---|---|---|
| Query string: `maxGenerations=abc&maxGenerations=1` | **400** Bad Request — Failed to convert 'maxGenerations' with value: '[Ljava.lang.String;@69affc32' | **400** Bad Request — Failed to convert 'maxGenerations' with value: 'abc' | Java prints the internal id of a `String[]` (`[Ljava.lang.String;@…`), which changes on every request and cannot be reproduced. Status and title match. |

## Paired requests

### Normal path

| # | Case | Request | Java | .NET | |
|---|---|---|---|---|---|
| 1 | Board, generation 0 | `GET /api/v1/boards/{id}` | **200** `application/json` | **200** `application/json` | ✅ |
| 2 | Next generation | `GET /api/v1/boards/{id}/next` | **200** `application/json` | **200** `application/json` | ✅ |
| 3 | Generation 0 | `GET /api/v1/boards/{id}/generations/0` | **200** `application/json` | **200** `application/json` | ✅ |
| 4 | Generation 10 | `GET /api/v1/boards/{id}/generations/10` | **200** `application/json` | **200** `application/json` | ✅ |
| 5 | Final state | `GET /api/v1/boards/{id}/final` | **200** `application/json` | **200** `application/json` | ✅ |

### Final-state limit

| # | Case | Request | Java | .NET | |
|---|---|---|---|---|---|
| 6 | maxGenerations=1 (no conclusion) | `GET /api/v1/boards/{id}/final?maxGenerations=1` | **422** No conclusion — No conclusion reached within 1 generations · `generationsAttempted`=1 | **422** No conclusion — No conclusion reached within 1 generations · `generationsAttempted`=1 | ✅ |
| 7 | maxGenerations=0 | `GET /api/v1/boards/{id}/final?maxGenerations=0` | **400** Invalid board — maxGenerations must be at least 1 | **400** Invalid board — maxGenerations must be at least 1 | ✅ |
| 8 | maxGenerations=-3 | `GET /api/v1/boards/{id}/final?maxGenerations=-3` | **400** Invalid board — maxGenerations must be at least 1 | **400** Invalid board — maxGenerations must be at least 1 | ✅ |
| 9 | maxGenerations=100000 (clamped) | `GET /api/v1/boards/{id}/final?maxGenerations=100000` | **200** `application/json` | **200** `application/json` | ✅ |
| 10 | maxGenerations= (empty) | `GET /api/v1/boards/{id}/final?maxGenerations=` | **200** `application/json` | **200** `application/json` | ✅ |
| 11 | maxGenerations=abc | `GET /api/v1/boards/{id}/final?maxGenerations=abc` | **400** Bad Request — Failed to convert 'maxGenerations' with value: 'abc' | **400** Bad Request — Failed to convert 'maxGenerations' with value: 'abc' | ✅ |
| 12 | maxGenerations=+5 (decodes to ' 5') | `GET /api/v1/boards/{id}/final?maxGenerations=+5` | **200** `application/json` | **200** `application/json` | ✅ |
| 13 | maxGenerations=%205 | `GET /api/v1/boards/{id}/final?maxGenerations=%205` | **200** `application/json` | **200** `application/json` | ✅ |
| 14 | maxGenerations=0x10 (hex) | `GET /api/v1/boards/{id}/final?maxGenerations=0x10` | **200** `application/json` | **200** `application/json` | ✅ |
| 15 | maxGenerations=1.5 | `GET /api/v1/boards/{id}/final?maxGenerations=1.5` | **400** Bad Request — Failed to convert 'maxGenerations' with value: '1.5' | **400** Bad Request — Failed to convert 'maxGenerations' with value: '1.5' | ✅ |
| 16 | maxGenerations twice (1, then 50) | `GET /api/v1/boards/{id}/final?maxGenerations=1&maxGenerations=50` | **422** No conclusion — No conclusion reached within 1 generations · `generationsAttempted`=1 | **422** No conclusion — No conclusion reached within 1 generations · `generationsAttempted`=1 | ✅ |
| 17 | MAXGENERATIONS=1 (name in capitals) | `GET /api/v1/boards/{id}/final?MAXGENERATIONS=1` | **200** `application/json` | **200** `application/json` | ✅ |

### Generation index

| # | Case | Request | Java | .NET | |
|---|---|---|---|---|---|
| 18 | -1 | `GET /api/v1/boards/{id}/generations/-1` | **400** Invalid board — Generation index cannot be negative | **400** Invalid board — Generation index cannot be negative | ✅ |
| 19 | 10001 (over the ceiling) | `GET /api/v1/boards/{id}/generations/10001` | **400** Invalid board — Generation index exceeds ceiling of 10000 | **400** Invalid board — Generation index exceeds ceiling of 10000 | ✅ |
| 20 | abc | `GET /api/v1/boards/{id}/generations/abc` | **400** Bad Request — Failed to convert 'n' with value: 'abc' | **400** Bad Request — Failed to convert 'n' with value: 'abc' | ✅ |
| 21 | 99999999999 (over int) | `GET /api/v1/boards/{id}/generations/99999999999` | **400** Bad Request — Failed to convert 'n' with value: '99999999999' | **400** Bad Request — Failed to convert 'n' with value: '99999999999' | ✅ |
| 22 | +2 | `GET /api/v1/boards/{id}/generations/+2` | **200** `application/json` | **200** `application/json` | ✅ |
| 23 | 007 | `GET /api/v1/boards/{id}/generations/007` | **200** `application/json` | **200** `application/json` | ✅ |
| 24 | 0x2 (hex) | `GET /api/v1/boards/{id}/generations/0x2` | **200** `application/json` | **200** `application/json` | ✅ |
| 25 | %202 (leading space) | `GET /api/v1/boards/{id}/generations/%202` | **200** `application/json` | **200** `application/json` | ✅ |

### Board id

| # | Case | Request | Java | .NET | |
|---|---|---|---|---|---|
| 26 | Unknown UUID | `GET /api/v1/boards/00000000-0000-0000-0000-000000000000` | **404** Board not found — No board with id <id> | **404** Board not found — No board with id <id> | ✅ |
| 27 | Unknown UUID, /next | `GET /api/v1/boards/00000000-0000-0000-0000-000000000000/next` | **404** Board not found — No board with id <id> | **404** Board not found — No board with id <id> | ✅ |
| 28 | Unknown UUID, /final | `GET /api/v1/boards/00000000-0000-0000-0000-000000000000/final` | **404** Board not found — No board with id <id> | **404** Board not found — No board with id <id> | ✅ |
| 29 | Unknown UUID, generation -1 | `GET /api/v1/boards/00000000-0000-0000-0000-000000000000/generations/-1` | **404** Board not found — No board with id <id> | **404** Board not found — No board with id <id> | ✅ |
| 30 | not-a-uuid | `GET /api/v1/boards/not-a-uuid` | **400** Bad Request — Failed to convert 'id' with value: 'not-a-uuid' | **400** Bad Request — Failed to convert 'id' with value: 'not-a-uuid' | ✅ |
| 31 | Upper-case id | `GET /api/v1/boards/{ID upper-case}` | **200** `application/json` | **200** `application/json` | ✅ |
| 32 | 32 hex digits, no dashes | `GET /api/v1/boards/00000000000000000000000000000000` | **400** Bad Request — Failed to convert 'id' with value: '00000000000000000000000000000000' | **400** Bad Request — Failed to convert 'id' with value: '00000000000000000000000000000000' | ✅ |
| 33 | Braced UUID | `GET /api/v1/boards/%7B00000000-0000-0000-0000-000000000000%7D` | **400** Bad Request — Failed to convert 'id' with value: '{<id>}' | **400** Bad Request — Failed to convert 'id' with value: '{<id>}' | ✅ |
| 34 | Short form 1-1-1-1-1 | `GET /api/v1/boards/1-1-1-1-1` | **404** Board not found — No board with id <id> | **404** Board not found — No board with id <id> | ✅ |
| 35 | UUID with a leading space | `GET /api/v1/boards/%2000000000-0000-0000-0000-000000000000` | **404** Board not found — No board with id <id> | **404** Board not found — No board with id <id> | ✅ |

### Upload body

| # | Case | Request | Java | .NET | |
|---|---|---|---|---|---|
| 36 | Not JSON: { | `POST /api/v1/boards`<br>body `{` | **400** Bad Request — Failed to read request | **400** Bad Request — Failed to read request | ✅ |
| 37 | Empty body | `POST /api/v1/boards`<br>body (empty) | **400** Bad Request — Failed to read request | **400** Bad Request — Failed to read request | ✅ |
| 38 | JSON null | `POST /api/v1/boards`<br>body `null` | **400** Bad Request — Failed to read request | **400** Bad Request — Failed to read request | ✅ |
| 39 | JSON array | `POST /api/v1/boards`<br>body `[]` | **400** Bad Request — Failed to read request | **400** Bad Request — Failed to read request | ✅ |
| 40 | Empty object {} | `POST /api/v1/boards`<br>body `{}` | **400** Bad Request — Failed to read request | **400** Bad Request — Failed to read request | ✅ |
| 41 | width 0 | `POST /api/v1/boards`<br>body `0x3 board (105 bytes)` | **400** Validation failed — width: must be greater than or equal to 1 | **400** Validation failed — width: must be greater than or equal to 1 | ✅ |
| 42 | width -1, height 0 | `POST /api/v1/boards`<br>body `{"width": -1, "height": 0, "cells": []}` | **400** Validation failed — height: must be greater than or equal to 1; width: must be greater than or equal to 1 | **400** Validation failed — width: must be greater than or equal to 1; height: must be greater than or equal to 1 | ✅ |
| 43 | No cells | `POST /api/v1/boards`<br>body `{"width": 1, "height": 1}` | **400** Validation failed — cells: must not be null | **400** Validation failed — cells: must not be null | ✅ |
| 44 | cells null | `POST /api/v1/boards`<br>body `{"width": 1, "height": 1, "cells": null}` | **400** Validation failed — cells: must not be null | **400** Validation failed — cells: must not be null | ✅ |
| 45 | A null cell | `POST /api/v1/boards`<br>body `{"width":1,"height":1,"cells":[[null]]}` | **400** Bad Request — Failed to read request | **400** Bad Request — Failed to read request | ✅ |
| 46 | A null row | `POST /api/v1/boards`<br>body `{"width":1,"height":2,"cells":[[true],null]}` | **400** Invalid board — Grid width does not match declared width | **400** Invalid board — Grid width does not match declared width | ✅ |
| 47 | width null | `POST /api/v1/boards`<br>body `{"width":null,"height":1,"cells":[[true]]}` | **400** Bad Request — Failed to read request | **400** Bad Request — Failed to read request | ✅ |
| 48 | Cells as 1 and 0 | `POST /api/v1/boards`<br>body `{"width":2,"height":1,"cells":[[1,0]]}` | **201** `application/json` | **201** `application/json` | ✅ |
| 49 | Cell as "true" | `POST /api/v1/boards`<br>body `{"width":1,"height":1,"cells":[["true"]]}` | **201** `application/json` | **201** `application/json` | ✅ |
| 50 | width "1" | `POST /api/v1/boards`<br>body `{"width":"1","height":1,"cells":[[true]]}` | **201** `application/json` | **201** `application/json` | ✅ |
| 51 | width 1.0 | `POST /api/v1/boards`<br>body `{"width":1.0,"height":1,"cells":[[true]]}` | **201** `application/json` | **201** `application/json` | ✅ |
| 52 | width 1.5 | `POST /api/v1/boards`<br>body `{"width":1.5,"height":1,"cells":[[true]]}` | **201** `application/json` | **201** `application/json` | ✅ |
| 53 | Text after the object | `POST /api/v1/boards`<br>body `{"width":1,"height":1,"cells":[[true]]} x` | **201** `application/json` | **201** `application/json` | ✅ |
| 54 | Two objects | `POST /api/v1/boards`<br>body `{"width":1,"height":1,"cells":[[true]]}{}` | **201** `application/json` | **201** `application/json` | ✅ |
| 55 | Unknown property | `POST /api/v1/boards`<br>body `{"width":1,"height":1,"cells":[[true]],"extra":5}` | **201** `application/json` | **201** `application/json` | ✅ |
| 56 | "Width" capitalised | `POST /api/v1/boards`<br>body `{"Width":1,"height":1,"cells":[[true]]}` | **400** Bad Request — Failed to read request | **400** Bad Request — Failed to read request | ✅ |
| 57 | width given twice | `POST /api/v1/boards`<br>body `{"width":5,"width":1,"height":1,"cells":[[true]]}` | **201** `application/json` | **201** `application/json` | ✅ |
| 58 | Comment in the JSON | `POST /api/v1/boards`<br>body `{"width":1,/*c*/"height":1,"cells":[[true]]}` | **400** Bad Request — Failed to read request | **400** Bad Request — Failed to read request | ✅ |
| 59 | Trailing comma | `POST /api/v1/boards`<br>body `{"width":1,"height":1,"cells":[[true]],}` | **400** Bad Request — Failed to read request | **400** Bad Request — Failed to read request | ✅ |
| 60 | Rows wider than declared | `POST /api/v1/boards`<br>body `{"width":2,"height":2,"cells":[[false,fa… (72 bytes)` | **400** Invalid board — Grid width does not match declared width | **400** Invalid board — Grid width does not match declared width | ✅ |
| 61 | More rows than declared | `POST /api/v1/boards`<br>body `{"width":1,"height":2,"cells":[[true],[true],[true]]}` | **400** Invalid board — Grid height does not match declared height | **400** Invalid board — Grid height does not match declared height | ✅ |
| 62 | 301x301 (over MaxCells) | `POST /api/v1/boards`<br>body `301x301 board (634,849 bytes)` | **400** Invalid board — Board exceeds maximum cell count of 90000 | **400** Invalid board — Board exceeds maximum cell count of 90000 | ✅ |
| 63 | Body of 2.1 MB | `POST /api/v1/boards`<br>body `{"width":1,"height":1,"cells":[[        … (2,100,035 bytes)` | **400** Request too large — Request body exceeds 2000000 bytes | **400** Request too large — Request body exceeds 2000000 bytes | ✅ |
| 64 | UTF-8 byte-order mark | `POST /api/v1/boards`<br>body `﻿{"width":1,"height":1,"cells":[[true]]}` | **201** `application/json` | **201** `application/json` | ✅ |

### Content type

| # | Case | Request | Java | .NET | |
|---|---|---|---|---|---|
| 65 | text/plain | `POST /api/v1/boards`<br>body `{"width":1,"height":1,"cells":[[true]]}`<br>`Content-Type: text/plain` | **415** Unsupported Media Type — Content-Type 'text/plain;charset=UTF-8' is not supported. | **415** Unsupported Media Type — Content-Type 'text/plain;charset=UTF-8' is not supported. | ✅ |
| 66 | No Content-Type | `POST /api/v1/boards`<br>body `{"width":1,"height":1,"cells":[[true]]}`<br>`Content-Type: ` | **415** Unsupported Media Type — Content-Type 'application/octet-stream' is not supported. | **415** Unsupported Media Type — Content-Type 'application/octet-stream' is not supported. | ✅ |
| 67 | application/json; charset=utf-8 | `POST /api/v1/boards`<br>body `{"width":1,"height":1,"cells":[[true]]}`<br>`Content-Type: application/json; charset=utf-8` | **201** `application/json` | **201** `application/json` | ✅ |
| 68 | application/problem+json | `POST /api/v1/boards`<br>body `{"width":1,"height":1,"cells":[[true]]}`<br>`Content-Type: application/problem+json` | **201** `application/json` | **201** `application/json` | ✅ |

### Methods and routes

| # | Case | Request | Java | .NET | |
|---|---|---|---|---|---|
| 69 | DELETE a board | `DELETE /api/v1/boards/{id}` | **405** Method Not Allowed — Method 'DELETE' is not supported. · Allow `GET` | **405** Method Not Allowed — Method 'DELETE' is not supported. · Allow `GET` | ✅ |
| 70 | PUT a board | `PUT /api/v1/boards/{id}`<br>body `3x3 board (102 bytes)` | **405** Method Not Allowed — Method 'PUT' is not supported. · Allow `GET` | **405** Method Not Allowed — Method 'PUT' is not supported. · Allow `GET` | ✅ |
| 71 | GET the board list | `GET /api/v1/boards` | **405** Method Not Allowed — Method 'GET' is not supported. · Allow `POST` | **405** Method Not Allowed — Method 'GET' is not supported. · Allow `POST` | ✅ |
| 72 | POST to /next | `POST /api/v1/boards/{id}/next`<br>body `3x3 board (102 bytes)` | **405** Method Not Allowed — Method 'POST' is not supported. · Allow `GET` | **405** Method Not Allowed — Method 'POST' is not supported. · Allow `GET` | ✅ |
| 73 | HEAD a board | `HEAD /api/v1/boards/{id}` | **200** `application/json` | **200** `application/json` | ✅ |
| 74 | OPTIONS a board | `OPTIONS /api/v1/boards/{id}` | **200** · Allow `GET,HEAD,OPTIONS` | **200** · Allow `GET,HEAD,OPTIONS` | ✅ |
| 75 | Unknown route | `GET /api/v1/nothing-here` | **404** Not Found — No static resource api/v1/nothing-here. | **404** Not Found — No static resource api/v1/nothing-here. | ✅ |
| 76 | Root / | `GET /` | **404** Not Found — No static resource . | **404** Not Found — No static resource . | ✅ |
| 77 | Trailing slash | `GET /api/v1/boards/{id}/` | **404** Not Found — No static resource api/v1/boards/<id>. | **404** Not Found — No static resource api/v1/boards/<id>. | ✅ |
| 78 | Path in capitals | `GET /API/V1/BOARDS/{id}` | **404** Not Found — No static resource API/V1/BOARDS/<id>. | **404** Not Found — No static resource API/V1/BOARDS/<id>. | ✅ |
| 79 | Double slash | `GET /api/v1//boards/{id}` | **404** Not Found — No static resource api/v1/boards/<id>. | **404** Not Found — No static resource api/v1/boards/<id>. | ✅ |

### Accept header

| # | Case | Request | Java | .NET | |
|---|---|---|---|---|---|
| 80 | Accept: application/xml | `GET /api/v1/boards/{id}`<br>`Accept: application/xml` | **406** Not Acceptable — Acceptable representations: [application/json, application/*+json]. | **406** Not Acceptable — Acceptable representations: [application/json, application/*+json]. | ✅ |
| 81 | Accept: text/plain | `GET /api/v1/boards/{id}`<br>`Accept: text/plain` | **406** Not Acceptable — Acceptable representations: [application/json, application/*+json]. | **406** Not Acceptable — Acceptable representations: [application/json, application/*+json]. | ✅ |

## Probes

Each probe varies one input. Expected values for these are pinned in the .NET tests.

### Cell value (21 of 21 identical)

| Input | Java | .NET | |
|---|---|---|---|
| `true` | **201** `application/json` | **201** `application/json` | ✅ |
| `false` | **201** `application/json` | **201** `application/json` | ✅ |
| `1` | **201** `application/json` | **201** `application/json` | ✅ |
| `0` | **201** `application/json` | **201** `application/json` | ✅ |
| `2` | **201** `application/json` | **201** `application/json` | ✅ |
| `-1` | **201** `application/json` | **201** `application/json` | ✅ |
| `1.0` | **400** Bad Request — Failed to read request | **400** Bad Request — Failed to read request | ✅ |
| `0.0` | **400** Bad Request — Failed to read request | **400** Bad Request — Failed to read request | ✅ |
| `0.5` | **400** Bad Request — Failed to read request | **400** Bad Request — Failed to read request | ✅ |
| `"true"` | **201** `application/json` | **201** `application/json` | ✅ |
| `"false"` | **201** `application/json` | **201** `application/json` | ✅ |
| `"True"` | **201** `application/json` | **201** `application/json` | ✅ |
| `"TRUE"` | **201** `application/json` | **201** `application/json` | ✅ |
| `"1"` | **400** Bad Request — Failed to read request | **400** Bad Request — Failed to read request | ✅ |
| `"0"` | **400** Bad Request — Failed to read request | **400** Bad Request — Failed to read request | ✅ |
| `"yes"` | **400** Bad Request — Failed to read request | **400** Bad Request — Failed to read request | ✅ |
| `""` | **400** Bad Request — Failed to read request | **400** Bad Request — Failed to read request | ✅ |
| `" true"` | **201** `application/json` | **201** `application/json` | ✅ |
| `[true]` | **400** Bad Request — Failed to read request | **400** Bad Request — Failed to read request | ✅ |
| `{}` | **400** Bad Request — Failed to read request | **400** Bad Request — Failed to read request | ✅ |
| `null` | **400** Bad Request — Failed to read request | **400** Bad Request — Failed to read request | ✅ |

### Width value (19 of 19 identical)

| Input | Java | .NET | |
|---|---|---|---|
| `1` | **201** `application/json` | **201** `application/json` | ✅ |
| `"1"` | **201** `application/json` | **201** `application/json` | ✅ |
| `" 1"` | **201** `application/json` | **201** `application/json` | ✅ |
| `"1.5"` | **400** Bad Request — Failed to read request | **400** Bad Request — Failed to read request | ✅ |
| `"+1"` | **201** `application/json` | **201** `application/json` | ✅ |
| `"0x1"` | **400** Bad Request — Failed to read request | **400** Bad Request — Failed to read request | ✅ |
| `1.0` | **201** `application/json` | **201** `application/json` | ✅ |
| `1.9` | **201** `application/json` | **201** `application/json` | ✅ |
| `-0.5` | **400** Validation failed — width: must be greater than or equal to 1 | **400** Validation failed — width: must be greater than or equal to 1 | ✅ |
| `1e0` | **201** `application/json` | **201** `application/json` | ✅ |
| `1E0` | **201** `application/json` | **201** `application/json` | ✅ |
| `2147483648` | **400** Bad Request — Failed to read request | **400** Bad Request — Failed to read request | ✅ |
| `"2147483648"` | **400** Bad Request — Failed to read request | **400** Bad Request — Failed to read request | ✅ |
| `1e10` | **400** Bad Request — Failed to read request | **400** Bad Request — Failed to read request | ✅ |
| `true` | **400** Bad Request — Failed to read request | **400** Bad Request — Failed to read request | ✅ |
| `"abc"` | **400** Bad Request — Failed to read request | **400** Bad Request — Failed to read request | ✅ |
| `""` | **400** Bad Request — Failed to read request | **400** Bad Request — Failed to read request | ✅ |
| `[1]` | **400** Bad Request — Failed to read request | **400** Bad Request — Failed to read request | ✅ |
| `null` | **400** Bad Request — Failed to read request | **400** Bad Request — Failed to read request | ✅ |

### Generation index n (15 of 15 identical)

| Input | Java | .NET | |
|---|---|---|---|
| `2` | **200** `application/json` | **200** `application/json` | ✅ |
| `%202` | **200** `application/json` | **200** `application/json` | ✅ |
| `2%20` | **200** `application/json` | **200** `application/json` | ✅ |
| `2%202` | **200** `application/json` | **200** `application/json` | ✅ |
| `0x2` | **200** `application/json` | **200** `application/json` | ✅ |
| `0X2` | **200** `application/json` | **200** `application/json` | ✅ |
| `%232` | **200** `application/json` | **200** `application/json` | ✅ |
| `-0x1` | **400** Invalid board — Generation index cannot be negative | **400** Invalid board — Generation index cannot be negative | ✅ |
| `010` | **200** `application/json` | **200** `application/json` | ✅ |
| `+2` | **200** `application/json` | **200** `application/json` | ✅ |
| `%2B2` | **200** `application/json` | **200** `application/json` | ✅ |
| `2.0` | **400** Bad Request — Failed to convert 'n' with value: '2.0' | **400** Bad Request — Failed to convert 'n' with value: '2.0' | ✅ |
| `0x7fffffff` | **400** Invalid board — Generation index exceeds ceiling of 10000 | **400** Invalid board — Generation index exceeds ceiling of 10000 | ✅ |
| `0x80000000` | **400** Bad Request — Failed to convert 'n' with value: '0x80000000' | **400** Bad Request — Failed to convert 'n' with value: '0x80000000' | ✅ |
| `%20` | **400** Bad Request — Failed to convert 'n' with value: ' ' | **400** Bad Request — Failed to convert 'n' with value: ' ' | ✅ |

### maxGenerations value (9 of 9 identical)

| Input | Java | .NET | |
|---|---|---|---|
| `+5` | **200** `application/json` | **200** `application/json` | ✅ |
| `%205` | **200** `application/json` | **200** `application/json` | ✅ |
| `5%20` | **200** `application/json` | **200** `application/json` | ✅ |
| `0x10` | **200** `application/json` | **200** `application/json` | ✅ |
| `%2310` | **200** `application/json` | **200** `application/json` | ✅ |
| `-0x1` | **400** Invalid board — maxGenerations must be at least 1 | **400** Invalid board — maxGenerations must be at least 1 | ✅ |
| `1%2050` | **200** `application/json` | **200** `application/json` | ✅ |
| `(empty)` | **200** `application/json` | **200** `application/json` | ✅ |
| `%20` | **200** `application/json` | **200** `application/json` | ✅ |

### Query string (4 of 5 identical)

| Input | Java | .NET | |
|---|---|---|---|
| `maxGenerations=1&maxGenerations=50` | **422** No conclusion — No conclusion reached within 1 generations · `generationsAttempted`=1 | **422** No conclusion — No conclusion reached within 1 generations · `generationsAttempted`=1 | ✅ |
| `maxGenerations=&maxGenerations=1` | **200** `application/json` | **200** `application/json` | ✅ |
| `maxGenerations=abc&maxGenerations=1` | **400** Bad Request — Failed to convert 'maxGenerations' with value: '[Ljava.lang.String;@69affc32' | **400** Bad Request — Failed to convert 'maxGenerations' with value: 'abc' | ⚠️ known |
| `maxgenerations=1` | **200** `application/json` | **200** `application/json` | ✅ |
| `maxGenerations` | **200** `application/json` | **200** `application/json` | ✅ |

### Board id (16 of 16 identical)

| Input | Java | .NET | |
|---|---|---|---|
| `1-1-1-1-1` | **404** Board not found — No board with id <id> | **404** Board not found — No board with id <id> | ✅ |
| `a-b-c-d-e` | **404** Board not found — No board with id <id> | **404** Board not found — No board with id <id> | ✅ |
| `+1-1-1-1-1` | **404** Board not found — No board with id <id> | **404** Board not found — No board with id <id> | ✅ |
| `-1-1-1-1-1` | **400** Bad Request — Failed to convert 'id' with value: '-1-1-1-1-1' | **400** Bad Request — Failed to convert 'id' with value: '-1-1-1-1-1' | ✅ |
| `1-1-1-1` | **400** Bad Request — Failed to convert 'id' with value: '1-1-1-1' | **400** Bad Request — Failed to convert 'id' with value: '1-1-1-1' | ✅ |
| `1-1-1-1-1-1` | **400** Bad Request — Failed to convert 'id' with value: '1-1-1-1-1-1' | **400** Bad Request — Failed to convert 'id' with value: '1-1-1-1-1-1' | ✅ |
| `123456789-1-1-1-1` | **404** Board not found — No board with id <id> | **404** Board not found — No board with id <id> | ✅ |
| `1-12345-1-1-1` | **404** Board not found — No board with id <id> | **404** Board not found — No board with id <id> | ✅ |
| `1-1-1-1-1234567890123` | **404** Board not found — No board with id <id> | **404** Board not found — No board with id <id> | ✅ |
| `1--1-1-1` | **400** Bad Request — Failed to convert 'id' with value: '1--1-1-1' | **400** Bad Request — Failed to convert 'id' with value: '1--1-1-1' | ✅ |
| `(upper-case id)` | **200** `application/json` | **200** `application/json` | ✅ |
| `%20(id)` | **200** `application/json` | **200** `application/json` | ✅ |
| `(id)%20` | **200** `application/json` | **200** `application/json` | ✅ |
| `g-1-1-1-1` | **400** Bad Request — Failed to convert 'id' with value: 'g-1-1-1-1' | **400** Bad Request — Failed to convert 'id' with value: 'g-1-1-1-1' | ✅ |
| `0x1-1-1-1-1` | **400** Bad Request — Failed to convert 'id' with value: '0x1-1-1-1-1' | **400** Bad Request — Failed to convert 'id' with value: '0x1-1-1-1-1' | ✅ |
| `1-1-1-1-111111111111111111111111111111` | **400** Bad Request — Failed to convert 'id' with value: '1-1-1-1-111111111111111111111111111111' | **400** Bad Request — Failed to convert 'id' with value: '1-1-1-1-111111111111111111111111111111' | ✅ |

### Accept header (42 of 42 identical)

| Input | Java | .NET | |
|---|---|---|---|
| `GET a board · application/xml` | **406** Not Acceptable — Acceptable representations: [application/json, application/*+json]. | **406** Not Acceptable — Acceptable representations: [application/json, application/*+json]. | ✅ |
| `GET a board · text/plain` | **406** Not Acceptable — Acceptable representations: [application/json, application/*+json]. | **406** Not Acceptable — Acceptable representations: [application/json, application/*+json]. | ✅ |
| `GET a board · application/*` | **200** `application/json` | **200** `application/json` | ✅ |
| `GET a board · */*` | **200** `application/json` | **200** `application/json` | ✅ |
| `GET a board · application/problem+json` | **200** None · `cells`=[[False, False, False], [True, True, True], [False, False, False]], `generation`=0, `height`=3, `id`=<id>, `width`=3 | **200** None · `cells`=[[False, False, False], [True, True, True], [False, False, False]], `generation`=0, `height`=3, `id`=<id>, `width`=3 | ✅ |
| `GET a board · text/html, */*;q=0.1` | **200** `application/json` | **200** `application/json` | ✅ |
| `GET a board · application/json;q=0` | **200** `application/json` | **200** `application/json` | ✅ |
| `GET an unknown board · application/xml` | **404** Board not found — No board with id <id> | **404** Board not found — No board with id <id> | ✅ |
| `GET an unknown board · text/plain` | **404** Board not found — No board with id <id> | **404** Board not found — No board with id <id> | ✅ |
| `GET an unknown board · application/*` | **404** Board not found — No board with id <id> | **404** Board not found — No board with id <id> | ✅ |
| `GET an unknown board · */*` | **404** Board not found — No board with id <id> | **404** Board not found — No board with id <id> | ✅ |
| `GET an unknown board · application/problem+json` | **404** Board not found — No board with id <id> | **404** Board not found — No board with id <id> | ✅ |
| `GET an unknown board · text/html, */*;q=0.1` | **404** Board not found — No board with id <id> | **404** Board not found — No board with id <id> | ✅ |
| `GET an unknown board · application/json;q=0` | **404** Board not found — No board with id <id> | **404** Board not found — No board with id <id> | ✅ |
| `GET /final, no conclusion · application/xml` | **422** No conclusion — No conclusion reached within 1 generations · `generationsAttempted`=1 | **422** No conclusion — No conclusion reached within 1 generations · `generationsAttempted`=1 | ✅ |
| `GET /final, no conclusion · text/plain` | **422** No conclusion — No conclusion reached within 1 generations · `generationsAttempted`=1 | **422** No conclusion — No conclusion reached within 1 generations · `generationsAttempted`=1 | ✅ |
| `GET /final, no conclusion · application/*` | **422** No conclusion — No conclusion reached within 1 generations · `generationsAttempted`=1 | **422** No conclusion — No conclusion reached within 1 generations · `generationsAttempted`=1 | ✅ |
| `GET /final, no conclusion · */*` | **422** No conclusion — No conclusion reached within 1 generations · `generationsAttempted`=1 | **422** No conclusion — No conclusion reached within 1 generations · `generationsAttempted`=1 | ✅ |
| `GET /final, no conclusion · application/problem+json` | **422** No conclusion — No conclusion reached within 1 generations · `generationsAttempted`=1 | **422** No conclusion — No conclusion reached within 1 generations · `generationsAttempted`=1 | ✅ |
| `GET /final, no conclusion · text/html, */*;q=0.1` | **422** No conclusion — No conclusion reached within 1 generations · `generationsAttempted`=1 | **422** No conclusion — No conclusion reached within 1 generations · `generationsAttempted`=1 | ✅ |
| `GET /final, no conclusion · application/json;q=0` | **422** No conclusion — No conclusion reached within 1 generations · `generationsAttempted`=1 | **422** No conclusion — No conclusion reached within 1 generations · `generationsAttempted`=1 | ✅ |
| `GET a bad id · application/xml` | **400** Bad Request — Failed to convert 'id' with value: 'not-a-uuid' | **400** Bad Request — Failed to convert 'id' with value: 'not-a-uuid' | ✅ |
| `GET a bad id · text/plain` | **400** Bad Request — Failed to convert 'id' with value: 'not-a-uuid' | **400** Bad Request — Failed to convert 'id' with value: 'not-a-uuid' | ✅ |
| `GET a bad id · application/*` | **400** Bad Request — Failed to convert 'id' with value: 'not-a-uuid' | **400** Bad Request — Failed to convert 'id' with value: 'not-a-uuid' | ✅ |
| `GET a bad id · */*` | **400** Bad Request — Failed to convert 'id' with value: 'not-a-uuid' | **400** Bad Request — Failed to convert 'id' with value: 'not-a-uuid' | ✅ |
| `GET a bad id · application/problem+json` | **400** Bad Request — Failed to convert 'id' with value: 'not-a-uuid' | **400** Bad Request — Failed to convert 'id' with value: 'not-a-uuid' | ✅ |
| `GET a bad id · text/html, */*;q=0.1` | **400** Bad Request — Failed to convert 'id' with value: 'not-a-uuid' | **400** Bad Request — Failed to convert 'id' with value: 'not-a-uuid' | ✅ |
| `GET a bad id · application/json;q=0` | **400** Bad Request — Failed to convert 'id' with value: 'not-a-uuid' | **400** Bad Request — Failed to convert 'id' with value: 'not-a-uuid' | ✅ |
| `POST a board · application/xml` | **406** Not Acceptable — Acceptable representations: [application/json, application/*+json]. | **406** Not Acceptable — Acceptable representations: [application/json, application/*+json]. | ✅ |
| `POST a board · text/plain` | **406** Not Acceptable — Acceptable representations: [application/json, application/*+json]. | **406** Not Acceptable — Acceptable representations: [application/json, application/*+json]. | ✅ |
| `POST a board · application/*` | **201** `application/json` | **201** `application/json` | ✅ |
| `POST a board · */*` | **201** `application/json` | **201** `application/json` | ✅ |
| `POST a board · application/problem+json` | **201** None · `cells`=[[True]], `generation`=0, `height`=1, `id`=<id>, `width`=1 | **201** None · `cells`=[[True]], `generation`=0, `height`=1, `id`=<id>, `width`=1 | ✅ |
| `POST a board · text/html, */*;q=0.1` | **201** `application/json` | **201** `application/json` | ✅ |
| `POST a board · application/json;q=0` | **201** `application/json` | **201** `application/json` | ✅ |
| `POST bad JSON · application/xml` | **400** Bad Request — Failed to read request | **400** Bad Request — Failed to read request | ✅ |
| `POST bad JSON · text/plain` | **400** Bad Request — Failed to read request | **400** Bad Request — Failed to read request | ✅ |
| `POST bad JSON · application/*` | **400** Bad Request — Failed to read request | **400** Bad Request — Failed to read request | ✅ |
| `POST bad JSON · */*` | **400** Bad Request — Failed to read request | **400** Bad Request — Failed to read request | ✅ |
| `POST bad JSON · application/problem+json` | **400** Bad Request — Failed to read request | **400** Bad Request — Failed to read request | ✅ |
| `POST bad JSON · text/html, */*;q=0.1` | **400** Bad Request — Failed to read request | **400** Bad Request — Failed to read request | ✅ |
| `POST bad JSON · application/json;q=0` | **400** Bad Request — Failed to read request | **400** Bad Request — Failed to read request | ✅ |

### Method (12 of 12 identical)

| Input | Java | .NET | |
|---|---|---|---|
| `HEAD /api/v1/boards/(id)` | **200** `application/json` | **200** `application/json` | ✅ |
| `HEAD /api/v1/boards/(id)/final` | **200** `application/json` | **200** `application/json` | ✅ |
| `HEAD /api/v1/boards/00000000-0000-0000-0000-000000000000` | **404** `application/problem+json` | **404** `application/problem+json` | ✅ |
| `OPTIONS /api/v1/boards/(id)` | **200** · Allow `GET,HEAD,OPTIONS` | **200** · Allow `GET,HEAD,OPTIONS` | ✅ |
| `OPTIONS /api/v1/boards` | **200** · Allow `POST,OPTIONS` | **200** · Allow `POST,OPTIONS` | ✅ |
| `OPTIONS /api/v1/boards/(id)/final` | **200** · Allow `GET,HEAD,OPTIONS` | **200** · Allow `GET,HEAD,OPTIONS` | ✅ |
| `OPTIONS /api/v1/nothing` | **404** Not Found — No static resource api/v1/nothing. | **404** Not Found — No static resource api/v1/nothing. | ✅ |
| `DELETE /api/v1/boards/(id)` | **405** Method Not Allowed — Method 'DELETE' is not supported. · Allow `GET` | **405** Method Not Allowed — Method 'DELETE' is not supported. · Allow `GET` | ✅ |
| `GET /api/v1/boards` | **405** Method Not Allowed — Method 'GET' is not supported. · Allow `POST` | **405** Method Not Allowed — Method 'GET' is not supported. · Allow `POST` | ✅ |
| `PUT /api/v1/boards` | **405** Method Not Allowed — Method 'PUT' is not supported. · Allow `POST` | **405** Method Not Allowed — Method 'PUT' is not supported. · Allow `POST` | ✅ |
| `POST /api/v1/boards/(id)` | **405** Method Not Allowed — Method 'POST' is not supported. · Allow `GET` | **405** Method Not Allowed — Method 'POST' is not supported. · Allow `GET` | ✅ |
| `PATCH /api/v1/boards/(id)/next` | **405** Method Not Allowed — Method 'PATCH' is not supported. · Allow `GET` | **405** Method Not Allowed — Method 'PATCH' is not supported. · Allow `GET` | ✅ |

### Route (14 of 14 identical)

| Input | Java | .NET | |
|---|---|---|---|
| `/api/v1/boards/(id)/` | **404** Not Found — No static resource api/v1/boards/<id>. | **404** Not Found — No static resource api/v1/boards/<id>. | ✅ |
| `/api/v1/boards/` | **404** Not Found — No static resource api/v1/boards. | **404** Not Found — No static resource api/v1/boards. | ✅ |
| `/api/v1/boards/(id)/next/` | **404** Not Found — No static resource api/v1/boards/<id>/next. | **404** Not Found — No static resource api/v1/boards/<id>/next. | ✅ |
| `/API/v1/boards/(id)` | **404** Not Found — No static resource API/v1/boards/<id>. | **404** Not Found — No static resource API/v1/boards/<id>. | ✅ |
| `/api/v1/Boards/(id)` | **404** Not Found — No static resource api/v1/Boards/<id>. | **404** Not Found — No static resource api/v1/Boards/<id>. | ✅ |
| `/api/v1/boards/(id)/NEXT` | **404** Not Found — No static resource api/v1/boards/<id>/NEXT. | **404** Not Found — No static resource api/v1/boards/<id>/NEXT. | ✅ |
| `/api/v1/boards/(id)/Final` | **404** Not Found — No static resource api/v1/boards/<id>/Final. | **404** Not Found — No static resource api/v1/boards/<id>/Final. | ✅ |
| `/api/v1/boards/(id)/GENERATIONS/1` | **404** Not Found — No static resource api/v1/boards/<id>/GENERATIONS/1. | **404** Not Found — No static resource api/v1/boards/<id>/GENERATIONS/1. | ✅ |
| `/api/v1/boards/(id).json` | **400** Bad Request — Failed to convert 'id' with value: '<id>.json' | **400** Bad Request — Failed to convert 'id' with value: '<id>.json' | ✅ |
| `/api/v1/boards/(id);x=1` | **200** `application/json` | **200** `application/json` | ✅ |
| `/api/v1/boards/(id)/generations/1/` | **404** Not Found — No static resource api/v1/boards/<id>/generations/1. | **404** Not Found — No static resource api/v1/boards/<id>/generations/1. | ✅ |
| `/api/v1/boards/(id)/generations/` | **404** Not Found — No static resource api/v1/boards/<id>/generations. | **404** Not Found — No static resource api/v1/boards/<id>/generations. | ✅ |
| `POST /api/v1/boards/` | **404** Not Found — No static resource api/v1/boards. | **404** Not Found — No static resource api/v1/boards. | ✅ |
| `POST /API/V1/BOARDS` | **404** Not Found — No static resource API/V1/BOARDS. | **404** Not Found — No static resource API/V1/BOARDS. | ✅ |

## Scenario boards

Each board was uploaded to both services, then read at `/next`, `/generations/4` and `/final`. The bodies (cells, `terminationKind`, `period`, `firstOccurrenceGeneration`, `generationsComputed`, `generationsLimit`) were compared in full.

| Board | `/next` | generation 4 | `/final` | Final outcome |
|---|---|---|---|---|
| Blinker (period 2) | ✅ | ✅ | ✅ | `CYCLE`, period 2, first seen 0, 2 generations |
| Block (still life) | ✅ | ✅ | ✅ | `FIXED_POINT`, period 1, first seen 0, 1 generation |
| Toad | ✅ | ✅ | ✅ | `CYCLE`, period 2, first seen 0, 2 generations |
| Beacon | ✅ | ✅ | ✅ | `CYCLE`, period 2, first seen 0, 2 generations |
| Glider, 6x6 | ✅ | ✅ | ✅ | `FIXED_POINT`, period 1, first seen 15, 16 generations |
| Plus sign (lead-in to a cycle) | ✅ | ✅ | ✅ | `CYCLE`, period 2, first seen 4, 6 generations |
| Single cell | ✅ | ✅ | ✅ | `EXTINCT`, period 1, first seen 1, 2 generations |
| Full 4x4 | ✅ | ✅ | ✅ | `EXTINCT`, period 1, first seen 2, 3 generations |
| Empty | ✅ | ✅ | ✅ | `EXTINCT`, period 1, first seen 0, 1 generation |
| 1x1 live | ✅ | ✅ | ✅ | `EXTINCT`, period 1, first seen 1, 2 generations |
| 1x5 | ✅ | ✅ | ✅ | `EXTINCT`, period 1, first seen 3, 4 generations |

## Stored schema

Both databases were created by their service on first start, then compared with `pragma_table_info`, `pragma_foreign_key_list`, `pragma_index_list` and `PRAGMA journal_mode`: **13 rows, identical**.

```text
board|0|id|TEXT|0|NULL|1
board|1|width|INTEGER|1|NULL|0
board|2|height|INTEGER|1|NULL|0
board|3|initial_state|TEXT|1|NULL|0
board|4|created_at|TEXT|1|NULL|0
board|5|max_generations|INTEGER|0|NULL|0
generation|0|board_id|TEXT|1|NULL|1
generation|1|idx|INTEGER|1|NULL|2
generation|2|state|TEXT|1|NULL|0
fk|0|0|board|board_id|id|NO ACTION|NO ACTION|NONE
index|sqlite_autoindex_generation_1|1|pk
index|sqlite_autoindex_board_1|1|pk
wal
```

`.schema` text (44 lines each) differs in one line, the comment that names the configuration setting:

- Line 22, Java: `-- NULL means use game-of-life.max-generations from application.yml at request time.`
- Line 22, .NET: `-- NULL means use GameOfLife:MaxGenerations from appsettings.json at request time.`

A stored board, Java: `id` is text of length 36, `created_at` `2026-10-04T08:40:05.121026Z`, `max_generations` null. .NET: `id` is text of length 36, `created_at` `2026-10-04T08:44:37.699378Z`, `max_generations` null.

## Concurrency and timing

The board for these runs is a glider in the corner of a 300×300 grid.

| Run | Java | .NET |
|---|---|---|
| 40 parallel reads of the same uncached generation (50) | statuses [200], 1 distinct body, 0.32 s | statuses [200], 1 distinct body, 0.42 s |
| 20 parallel uploads | statuses [201], 20 distinct ids | statuses [201], 20 distinct ids |
| `/final?maxGenerations=10000`, three times in a row | `FIXED_POINT` after 1192 generations; 2.48 s, 2.47 s, 2.48 s | `FIXED_POINT` after 1192 generations; 1.51 s, 1.42 s, 1.44 s |
| Four of those `/final` calls in parallel | statuses [200], 2.54 s wall | statuses [200], 1.58 s wall |

`dotnet run` without `-c Release` builds Debug, which takes about 5.5 s for the `/final` walk.

## What differed before the fixes

The first paired run, made during the review, compared status, title and success body only. It found 22 real differences; six more were false alarms from boards that the harness had not masked. All 22 were fixed in PR [#8](https://github.com/anoopsaxena3262/game-of-life-api/pull/8):

| Case | Java | .NET before the fix |
|---|---|---|
| `maxGenerations=+5`, `%205`, `0x10` | 200; the value is trimmed or read as hex | 400 Bad Request |
| `maxGenerations` twice | 422: the first value wins | 400: values joined as `1,50` |
| `MAXGENERATIONS=1` | 200: the name is case-sensitive, so it is ignored | 422: matched case-insensitively |
| `/generations/0x2`, `/generations/%202` | 200 | 400 Bad Request |
| Short UUID `1-1-1-1-1` | 404 Board not found | 400 Bad Request |
| `{}`, `"Width"` capitalised | 400 Bad Request | 400 Validation failed |
| Cells `1`/`0`, cell `"true"` | 201 | 400 Bad Request |
| `width` `1.0`, `1.5` | 201 | 400 Bad Request |
| Text after the object, two objects | 201 | 400 Bad Request |
| `HEAD` a board | 200 | 405 |
| `OPTIONS` a board | 200, `Allow: GET,HEAD,OPTIONS` | 405 |
| Trailing slash, path in capitals | 404 | 200 |
| `Accept: application/xml`, `text/plain` | 406 Not Acceptable | 200 |

The review then compared `detail` and `Allow` as well, and checked an error with an `Accept` header that excludes JSON. That found the 404, 405 and 415 `detail` texts, the double-slash `detail`, the listening address, and one real bug: such an error returned **500 with the exception text in the body**. All were fixed in PR #8.

This run compares `type`, `instance` and the extension fields too. It found three more, fixed in the change that adds this report:

| Case | Java | .NET before the fix |
|---|---|---|
| A body over the size limit with a declared `Content-Length` | No `instance` (the filter writes the answer before the error handler) | `instance` set |
| `instance` for a path with percent-encoded characters, e.g. `%7B…%7D`, `%20` | The path as sent, still encoded | The decoded path |
| Several validation failures in one body | Messages in no fixed order | Always `width`, `height`, `cells`, which is one of Java's orders. Not changed: compared as a set |


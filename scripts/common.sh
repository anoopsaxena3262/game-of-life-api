#!/usr/bin/env bash
# Shared by the scripts in this directory. try-it.sh does not use this file.
# Each scenario script sets its own board or request and the status it expects.

demo_init() {
  cd "$(dirname "${BASH_SOURCE[1]}")/.."
  BASE="${BASE_URL:-http://localhost:8080}"
  HEADERS=$(mktemp)
  BODY=$(mktemp)
  trap 'rm -f "$HEADERS" "$BODY"' EXIT

  local probe
  probe=$(curl -s -o /dev/null -w '%{http_code}' --connect-timeout 2 \
    "$BASE/api/v1/boards/00000000-0000-0000-0000-000000000000" || true)
  if [[ "$probe" == "000" || -z "$probe" ]]; then
    echo "Nothing is listening at $BASE."
    echo "Start the service in another terminal and leave it running:"
    echo "  dotnet run --project src/GameOfLife.Api --launch-profile http"
    exit 1
  fi
}

demo_pretty() {
  if command -v python3 >/dev/null 2>&1; then
    python3 -m json.tool
  else
    cat
  fi
}

demo_section() {
  echo
  echo "---- $1 ----"
}

# Build an upload body from rows of 0 and 1. First two arguments are width and height.
demo_board_json() {
  python3 - "$@" <<'PY'
import json, sys
width = int(sys.argv[1])
height = int(sys.argv[2])
rows = sys.argv[3:]
if len(rows) != height:
    raise SystemExit(f"expected {height} rows, got {len(rows)}")
cells = []
for index, row in enumerate(rows):
    if len(row) != width or any(ch not in "01" for ch in row):
        raise SystemExit(f"row {index} must be {width} characters of 0 or 1: {row}")
    cells.append([ch == "1" for ch in row])
print(json.dumps({"width": width, "height": height, "cells": cells}))
PY
}

demo_upload() {
  local json="$1"
  local label="$2"
  demo_section "POST /api/v1/boards  ($label)"
  # The body goes on stdin. A 300x300 board is over Linux's 128 KB limit for one argument.
  printf '%s' "$json" | curl -sS -D "$HEADERS" -o "$BODY" -X POST "$BASE/api/v1/boards" \
    -H 'Content-Type: application/json' \
    --data-binary @-
  local status location
  status=$(awk 'NR==1 { print $2 }' "$HEADERS" | tr -d '\r')
  location=$(awk 'tolower($1)=="location:" { print $2 }' "$HEADERS" | tr -d '\r')
  DEMO_ID="${location##*/}"
  if [[ "$status" != "201" || -z "$DEMO_ID" ]]; then
    echo "Expected 201 and a Location header. Got status ${status:-<none>}."
    demo_pretty < "$BODY" || true
    exit 1
  fi
  echo "201  id=$DEMO_ID"
  demo_pretty < "$BODY"
}

# GET a path. Optional third argument is the HTTP status to require (default 200).
demo_get() {
  local title="$1"
  local path="$2"
  local expect="${3:-200}"
  demo_section "$title"
  echo "GET $path"
  local code
  code=$(curl -sS -D "$HEADERS" -o "$BODY" -w '%{http_code}' "$BASE$path")
  echo "HTTP $code"
  demo_pretty < "$BODY"
  if [[ "$code" != "$expect" ]]; then
    echo "Expected HTTP $expect."
    exit 1
  fi
}

# A call that is not a normal upload. Prints the status and the body.
demo_request() {
  local title="$1"
  local method="$2"
  local path="$3"
  local expect="$4"
  local json="${5:-}"
  demo_section "$title"
  echo "$method $path"
  local code
  if [[ -n "$json" ]]; then
    code=$(printf '%s' "$json" | curl -sS -D "$HEADERS" -o "$BODY" -w '%{http_code}' -X "$method" "$BASE$path" \
      -H 'Content-Type: application/json' \
      --data-binary @-)
  else
    code=$(curl -sS -D "$HEADERS" -o "$BODY" -w '%{http_code}' -X "$method" "$BASE$path")
  fi
  echo "HTTP $code"
  demo_pretty < "$BODY" || true
  if [[ "$code" != "$expect" ]]; then
    echo "Expected HTTP $expect."
    exit 1
  fi
}

demo_expect_field() {
  local field="$1"
  local want="$2"
  local got
  got=$(python3 -c 'import json,sys; print(json.load(open(sys.argv[1])).get(sys.argv[2]))' "$BODY" "$field")
  if [[ "$got" != "$want" ]]; then
    echo "Expected $field=$want, got ${got:-<missing>}."
    exit 1
  fi
  echo "ok  $field=$got"
}

# Compare response cells to rows of 0 and 1. One argument per row.
demo_expect_cells() {
  python3 - "$BODY" "$@" <<'PY'
import json, sys
body = json.load(open(sys.argv[1]))
rows = sys.argv[2:]
expect = [[ch == "1" for ch in row] for row in rows]
if body.get("cells") != expect:
    raise SystemExit("cells mismatch.\n got %s\n want %s" % (body.get("cells"), expect))
print("ok  cells match")
PY
}

demo_done() {
  echo
  echo "Done."
}

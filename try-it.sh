#!/usr/bin/env bash
# Demo walk. The service must already be running in another terminal.
#   dotnet run --project src/GameOfLife.Api --launch-profile http
# Then, from the project root:
#   ./try-it.sh

set -euo pipefail

cd "$(dirname "$0")"

BASE="${BASE_URL:-http://localhost:8080}"

pretty() {
  if command -v python3 >/dev/null 2>&1; then
    python3 -m json.tool
  else
    cat
  fi
}

probe=$(curl -s -o /dev/null -w '%{http_code}' --connect-timeout 2 \
  "$BASE/api/v1/boards/00000000-0000-0000-0000-000000000000" || true)
if [[ "$probe" == "000" || -z "$probe" ]]; then
  echo "Nothing is listening at $BASE."
  echo "Start the service in another terminal and leave it running:"
  echo "  dotnet run --project src/GameOfLife.Api --launch-profile http"
  exit 1
fi

echo "POST /api/v1/boards  (3x3 blinker)"
headers=$(mktemp)
body=$(mktemp)
trap 'rm -f "$headers" "$body"' EXIT

curl -sS -D "$headers" -o "$body" -X POST "$BASE/api/v1/boards" \
  -H 'Content-Type: application/json' \
  -d '{"width":3,"height":3,"cells":[[false,false,false],[true,true,true],[false,false,false]]}'

status=$(awk 'NR==1 { print $2 }' "$headers" | tr -d '\r')
location=$(awk 'tolower($1)=="location:" { print $2 }' "$headers" | tr -d '\r')
id="${location##*/}"

if [[ "$status" != "201" || -z "$id" ]]; then
  echo "Expected 201 and a Location header. Got status ${status:-<none>}."
  echo "Location: ${location:-<none>}"
  pretty < "$body" || true
  exit 1
fi

echo "201  id=$id"
echo "Location: $location"
echo
echo "---- generation 0 (upload body) ----"
pretty < "$body"
echo

show() {
  local title="$1"
  local path="$2"
  local expect="${3:-200}"
  echo "---- $title ----"
  echo "GET $path"
  local code
  code=$(curl -sS -o "$body" -w '%{http_code}' "$BASE$path")
  echo "HTTP $code"
  pretty < "$body"
  echo
  if [[ "$code" != "$expect" ]]; then
    echo "Expected HTTP $expect."
    exit 1
  fi
}

expect_cells() {
  python3 - "$body" "$@" <<'PY'
import json, sys
body = json.load(open(sys.argv[1]))
rows = sys.argv[2:]
expect = [[ch == "1" for ch in row] for row in rows]
if body.get("cells") != expect:
    raise SystemExit("cells mismatch.\n got %s\n want %s" % (body.get("cells"), expect))
print("ok  cells match")
PY
}

expect_field() {
  local field="$1"
  local want="$2"
  local got
  got=$(python3 -c 'import json,sys; print(json.load(open(sys.argv[1])).get(sys.argv[2]))' "$body" "$field")
  if [[ "$got" != "$want" ]]; then
    echo "Expected $field=$want, got ${got:-<missing>}."
    exit 1
  fi
  echo "ok  $field=$got"
}

show "board (generation 0)" "/api/v1/boards/$id"
expect_field generation 0
expect_cells \
  000 \
  111 \
  000

show "next, first call (generation 1, middle column live)" "/api/v1/boards/$id/next"
expect_field generation 1
expect_cells \
  010 \
  010 \
  010
next_file=$(mktemp)
cp "$body" "$next_file"

show "next, second call (same body as the first)" "/api/v1/boards/$id/next"
python3 - "$body" "$next_file" <<'PY'
import json, sys
if json.load(open(sys.argv[1])) != json.load(open(sys.argv[2])):
    raise SystemExit("second /next did not match the first")
print("ok  second /next matches the first")
PY
rm -f "$next_file"

show "generation 10 (blinker matches generation 0)" "/api/v1/boards/$id/generations/10"
expect_field generation 10
expect_cells \
  000 \
  111 \
  000

show "final (terminationKind CYCLE, period 2)" "/api/v1/boards/$id/final"
expect_field terminationKind CYCLE
expect_field period 2
expect_cells \
  000 \
  111 \
  000

echo "---- final with maxGenerations=1 (expect 422) ----"
echo "GET /api/v1/boards/$id/final?maxGenerations=1"
code=$(curl -sS -o "$body" -w '%{http_code}' \
  "$BASE/api/v1/boards/$id/final?maxGenerations=1")
echo "HTTP $code"
pretty < "$body"
echo

if [[ "$code" != "422" ]]; then
  echo "Expected HTTP 422 for maxGenerations=1."
  exit 1
fi
expect_field generationsAttempted 1

echo "Done. id=$id"
echo "Stop the service with Ctrl+C in the terminal that is running it."

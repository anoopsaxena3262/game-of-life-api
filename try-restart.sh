#!/usr/bin/env bash
# Show that a board is still there after the process stops or is killed.
#
# Terminal 1 must already be running the service.
#   ./try-restart.sh save
# Stop that process (Ctrl+C, or kill -9 of the listener on 8080). Start it again.
#   ./try-restart.sh check
#
# ./try-all.sh does not include this. The server has to die between the two steps.
# The automated form, with no manual stop, is RestartPersistenceTests in
# tests/GameOfLife.Api.IntegrationTests.

set -euo pipefail

cd "$(dirname "$0")"

BASE="${BASE_URL:-http://localhost:8080}"
ID_FILE="data/restart-demo.id"
DB_FILE="data/game-of-life.db"

require_server() {
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

pretty() {
  if command -v python3 >/dev/null 2>&1; then
    python3 -m json.tool
  else
    cat
  fi
}

save_board() {
  require_server
  mkdir -p data
  local headers body status location id
  headers=$(mktemp)
  body=$(mktemp)
  trap 'rm -f "$headers" "$body"' RETURN

  curl -sS -D "$headers" -o "$body" -X POST "$BASE/api/v1/boards" \
    -H 'Content-Type: application/json' \
    -d '{"width":3,"height":3,"cells":[[false,false,false],[true,true,true],[false,false,false]]}'
  status=$(awk 'NR==1 { print $2 }' "$headers" | tr -d '\r')
  location=$(awk 'tolower($1)=="location:" { print $2 }' "$headers" | tr -d '\r')
  id="${location##*/}"
  if [[ "$status" != "201" || -z "$id" ]]; then
    echo "Upload failed with status ${status:-<none>}."
    pretty < "$body" || true
    exit 1
  fi

  # Fill the generation cache through index 10 before the process dies.
  local code
  code=$(curl -sS -o "$body" -w '%{http_code}' "$BASE/api/v1/boards/$id/generations/10")
  if [[ "$code" != "200" ]]; then
    echo "GET /generations/10 failed with HTTP $code."
    pretty < "$body" || true
    exit 1
  fi

  printf '%s\n' "$id" > "$ID_FILE"
  echo "Saved id=$id"
  echo "Generation 0 and generations 1 through 10 are on disk in $DB_FILE."
  echo
  echo "Now stop the service. Either:"
  echo "  Ctrl+C in the terminal that is running it"
  echo "  or kill -9 of the process listening on 8080"
  echo
  echo "Start it again with:  dotnet run --project src/GameOfLife.Api --launch-profile http"
  echo "Then run:             ./try-restart.sh check"
}

check_board() {
  require_server
  if [[ ! -f "$ID_FILE" ]]; then
    echo "No saved id. Run ./try-restart.sh save before you stop the service."
    exit 1
  fi
  local id
  id=$(tr -d '[:space:]' < "$ID_FILE")
  local body code
  body=$(mktemp)
  trap 'rm -f "$body"' RETURN

  echo "GET /api/v1/boards/$id"
  code=$(curl -sS -o "$body" -w '%{http_code}' "$BASE/api/v1/boards/$id")
  echo "HTTP $code"
  pretty < "$body"
  if [[ "$code" != "200" ]]; then
    echo "The board was not found after restart."
    exit 1
  fi
  python3 - "$body" <<'PY'
import json, sys
board = json.load(open(sys.argv[1]))
expect = [[False, False, False], [True, True, True], [False, False, False]]
if board.get("generation") != 0 or board.get("cells") != expect:
    raise SystemExit("Generation 0 is not the blinker that was uploaded.")
print("ok  generation 0 is the uploaded blinker")
PY

  if command -v sqlite3 >/dev/null 2>&1 && [[ -f "$DB_FILE" ]]; then
    local indexes
    indexes=$(sqlite3 "$DB_FILE" "SELECT idx FROM generation WHERE board_id = '$id' ORDER BY idx;")
    echo "Cached generation indexes still in the file:"
    echo "$indexes"
    local expected="0
1
2
3
4
5
6
7
8
9
10"
    if [[ "$indexes" != "$expected" ]]; then
      echo "Expected indexes 0 through 10. The file does not still hold the cache."
      exit 1
    fi
    echo "ok  generations 0 through 10 are still in the database file"
  fi

  echo
  echo "The board survived the stop. Same id, same generation 0."
}

case "${1:-}" in
  save) save_board ;;
  check) check_board ;;
  *)
    echo "Usage: ./try-restart.sh save | check"
    echo "  save   upload a blinker while the service is running"
    echo "  check  after you stop that process and start it again"
    exit 1
    ;;
esac

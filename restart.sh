#!/usr/bin/env bash
# Start the service from the project root.
# If a database file is already there, ask whether to keep it or delete it first.
# The running app does not ask this. Tests and the published app must start without a prompt.

set -euo pipefail

cd "$(dirname "$0")"

PORT=8080
DB_FILES=(
  data/game-of-life.db
  data/game-of-life.db-wal
  data/game-of-life.db-shm
)

# lsof exits 1 when nothing is listening. Keep going in that case.
listener_pid() {
  lsof -nP -iTCP:"$PORT" -sTCP:LISTEN -t 2>/dev/null | head -n 1 || true
}

stop_if_running() {
  local pid
  pid="$(listener_pid)"
  if [[ -z "$pid" ]]; then
    return 0
  fi

  echo "Something is still listening on port $PORT (pid $pid)."
  ps -p "$pid" -o command= 2>/dev/null || true
  local answer=""
  read -r -p "Stop it before we continue? [y/N] " answer || true
  if [[ "$answer" != [yY] && "$answer" != [yY][eE][sS] ]]; then
    echo "Left it running. Stop it first, then run ./restart.sh again."
    exit 1
  fi

  kill "$pid"
  local _
  for _ in 1 2 3 4 5 6 7 8 9 10; do
    if ! kill -0 "$pid" 2>/dev/null; then
      echo "Stopped pid $pid."
      return 0
    fi
    sleep 0.3
  done

  echo "Pid $pid did not stop. Try: kill $pid"
  exit 1
}

existing_db_files() {
  local f
  for f in "${DB_FILES[@]}"; do
    if [[ -e "$f" ]]; then
      printf '%s\n' "$f"
    fi
  done
}

ask_about_database() {
  local existing
  existing="$(existing_db_files)"
  if [[ -z "$existing" ]]; then
    echo "No database file yet. Starting with a new one."
    return 0
  fi

  echo "Database file is already here:"
  echo "$existing"
  echo
  echo "  1) Restart and keep it"
  echo "  2) Delete it, then start with an empty database"
  local choice=""
  read -r -p "Choice [1]: " choice || true
  choice="${choice:-1}"

  case "$choice" in
    1)
      echo "Keeping the database."
      ;;
    2)
      rm -f "${DB_FILES[@]}"
      echo "Deleted the database files. The next start creates a new one."
      ;;
    *)
      echo "Need 1 or 2. Nothing was deleted."
      exit 1
      ;;
  esac
}

stop_if_running
ask_about_database

# The http launch profile puts the database at data/game-of-life.db under this directory.
# The app creates data/ itself; mkdir keeps the path obvious.
mkdir -p data
exec dotnet run --project src/GameOfLife.Api --launch-profile http

#!/usr/bin/env bash
# Run every demo script against a service that is already running.
#   dotnet run --project src/GameOfLife.Api --launch-profile http
#   ./try-all.sh
# Stops at the first script that exits non-zero.

set -euo pipefail

cd "$(dirname "$0")"

BASE="${BASE_URL:-http://localhost:8080}"

probe=$(curl -s -o /dev/null -w '%{http_code}' --connect-timeout 2 \
  "$BASE/api/v1/boards/00000000-0000-0000-0000-000000000000" || true)
if [[ "$probe" == "000" || -z "$probe" ]]; then
  echo "Nothing is listening at $BASE."
  echo "Start the service in another terminal and leave it running:"
  echo "  dotnet run --project src/GameOfLife.Api --launch-profile http"
  exit 1
fi

scenarios=(
  ./try-it.sh
  ./scripts/try-fixed-point.sh
  ./scripts/try-toad.sh
  ./scripts/try-beacon.sh
  ./scripts/try-glider.sh
  ./scripts/try-large-glider.sh
  ./scripts/try-empty.sh
  ./scripts/try-single-cell.sh
  ./scripts/try-full-board.sh
  ./scripts/try-one-by-one.sh
  ./scripts/try-one-by-n.sh
  ./scripts/try-not-found.sh
  ./scripts/try-invalid-board.sh
  ./scripts/try-mismatched-board.sh
  ./scripts/try-bad-generation.sh
  ./scripts/try-bad-limit.sh
  ./scripts/try-bad-id.sh
  ./scripts/try-limit-clamped.sh
  ./scripts/try-plus.sh
  ./scripts/try-oversized.sh
  ./scripts/try-generation-ceiling.sh
  ./scripts/try-bad-json.sh
  ./scripts/try-missing-cells.sh
  ./scripts/try-null-cell.sh
)

passed=0
for scenario in "${scenarios[@]}"; do
  echo
  echo "======== $scenario ========"
  "$scenario"
  passed=$((passed + 1))
  echo "PASS  $scenario"
done

echo
echo "All $passed scenarios passed."

#!/usr/bin/env bash
# Glider in the corner of a 300x300 board.
# /generations/56 is 400: 56 x 90,000 cells is past GameOfLife:MaxCellGenerations.
# /final does not use that budget. The default walk of 1,000 generations is a
# 422 because the glider has not reached the edge. The ceiling walk reaches
# the corner still life at generation 1,192.
# Service must already be running. See docs/developer-guide.md.

set -euo pipefail
source "$(dirname "$0")/common.sh"
demo_init

# The body is 90,000 cells. Print the fields the checks care about.
demo_pretty() {
  python3 -c '
import json, sys
body = json.load(sys.stdin)
if not isinstance(body, dict):
    print(body)
    raise SystemExit
print("json keys:", ", ".join(body))
for key in (
        "title", "detail", "status", "terminationKind", "period",
        "generationsLimit", "generationsComputed", "generationsAttempted",
        "firstOccurrenceGeneration", "width", "height"):
    if key in body:
        print(f"  {key}={body[key]}")
'
}

json=$(python3 - <<'PY'
import json
n = 300
cells = [[False] * n for _ in range(n)]
cells[0][1] = True
cells[1][2] = True
cells[2][0] = True
cells[2][1] = True
cells[2][2] = True
print(json.dumps({"width": n, "height": n, "cells": cells}))
PY
)
demo_upload "$json" "glider, top-left of a 300x300 board"

demo_get "generation 56 is past the cell-generation budget" \
  "/api/v1/boards/$DEMO_ID/generations/56" 400
demo_expect_field title "Invalid board"
demo_expect_field detail "Generation 56 on a board of 90000 cells exceeds the cell-generation budget of 5000000"

demo_get "final at the default of 1000 (glider still in flight)" \
  "/api/v1/boards/$DEMO_ID/final" 422
demo_expect_field title "No conclusion"
demo_expect_field generationsAttempted 1000

demo_get "final at the ceiling (dies in the corner)" \
  "/api/v1/boards/$DEMO_ID/final?maxGenerations=10000"
demo_expect_field terminationKind FIXED_POINT
demo_expect_field period 1
demo_expect_field generationsLimit 10000
demo_expect_field generationsComputed 1192
demo_expect_field firstOccurrenceGeneration 1191
demo_done

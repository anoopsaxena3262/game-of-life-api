#!/usr/bin/env bash
# Glider on a finite board. It moves, then dies at the edge. /final is FIXED_POINT.
# Service must already be running. See docs/developer-guide.md.

set -euo pipefail
source "$(dirname "$0")/common.sh"
demo_init

json=$(demo_board_json 6 6 \
  010000 \
  001000 \
  111000 \
  000000 \
  000000 \
  000000)
demo_upload "$json" "glider, top-left of a 6x6 board"
demo_get "generation 4 (shifted down and across)" "/api/v1/boards/$DEMO_ID/generations/4"
demo_expect_cells \
  000000 \
  001000 \
  000100 \
  011100 \
  000000 \
  000000
demo_get "final (died in the corner, still life)" "/api/v1/boards/$DEMO_ID/final"
demo_expect_field terminationKind FIXED_POINT
demo_expect_field period 1
demo_done

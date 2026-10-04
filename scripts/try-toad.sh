#!/usr/bin/env bash
# Toad, a period-2 oscillator. /final is CYCLE, same kind of conclusion as the blinker.
# Service must already be running. See docs/developer-guide.md.

set -euo pipefail
source "$(dirname "$0")/common.sh"
demo_init

json=$(demo_board_json 6 4 \
  000000 \
  001110 \
  011100 \
  000000)
demo_upload "$json" "toad"
demo_get "next (the other phase)" "/api/v1/boards/$DEMO_ID/next"
demo_get "generation 2 (back to the upload)" "/api/v1/boards/$DEMO_ID/generations/2"
demo_expect_cells \
  000000 \
  001110 \
  011100 \
  000000
demo_get "final" "/api/v1/boards/$DEMO_ID/final"
demo_expect_field terminationKind CYCLE
demo_expect_field period 2
demo_done

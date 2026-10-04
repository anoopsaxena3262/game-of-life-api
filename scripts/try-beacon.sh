#!/usr/bin/env bash
# Beacon, a period-2 oscillator.
# Service must already be running. See docs/developer-guide.md.

set -euo pipefail
source "$(dirname "$0")/common.sh"
demo_init

json=$(demo_board_json 4 4 \
  1100 \
  1100 \
  0011 \
  0011)
demo_upload "$json" "beacon"
demo_get "next (the other phase)" "/api/v1/boards/$DEMO_ID/next"
demo_get "generation 2 (back to the upload)" "/api/v1/boards/$DEMO_ID/generations/2"
demo_expect_cells \
  1100 \
  1100 \
  0011 \
  0011
demo_get "final" "/api/v1/boards/$DEMO_ID/final"
demo_expect_field terminationKind CYCLE
demo_expect_field period 2
demo_done

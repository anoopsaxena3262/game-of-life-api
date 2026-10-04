#!/usr/bin/env bash
# A single row and a single column. Edges count as dead, so the ends die.
# Service must already be running. See docs/developer-guide.md.

set -euo pipefail
source "$(dirname "$0")/common.sh"
demo_init

json=$(demo_board_json 5 1 11111)
demo_upload "$json" "1x5 row, all live"
demo_get "next (ends died: 01110)" "/api/v1/boards/$DEMO_ID/next"
demo_expect_cells 01110

json=$(demo_board_json 1 5 1 1 1 1 1)
demo_upload "$json" "5x1 column, all live"
demo_get "next (ends died)" "/api/v1/boards/$DEMO_ID/next"
demo_expect_cells 0 1 1 1 0
demo_done

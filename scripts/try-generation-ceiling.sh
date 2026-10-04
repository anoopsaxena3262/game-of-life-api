#!/usr/bin/env bash
# A generation index above the ceiling is 400.
# Service must already be running. See docs/developer-guide.md.

set -euo pipefail
source "$(dirname "$0")/common.sh"
demo_init

json=$(demo_board_json 3 3 \
  000 \
  111 \
  000)
demo_upload "$json" "blinker, used only so the id exists"
demo_get "generation 10001" "/api/v1/boards/$DEMO_ID/generations/10001" 400
demo_expect_field status 400
demo_done

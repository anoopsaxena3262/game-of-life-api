#!/usr/bin/env bash
# A negative generation index is 400. The board itself is a valid blinker.
# Service must already be running. See docs/developer-guide.md.

set -euo pipefail
source "$(dirname "$0")/common.sh"
demo_init

json=$(demo_board_json 3 3 \
  000 \
  111 \
  000)
demo_upload "$json" "blinker, used only so the id exists"
demo_get "generation -1" "/api/v1/boards/$DEMO_ID/generations/-1" 400
demo_expect_field status 400
demo_done

#!/usr/bin/env bash
# maxGenerations must be at least 1. Zero is 400, not 422.
# Service must already be running. See docs/developer-guide.md.

set -euo pipefail
source "$(dirname "$0")/common.sh"
demo_init

json=$(demo_board_json 3 3 \
  000 \
  111 \
  000)
demo_upload "$json" "blinker"
demo_get "final maxGenerations=0" "/api/v1/boards/$DEMO_ID/final?maxGenerations=0" 400
demo_expect_field status 400
demo_done

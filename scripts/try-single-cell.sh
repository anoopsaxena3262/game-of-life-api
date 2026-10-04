#!/usr/bin/env bash
# One live cell dies of underpopulation. /final is EXTINCT.
# Service must already be running. See docs/developer-guide.md.

set -euo pipefail
source "$(dirname "$0")/common.sh"
demo_init

json=$(demo_board_json 3 3 \
  000 \
  010 \
  000)
demo_upload "$json" "single live cell"
demo_get "next (every cell dead)" "/api/v1/boards/$DEMO_ID/next"
demo_get "final" "/api/v1/boards/$DEMO_ID/final"
demo_expect_field terminationKind EXTINCT
demo_expect_field period 1
demo_done

#!/usr/bin/env bash
# Already empty. /final is EXTINCT on the first step.
# Service must already be running. See docs/developer-guide.md.

set -euo pipefail
source "$(dirname "$0")/common.sh"
demo_init

json=$(demo_board_json 4 4 \
  0000 \
  0000 \
  0000 \
  0000)
demo_upload "$json" "empty 4x4"
demo_get "next (still empty)" "/api/v1/boards/$DEMO_ID/next"
demo_get "final" "/api/v1/boards/$DEMO_ID/final"
demo_expect_field terminationKind EXTINCT
demo_expect_field period 1
demo_done

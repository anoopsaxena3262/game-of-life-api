#!/usr/bin/env bash
# Still life. The block never changes, so /final is FIXED_POINT with period 1.
# Service must already be running. See docs/developer-guide.md.

set -euo pipefail
source "$(dirname "$0")/common.sh"
demo_init

json=$(demo_board_json 4 4 \
  0000 \
  0110 \
  0110 \
  0000)
demo_upload "$json" "4x4 block, still life"
demo_get "next (same cells as the upload)" "/api/v1/boards/$DEMO_ID/next"
demo_expect_cells \
  0000 \
  0110 \
  0110 \
  0000
demo_get "final" "/api/v1/boards/$DEMO_ID/final"
demo_expect_field terminationKind FIXED_POINT
demo_expect_field period 1
demo_done

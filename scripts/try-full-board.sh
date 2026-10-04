#!/usr/bin/env bash
# Every cell live. The board collapses and dies. /final is EXTINCT.
# Service must already be running. See docs/developer-guide.md.

set -euo pipefail
source "$(dirname "$0")/common.sh"
demo_init

json=$(demo_board_json 4 4 \
  1111 \
  1111 \
  1111 \
  1111)
demo_upload "$json" "4x4 fully live"
demo_get "next (four corners)" "/api/v1/boards/$DEMO_ID/next"
demo_get "final" "/api/v1/boards/$DEMO_ID/final"
demo_expect_field terminationKind EXTINCT
demo_expect_field period 1
demo_done

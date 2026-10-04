#!/usr/bin/env bash
# 1x1 boards. A dead cell stays dead. A live cell dies. Both /final results are EXTINCT.
# Service must already be running. See docs/developer-guide.md.

set -euo pipefail
source "$(dirname "$0")/common.sh"
demo_init

json=$(demo_board_json 1 1 0)
demo_upload "$json" "1x1 dead"
demo_get "final" "/api/v1/boards/$DEMO_ID/final"
demo_expect_field terminationKind EXTINCT

json=$(demo_board_json 1 1 1)
demo_upload "$json" "1x1 live"
demo_get "next (dead)" "/api/v1/boards/$DEMO_ID/next"
demo_get "final" "/api/v1/boards/$DEMO_ID/final"
demo_expect_field terminationKind EXTINCT
demo_done

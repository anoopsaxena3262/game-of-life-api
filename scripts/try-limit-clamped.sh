#!/usr/bin/env bash
# A caller limit above the ceiling is clamped, not rejected.
# The blinker still concludes. generationsLimit in the body is the ceiling.
# Service must already be running. See docs/developer-guide.md.

set -euo pipefail
source "$(dirname "$0")/common.sh"
demo_init

json=$(demo_board_json 3 3 \
  000 \
  111 \
  000)
demo_upload "$json" "blinker"
demo_get "final maxGenerations=100000 (clamped, still a conclusion)" \
  "/api/v1/boards/$DEMO_ID/final?maxGenerations=100000"
demo_expect_field terminationKind CYCLE
demo_expect_field generationsLimit 10000
echo
echo "generationsLimit is 10000. The request asked for 100000."
echo "The server log also warns that 100000 was clamped to the ceiling."
demo_done

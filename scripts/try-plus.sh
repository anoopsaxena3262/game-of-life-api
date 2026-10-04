#!/usr/bin/env bash
# Plus sign. Generations 0-3 are a lead-in. A period-2 cycle starts at generation 4.
# Service must already be running. See docs/developer-guide.md.

set -euo pipefail
source "$(dirname "$0")/common.sh"
demo_init

json=$(demo_board_json 5 5 \
  00000 \
  00100 \
  01110 \
  00100 \
  00000)
demo_upload "$json" "plus sign, cycle with a lead-in"
demo_get "final" "/api/v1/boards/$DEMO_ID/final"
demo_expect_field terminationKind CYCLE
demo_expect_field period 2
demo_expect_field firstOccurrenceGeneration 4
demo_expect_field generationsComputed 6
demo_done

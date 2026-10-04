#!/usr/bin/env bash
# Declared width does not match the cell rows. Expect 400.
# Service must already be running. See docs/developer-guide.md.

set -euo pipefail
source "$(dirname "$0")/common.sh"
demo_init

demo_request "2x2 declared, rows are 3 wide" POST "/api/v1/boards" 400 \
  '{"width":2,"height":2,"cells":[[false,false,false],[false,false,false]]}'
demo_expect_field title "Invalid board"
demo_done

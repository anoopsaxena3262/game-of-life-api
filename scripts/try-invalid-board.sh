#!/usr/bin/env bash
# Width must be at least 1. Expect 400.
# Service must already be running. See docs/developer-guide.md.

set -euo pipefail
source "$(dirname "$0")/common.sh"
demo_init

demo_request "width 0" POST "/api/v1/boards" 400 \
  '{"width":0,"height":3,"cells":[[false,false,false],[false,false,false],[false,false,false]]}'
demo_expect_field status 400
demo_expect_field title "Validation failed"
demo_done

#!/usr/bin/env bash
# A null cell must not be stored as dead.
# Service must already be running. See docs/developer-guide.md.

set -euo pipefail
source "$(dirname "$0")/common.sh"
demo_init

demo_request "null cell" POST "/api/v1/boards" 400 '{"width":1,"height":1,"cells":[[null]]}'
demo_expect_field status 400
demo_done

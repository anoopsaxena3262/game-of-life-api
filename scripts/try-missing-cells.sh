#!/usr/bin/env bash
# Width and height with no cells array.
# Service must already be running. See docs/developer-guide.md.

set -euo pipefail
source "$(dirname "$0")/common.sh"
demo_init

demo_request "missing cells" POST "/api/v1/boards" 400 '{"width":1,"height":1}'
demo_expect_field title "Validation failed"
demo_done

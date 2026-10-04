#!/usr/bin/env bash
# A body that is not JSON. The title is Bad Request, not Validation failed.
# Service must already be running. See docs/developer-guide.md.

set -euo pipefail
source "$(dirname "$0")/common.sh"
demo_init

demo_request "not json" POST "/api/v1/boards" 400 '{'
demo_expect_field title "Bad Request"
demo_done

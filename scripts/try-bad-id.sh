#!/usr/bin/env bash
# The path id is not a UUID. Expect 400, not 404.
# Service must already be running. See docs/developer-guide.md.

set -euo pipefail
source "$(dirname "$0")/common.sh"
demo_init

demo_request "id is not a UUID" GET "/api/v1/boards/not-a-uuid" 400
demo_expect_field status 400
demo_done

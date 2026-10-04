#!/usr/bin/env bash
# A well-formed id that was never uploaded. Expect 404.
# Service must already be running. See docs/developer-guide.md.

set -euo pipefail
source "$(dirname "$0")/common.sh"
demo_init

demo_request "unknown board" GET \
  "/api/v1/boards/00000000-0000-0000-0000-000000000000" \
  404
demo_expect_field title "Board not found"
demo_done

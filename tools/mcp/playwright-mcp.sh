#!/bin/sh
# Launches the locally-installed Playwright MCP server, resolving paths relative to this
# script's own directory so it works regardless of the caller's working directory or PATH.
DIR="$(cd "$(dirname "$0")" && pwd)"
exec node "$DIR/node_modules/@playwright/mcp/cli.js" "$@"

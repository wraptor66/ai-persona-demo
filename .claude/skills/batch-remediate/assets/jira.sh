#!/usr/bin/env bash
# Run with bash; keeps credentials out of command-line arguments.
set -euo pipefail
exec python3 "$(dirname "$0")/jira_ticket.py" --request "$@"

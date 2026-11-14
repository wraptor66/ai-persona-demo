#!/usr/bin/env bash
# jira.sh METHOD PATH [BODY_FILE]  - one Jira Cloud REST call, auth from the environment.
#   jira.sh GET  "/rest/api/2/issue/PAI-123?fields=summary"
#   jira.sh POST "/rest/api/2/issue/PAI-123/comment" {BATCH_ROOT}/tmp/PAI-123-comment.json
# Needs JIRA_BASE_URL, JIRA_EMAIL, JIRA_API_TOKEN (sourced from ~/.batch-remediate.env or ~/.batch-remediate-auto.env if set
# there). Prints the response body; prints "HTTP <code>" to stderr and exits 1 on a non-2xx status.
# Never echoes the credentials.
set -euo pipefail
for f in "$HOME/.batch-remediate.env" "$HOME/.batch-remediate-auto.env"; do [ -f "$f" ] && . "$f" && break; done
m="${1:?method}"; p="${2:?path}"; body="${3:-}"
for v in JIRA_BASE_URL JIRA_EMAIL JIRA_API_TOKEN; do [ -n "${!v:-}" ] || { echo "$v is not set" >&2; exit 1; }; done
args=(-sS -o /dev/stdout -w '\n%{http_code}' -X "$m" -u "$JIRA_EMAIL:$JIRA_API_TOKEN" -H "Accept: application/json")
[ -n "$body" ] && args+=(-H "Content-Type: application/json" --data-binary "@$body")
out="$(curl "${args[@]}" "${JIRA_BASE_URL%/}$p")"
code="${out##*$'\n'}"; resp="${out%$'\n'*}"
printf '%s\n' "$resp"
case "$code" in 2*) exit 0 ;; *) echo "HTTP $code" >&2; exit 1 ;; esac

#!/usr/bin/env python3
"""Jira Cloud intake and REST transport. Python standard library only."""

import argparse
import base64
import json
import os
from pathlib import Path
import re
import sys
from urllib import error, parse, request


ROOT = Path(__file__).resolve().parents[4]


class JiraError(Exception):
    """Safe diagnostic that never includes credentials or a server response body."""


class NoRedirect(request.HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):
        raise JiraError("Jira redirected the request; check JIRA_SITE.")


def read_config(root):
    values = {}
    in_block = False
    for line in (root / ".claude/batch-remediate.config.md").read_text().splitlines():
        if line.startswith("```"):
            in_block = not in_block
        elif in_block:
            match = re.match(r"^([A-Z_]+)\s*=\s*(.*?)\s*(?:#.*)?$", line)
            if match:
                values[match[1]] = match[2].strip()
    return values


class JiraClient:
    def __init__(self, config):
        self.site = (os.getenv("JIRA_SITE") or os.getenv("JIRA_BASE_URL")
                     or config.get("JIRA_SITE") or config.get("TICKET_URL", "")).rstrip("/")
        email = os.getenv("JIRA_EMAIL") or config.get("JIRA_EMAIL", "")
        # Intentionally do not read tokens from Markdown or evaluate shell substitutions.
        token = os.getenv("JIRA_API_TOKEN", "")
        url = parse.urlsplit(self.site)
        if (url.scheme != "https" or not url.hostname or url.username or url.password
                or url.path or url.query or url.fragment):
            raise JiraError("JIRA_SITE must be an HTTPS origin.")
        if not email or not token or token == "${JIRA_API_TOKEN}":
            raise JiraError("Set JIRA_API_TOKEN in the environment and configure JIRA_EMAIL.")
        encoded = base64.b64encode(f"{email}:{token}".encode()).decode()
        self.headers = {"Authorization": f"Basic {encoded}", "Accept": "application/json"}
        self.opener = request.build_opener(NoRedirect)

    def call(self, method, path, body=None):
        if not path.startswith("/rest/api/") or parse.urlsplit(path).netloc:
            raise JiraError("Only relative Jira REST API paths are permitted.")
        payload = json.dumps(body).encode() if body is not None else None
        headers = dict(self.headers)
        if payload is not None:
            headers["Content-Type"] = "application/json"
        req = request.Request(self.site + path, data=payload, headers=headers, method=method)
        try:
            with self.opener.open(req, timeout=30) as response:
                data = response.read()
                return json.loads(data) if data else {}
        except error.HTTPError as exc:
            raise JiraError(f"Jira request failed (HTTP {exc.code}).") from None
        except (error.URLError, TimeoutError, OSError, ValueError):
            raise JiraError("Jira request failed (network or invalid JSON).") from None


def render_adf(node):
    """Turn Jira v3 ADF into readable Markdown, preserving lists and headings."""
    if node is None:
        return ""
    if isinstance(node, str):
        return node
    kind = node.get("type", "")
    children = node.get("content", [])
    if kind == "text":
        return node.get("text", "")
    if kind == "hardBreak":
        return "\n"
    if kind == "mention":
        return node.get("attrs", {}).get("text", "")
    if kind in ("orderedList", "bulletList"):
        return "\n".join(
            (f"{i}. " if kind == "orderedList" else "- ") + render_adf(child).strip()
            for i, child in enumerate(children, node.get("attrs", {}).get("order", 1))
        ) + "\n\n"
    text = "".join(render_adf(child) for child in children)
    if kind == "heading":
        return "#" * node.get("attrs", {}).get("level", 2) + " " + text + "\n\n"
    if kind in ("paragraph", "codeBlock"):
        return text + "\n\n"
    return text


def acceptance_criteria(text, heading="Acceptance Criteria"):
    lines = text.splitlines()
    for index, line in enumerate(lines):
        normalized = re.sub(r"^(?:#{1,6}\s+|h[1-6]\.\s+)", "", line.strip())
        if normalized.strip("* _#:").casefold() == heading.casefold():
            end = next((i for i in range(index + 1, len(lines))
                        if re.match(r"^(?:#{1,6}\s+|h[1-6]\.\s+|\*\*[^*]+\*\*\s*$)",
                                    lines[i])), len(lines))
            return "\n".join(lines[index + 1:end]).strip()
    return ""


def fetch_ticket(client, config, issue_key=None):
    if not issue_key:
        # TICKET_PROJECT is the real key; JIRA_PROJECT may be a human tenant label.
        project = config.get("TICKET_PROJECT") or config.get("JIRA_PROJECT", "")
        if not re.fullmatch(r"[A-Za-z][A-Za-z0-9_]*", project):
            raise JiraError("Configure a valid Jira project key.")
        result = client.call("POST", "/rest/api/3/search/jql", {
            "jql": f'project = "{project}" AND issuetype = Story ORDER BY created ASC, key ASC',
            "maxResults": 1,
            "fields": ["summary"],
        })
        if not result.get("issues"):
            raise JiraError("No Story found in the configured Jira project.")
        issue_key = result["issues"][0]["key"]
    if not re.fullmatch(r"[A-Za-z][A-Za-z0-9_]*-\d+", issue_key):
        raise JiraError("Invalid Jira issue key.")
    ac_field = config.get("AC_FIELD", "")
    fields = "summary,description,status,issuetype,updated"
    if ac_field:
        fields += "," + ac_field
    issue = client.call("GET", f"/rest/api/3/issue/{issue_key}?fields={fields}")
    data = issue["fields"]
    description = render_adf(data.get("description")).strip()
    ac = (render_adf(data.get(ac_field)).strip() if ac_field else
          acceptance_criteria(description, config.get("AC_HEADING", "Acceptance Criteria")))
    return {
        "source": "jira", "key": issue["key"], "summary": data["summary"],
        "description": description, "acceptance_criteria": ac,
        "url": client.site + "/browse/" + issue["key"],
    }


def intake(root, config, issue_key=None, client_factory=JiraClient):
    try:
        return fetch_ticket(client_factory(config), config, issue_key)
    except (JiraError, KeyError, TypeError, AttributeError):
        # Never attach a local NL-* story to an unrelated live Jira key.
        print("Jira intake unavailable; using offline BACKLOG.md, not live ticket data.",
              file=sys.stderr)
        return {"source": "backlog", "key": None, "requested_key": issue_key,
                "summary": "Offline Northline backlog",
                "description": (root / "BACKLOG.md").read_text(),
                "acceptance_criteria": "", "url": None}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("issue_key", nargs="?", help="Default: oldest Story in the project")
    parser.add_argument("--request", nargs="+", metavar="ARG",
                        help="Raw METHOD PATH [BODY_FILE]; never falls back")
    args = parser.parse_args()
    try:
        config = read_config(ROOT)
        if args.request:
            if len(args.request) not in (2, 3):
                parser.error("--request needs METHOD PATH [BODY_FILE]")
            method, path = args.request[:2]
            body = json.loads(Path(args.request[2]).read_text()) if len(args.request) == 3 else None
            result = JiraClient(config).call(method, path, body)
        else:
            result = intake(ROOT, config, args.issue_key or os.getenv("JIRA_ISSUE_KEY"))
        print(json.dumps(result, indent=2))
    except (JiraError, OSError, ValueError):
        print("Jira helper failed; check configuration, credentials, path, and body file.",
              file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())

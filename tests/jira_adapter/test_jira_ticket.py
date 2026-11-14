import contextlib
import importlib.util
import io
import json
import os
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch
from urllib import error


ROOT = Path(__file__).resolve().parents[2]
SCRIPT = ROOT / ".claude/skills/batch-remediate/assets/jira_ticket.py"
SPEC = importlib.util.spec_from_file_location("jira_ticket", SCRIPT)
jira = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(jira)


def paragraph(text):
    return {"type": "paragraph", "content": [{"type": "text", "text": text}]}


def issue(ac=True):
    content = [paragraph("As a store manager, I want a low-stock endpoint.")]
    if ac:
        content += [
            {"type": "heading", "attrs": {"level": 3},
             "content": [{"type": "text", "text": "Acceptance Criteria"}]},
            {"type": "orderedList", "content": [
                {"type": "listItem", "content": [paragraph("Default threshold is 10.")]},
                {"type": "listItem", "content": [paragraph("Requires authentication.")]},
            ]},
            {"type": "heading", "attrs": {"level": 3},
             "content": [{"type": "text", "text": "Notes"}]},
            paragraph("Not acceptance criteria."),
        ]
    return {"key": "KAN-4", "fields": {
        "summary": "Add low-stock alert endpoint for Products",
        "description": {"type": "doc", "version": 1, "content": content},
    }}


class FakeClient:
    site = "https://aipersona.atlassian.net"

    def __init__(self, data=None):
        self.data = issue() if data is None else data
        self.calls = []

    def call(self, method, path, body=None):
        self.calls.append((method, path, body))
        if path.endswith("/search/jql"):
            return {"issues": [{"key": "KAN-4"}]}
        return self.data


class IntakeTests(unittest.TestCase):
    def test_config_has_placeholder_not_token(self):
        config = jira.read_config(ROOT)
        self.assertEqual(config["JIRA_API_TOKEN"], "${JIRA_API_TOKEN}")
        self.assertEqual(config["JIRA_PROJECT"], "NORTHLINE")
        self.assertEqual(config["TICKET_PROJECT"], "KAN")
        self.assertEqual(config["JIRA_EMAIL"], "mitchell@aipersona.dev")
        self.assertEqual(jira.ROOT, ROOT)

    def test_explicit_key_fetches_v3_and_extracts_adf_ac(self):
        client = FakeClient()
        result = jira.fetch_ticket(client, {}, "KAN-4")
        self.assertEqual(client.calls[0][0], "GET")
        self.assertTrue(client.calls[0][1].startswith("/rest/api/3/issue/KAN-4?"))
        self.assertEqual(result["source"], "jira")
        self.assertEqual(result["summary"], issue()["fields"]["summary"])
        self.assertIn("As a store manager", result["description"])
        self.assertEqual(result["acceptance_criteria"],
                         "1. Default threshold is 10.\n2. Requires authentication.")
        self.assertEqual(result["url"], client.site + "/browse/KAN-4")

    def test_omitted_key_selects_first_story_in_canonical_project(self):
        client = FakeClient()
        jira.fetch_ticket(client, {"TICKET_PROJECT": "KAN", "JIRA_PROJECT": "NORTHLINE"})
        method, path, body = client.calls[0]
        self.assertEqual((method, path), ("POST", "/rest/api/3/search/jql"))
        self.assertEqual(body["maxResults"], 1)
        self.assertEqual(body["jql"],
                         'project = "KAN" AND issuetype = Story ORDER BY created ASC, key ASC')
        self.assertEqual(client.calls[1][0], "GET")

    def test_success_without_ac_never_reads_backlog(self):
        client = FakeClient(issue(ac=False))
        result = jira.intake(Path("/no-backlog-here"), {}, "KAN-4", lambda _: client)
        self.assertEqual(result["source"], "jira")
        self.assertEqual(result["acceptance_criteria"], "")

    def test_custom_ac_field(self):
        data = issue()
        data["fields"]["customfield_123"] = "Separate acceptance criteria."
        client = FakeClient(data)
        result = jira.fetch_ticket(client, {"AC_FIELD": "customfield_123"}, "KAN-4")
        self.assertEqual(result["acceptance_criteria"], "Separate acceptance criteria.")
        self.assertIn("customfield_123", client.calls[0][1])

    def test_http_failure_returns_explicit_offline_backlog(self):
        for status in (401, 403, 404, 500):
            with self.subTest(status=status), tempfile.TemporaryDirectory() as tmp:
                root = Path(tmp)
                (root / "BACKLOG.md").write_text("# Offline backlog\nLocal requirements")
                client = FakeClient()
                client.call = lambda *a: (_ for _ in ()).throw(
                    jira.JiraError(f"Jira request failed (HTTP {status})."))
                warning = io.StringIO()
                with contextlib.redirect_stderr(warning):
                    result = jira.intake(root, {}, "KAN-4", lambda _: client)
                self.assertEqual(result["source"], "backlog")
                self.assertIsNone(result["key"])
                self.assertEqual(result["requested_key"], "KAN-4")
                self.assertIn("Local requirements", result["description"])
                self.assertIn("not live ticket data", warning.getvalue())

    def test_plain_markdown_and_wiki_criteria(self):
        for heading in ("## Acceptance Criteria", "**Acceptance Criteria**",
                        "h2. Acceptance Criteria", "Acceptance Criteria"):
            self.assertEqual(jira.acceptance_criteria(f"{heading}\nMust pass.\n## Notes\nIgnore"),
                             "Must pass.")

    def test_invalid_key_never_reaches_transport(self):
        client = FakeClient()
        with self.assertRaises(jira.JiraError):
            jira.fetch_ticket(client, {}, "../other-site")
        self.assertEqual(client.calls, [])

    def test_no_story_is_a_jira_failure(self):
        client = FakeClient()
        client.call = lambda *a: {"issues": []}
        with self.assertRaises(jira.JiraError):
            jira.fetch_ticket(client, {"TICKET_PROJECT": "KAN"})


class TransportTests(unittest.TestCase):
    def client(self):
        # Synthetic test credential only; never contacts Jira.
        return jira.JiraClient({"JIRA_SITE": "https://example.atlassian.net",
                                "JIRA_EMAIL": "test@example.invalid"})

    @patch.dict(os.environ, {"JIRA_API_TOKEN": "synthetic-test-only"}, clear=True)
    def test_auth_in_header_and_timeout_not_url(self):
        client = self.client()
        with patch.object(client.opener, "open") as opened:
            opened.return_value.__enter__.return_value.read.return_value = b'{"key":"KAN-4"}'
            self.assertEqual(client.call("GET", "/rest/api/3/issue/KAN-4"), {"key": "KAN-4"})
            req = opened.call_args.args[0]
            self.assertNotIn("synthetic-test-only", req.full_url)
            self.assertTrue(req.get_header("Authorization").startswith("Basic "))
            self.assertEqual(opened.call_args.kwargs["timeout"], 30)

    @patch.dict(os.environ, {}, clear=True)
    def test_token_cannot_come_from_config(self):
        with self.assertRaises(jira.JiraError):
            jira.JiraClient({"JIRA_SITE": "https://example.atlassian.net",
                             "JIRA_EMAIL": "test@example.invalid",
                             "JIRA_API_TOKEN": "must-not-be-used"})

    @patch.dict(os.environ, {"JIRA_API_TOKEN": "synthetic-test-only"}, clear=True)
    def test_http_errors_do_not_expose_response_or_credentials(self):
        client = self.client()
        with patch.object(client.opener, "open", side_effect=error.HTTPError(
                "https://example.atlassian.net", 401, "secret-body", {}, None)):
            with self.assertRaisesRegex(jira.JiraError, r"HTTP 401") as caught:
                client.call("GET", "/rest/api/3/issue/KAN-4")
            self.assertNotIn("secret-body", str(caught.exception))
            self.assertNotIn("synthetic-test-only", str(caught.exception))

    @patch.dict(os.environ, {"JIRA_API_TOKEN": "synthetic-test-only"}, clear=True)
    def test_unsafe_origins_paths_and_redirects_rejected(self):
        for site in ("http://example.com", "https://user:pass@example.com",
                     "https://example.com/path"):
            with self.assertRaises(jira.JiraError):
                jira.JiraClient({"JIRA_SITE": site})
        with self.assertRaises(jira.JiraError):
            self.client().call("GET", "https://other.example/rest/api/3/issue/KAN-4")
        with self.assertRaises(jira.JiraError):
            jira.NoRedirect().redirect_request(None, None, 302, "", {}, "https://other.example")

    @patch.dict(os.environ, {"JIRA_API_TOKEN": "synthetic-test-only"}, clear=True)
    def test_invalid_json_is_a_safe_failure(self):
        client = self.client()
        with patch.object(client.opener, "open") as opened:
            opened.return_value.__enter__.return_value.read.return_value = b"not-json"
            with self.assertRaises(jira.JiraError):
                client.call("GET", "/rest/api/3/issue/KAN-4")


if __name__ == "__main__":
    unittest.main()

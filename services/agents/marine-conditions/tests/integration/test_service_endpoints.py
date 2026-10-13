"""Integration tests for HTTP service endpoints."""

import http.server
import json
import threading
import time
import unittest
import urllib.error
import urllib.request
from typing import Any, Dict, Tuple

from app import AgentHttpHandler, AGENT_INSTANCE
from tests.fixtures.mock_data import (
    MOCK_MARINE_RESPONSE,
    MOCK_WEATHER_RESPONSE,
    SAMPLE_INPUT_DICT,
)
from tools import ToolRegistry


class TestServiceEndpoints(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        # Configure the global agent instance with mock transport for network-isolated tests
        def mock_transport(url: str, timeout: float) -> Tuple[int, Dict[str, Any]]:
            if "marine" in url:
                return 200, MOCK_MARINE_RESPONSE
            return 200, MOCK_WEATHER_RESPONSE

        AGENT_INSTANCE.tool_registry = ToolRegistry(transport=mock_transport)

        # Bind to port 0 to get an ephemeral free port
        cls.server = http.server.HTTPServer(("127.0.0.1", 0), AgentHttpHandler)
        cls.port = cls.server.server_address[1]
        cls.server_thread = threading.Thread(target=cls.server.serve_forever, daemon=True)
        cls.server_thread.start()
        time.sleep(0.1)

    @classmethod
    def tearDownClass(cls):
        cls.server.shutdown()
        cls.server.server_close()
        cls.server_thread.join(timeout=1.0)

    def _url(self, path: str) -> str:
        return f"http://127.0.0.1:{self.port}{path}"

    def test_health_endpoint(self):
        req = urllib.request.Request(self._url("/health"))
        with urllib.request.urlopen(req, timeout=5.0) as resp:
            self.assertEqual(resp.status, 200)
            data = json.loads(resp.read().decode("utf-8"))
            self.assertEqual(data["status"], "Healthy")
            self.assertEqual(data["service"], "blueverse-agent-marine-conditions")
            self.assertIn("Sanuda Abeysinghe", data["owner"])

    def test_health_endpoint_with_query_params(self):
        req = urllib.request.Request(self._url("/health?probe=ready"))
        with urllib.request.urlopen(req, timeout=5.0) as resp:
            self.assertEqual(resp.status, 200)
            data = json.loads(resp.read().decode("utf-8"))
            self.assertEqual(data["status"], "Healthy")

    def test_agent_namespaced_health_endpoint(self):
        req = urllib.request.Request(self._url("/api/agents/marine-conditions/health"))
        with urllib.request.urlopen(req, timeout=5.0) as resp:
            self.assertEqual(resp.status, 200)
            data = json.loads(resp.read().decode("utf-8"))
            self.assertEqual(data["status"], "Healthy")
            self.assertEqual(data["role"], "Marine Conditions Intelligence Agent")
            self.assertIn("endpoints", data)

    def test_agent_namespaced_invocations_endpoint(self):
        req = urllib.request.Request(
            self._url("/api/agents/marine-conditions/invocations"),
            data=json.dumps(SAMPLE_INPUT_DICT).encode("utf-8"),
            headers={"Content-Type": "application/json"},
            method="POST",
        )
        with urllib.request.urlopen(req, timeout=5.0) as resp:
            self.assertEqual(resp.status, 200)
            report = json.loads(resp.read().decode("utf-8"))
            self.assertEqual(report["workflow_id"], SAMPLE_INPUT_DICT["workflow_id"])
            self.assertEqual(report["status"], "COMPLETE")

    def test_invocations_endpoint_success(self):
        req = urllib.request.Request(
            self._url("/invocations"),
            data=json.dumps(SAMPLE_INPUT_DICT).encode("utf-8"),
            headers={"Content-Type": "application/json"},
            method="POST",
        )
        with urllib.request.urlopen(req, timeout=5.0) as resp:
            self.assertEqual(resp.status, 200)
            report = json.loads(resp.read().decode("utf-8"))
            self.assertEqual(report["workflow_id"], SAMPLE_INPUT_DICT["workflow_id"])
            self.assertEqual(report["status"], "COMPLETE")
            self.assertIn("wind_speed", report["environmental_factors"])
            self.assertEqual(report["environmental_factors"]["wind_speed"]["value"], 12.5)

    def test_report_alias_endpoint_success(self):
        req = urllib.request.Request(
            self._url("/report"),
            data=json.dumps(SAMPLE_INPUT_DICT).encode("utf-8"),
            headers={"Content-Type": "application/json"},
            method="POST",
        )
        with urllib.request.urlopen(req, timeout=5.0) as resp:
            self.assertEqual(resp.status, 200)
            report = json.loads(resp.read().decode("utf-8"))
            self.assertEqual(report["status"], "COMPLETE")

    def test_invocations_endpoint_malformed_json(self):
        req = urllib.request.Request(
            self._url("/invocations"),
            data=b"not-valid-json{",
            headers={"Content-Type": "application/json"},
            method="POST",
        )
        with self.assertRaises(urllib.error.HTTPError) as cm:
            urllib.request.urlopen(req, timeout=5.0)
        self.assertEqual(cm.exception.code, 400)
        err = json.loads(cm.exception.read().decode("utf-8"))
        self.assertEqual(err["title"], "Bad Request")

    def test_invocations_endpoint_missing_fields(self):
        invalid_body = {"activity": "snorkeling"}  # Missing workflow_id, location, etc.
        req = urllib.request.Request(
            self._url("/invocations"),
            data=json.dumps(invalid_body).encode("utf-8"),
            headers={"Content-Type": "application/json"},
            method="POST",
        )
        with self.assertRaises(urllib.error.HTTPError) as cm:
            urllib.request.urlopen(req, timeout=5.0)
        self.assertEqual(cm.exception.code, 400)
        err = json.loads(cm.exception.read().decode("utf-8"))
        self.assertIn("Validation error", err["detail"])

    def test_not_found_endpoint(self):
        req = urllib.request.Request(self._url("/non-existent-route"))
        with self.assertRaises(urllib.error.HTTPError) as cm:
            urllib.request.urlopen(req, timeout=5.0)
        self.assertEqual(cm.exception.code, 404)


if __name__ == "__main__":
    unittest.main()

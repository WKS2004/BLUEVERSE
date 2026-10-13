from __future__ import annotations

from datetime import datetime, timedelta, timezone
import json
import threading
import urllib.error
import urllib.request
import unittest
from uuid import uuid4

from blueverse_safety_operations.agent import ModelCall, SafetyOperationsAgent
from blueverse_safety_operations.contracts import INPUT_SCHEMA_VERSION, OUTPUT_SCHEMA_VERSION
from blueverse_safety_operations.server import (
    SAFETY_OPERATIONS_DISPATCH_PATH,
    SAFETY_OPERATIONS_HEALTH_PATH,
    create_server,
)


def valid_input() -> dict[str, object]:
    now = datetime.now(timezone.utc).replace(microsecond=0)
    categories = (
        "planner_report",
        "experience_report",
        "marine_report",
        "operations_context",
        "safety_profile",
    )
    return {
        "schemaVersion": INPUT_SCHEMA_VERSION,
        "workflowId": str(uuid4()),
        "stepId": "safety-assessment",
        "dispatchId": str(uuid4()),
        "assessmentId": str(uuid4()),
        "correlationId": "corr-server-test-01",
        "target": {
            "type": "ACTIVITY",
            "id": str(uuid4()),
            "currentState": "ACTIVE",
            "stateVersion": 1,
        },
        "period": {
            "startsAt": (now + timedelta(minutes=5)).isoformat().replace("+00:00", "Z"),
            "endsAt": (now + timedelta(hours=2)).isoformat().replace("+00:00", "Z"),
        },
        "objective": "Assess the supplied activity evidence.",
        "policyVersion": "operations-policy.v1",
        "allowedActions": ["TEMPORARILY_SUSPEND"],
        "reports": [
            {
                "reference": str(uuid4()),
                "category": category,
                "status": "VALID",
                "source": f"service-{category}",
                "observedAt": (now - timedelta(minutes=1)).isoformat().replace("+00:00", "Z"),
                "validUntil": (now + timedelta(hours=3)).isoformat().replace("+00:00", "Z"),
                "facts": {},
            }
            for category in categories
        ],
    }


class FixedModel:
    def __init__(self) -> None:
        self.calls: list[ModelCall] = []

    def generate(self, request: ModelCall) -> dict[str, object]:
        self.calls.append(request)
        context = request.context
        target = context["target"]
        reports = context["reports"]
        return {
            "schemaVersion": OUTPUT_SCHEMA_VERSION,
            "workflowId": context["workflowId"],
            "stepId": context["stepId"],
            "assessmentId": context["assessmentId"],
            "targetId": target["id"],
            "recommendation": "PROPOSE_ACTION",
            "summary": "The validated evidence supports a temporary restriction.",
            "proposedAction": "TEMPORARILY_SUSPEND",
            "evidenceReferences": [report["reference"] for report in reports],
            "uncertainties": ["A reviewer should confirm the current conditions."],
            "reviewSuggestion": "REVIEW_RECOMMENDED",
        }


class HttpServiceTests(unittest.TestCase):
    def setUp(self) -> None:
        self.service_key = "test-only-service-key"
        self.environment = {
            "AGENTIC_ENABLED": "true",
            "AGENTIC_SERVICE_KEY": self.service_key,
            "SAFETY_OPERATIONS_GEMINI_ENABLED": "false",
        }
        self.model = FixedModel()
        self.agent = SafetyOperationsAgent(self.model)
        self.server = create_server(
            "127.0.0.1",
            0,
            environment=self.environment,
            agent=self.agent,
        )
        self.thread = threading.Thread(target=self.server.serve_forever, daemon=True)
        self.thread.start()
        self.root = f"http://127.0.0.1:{self.server.server_port}"

    def tearDown(self) -> None:
        self.server.shutdown()
        self.server.server_close()
        self.thread.join(timeout=2)

    def request(
        self,
        method: str,
        path: str,
        payload: object | None = None,
        *,
        service_key: str | None = None,
    ) -> tuple[int, dict[str, object]]:
        headers: dict[str, str] = {}
        data = None
        if payload is not None:
            headers["Content-Type"] = "application/json"
            data = json.dumps(payload).encode("utf-8")
        if service_key is not None:
            headers["X-Blueverse-Service-Key"] = service_key
        request = urllib.request.Request(self.root + path, data=data, headers=headers, method=method)
        try:
            with urllib.request.urlopen(request, timeout=2) as response:
                status = response.status
                body = response.read()
        except urllib.error.HTTPError as response:
            status = response.code
            body = response.read()
        return status, json.loads(body)

    def test_liveness_is_anonymous_and_reports_container_health(self) -> None:
        status, body = self.request("GET", "/health/live")

        self.assertEqual(200, status)
        self.assertEqual({"status": "HEALTHY"}, body)

    def test_private_health_requires_service_key_and_reports_readiness(self) -> None:
        unauthorized_status, unauthorized = self.request("GET", SAFETY_OPERATIONS_HEALTH_PATH)
        unauthorized_dispatch_status, unauthorized_dispatch = self.request(
            "POST",
            SAFETY_OPERATIONS_DISPATCH_PATH,
            valid_input(),
        )
        ready_status, ready = self.request(
            "GET", SAFETY_OPERATIONS_HEALTH_PATH, service_key=self.service_key
        )

        self.assertEqual(401, unauthorized_status)
        self.assertEqual({"status": "UNAUTHORIZED"}, unauthorized)
        self.assertEqual(401, unauthorized_dispatch_status)
        self.assertEqual({"status": "UNAUTHORIZED"}, unauthorized_dispatch)
        self.assertEqual(200, ready_status)
        self.assertEqual({"status": "AVAILABLE"}, ready)
        self.assertEqual([], self.model.calls)

    def test_dispatch_rejects_invalid_input_before_calling_the_agent(self) -> None:
        status, body = self.request(
            "POST",
            SAFETY_OPERATIONS_DISPATCH_PATH,
            {"schemaVersion": INPUT_SCHEMA_VERSION},
            service_key=self.service_key,
        )

        self.assertEqual(400, status)
        self.assertEqual(
            {"contractVersion": 1, "status": "INVALID", "errorCode": "REQUEST_INVALID"},
            body,
        )
        self.assertEqual([], self.model.calls)

    def test_dispatch_returns_planner_report_contract_for_validated_stub_result(self) -> None:
        request = valid_input()
        status, body = self.request(
            "POST",
            SAFETY_OPERATIONS_DISPATCH_PATH,
            request,
            service_key=self.service_key,
        )

        self.assertEqual(200, status)
        self.assertEqual(1, body["contractVersion"])
        self.assertEqual(request["workflowId"], body["workflowId"])
        self.assertEqual(request["stepId"], body["stepId"])
        self.assertEqual("safety_operations", body["agentId"])
        self.assertEqual("SafetyOperationsRecommendation.v1", body["reportType"])
        self.assertEqual("SUCCEEDED", body["status"])
        self.assertEqual(request["workflowId"], body["report"]["workflowId"])
        self.assertEqual(
            [report["reference"] for report in request["reports"]],
            body["sourceReferences"],
        )
        self.assertEqual(
            ["A reviewer should confirm the current conditions."],
            body["uncertaintyNotes"],
        )
        self.assertTrue(body["producedAtUtc"].endswith("Z"))
        self.assertEqual(1, len(self.model.calls))

    def test_dispatch_is_unavailable_when_agentic_execution_defaults_to_disabled(self) -> None:
        self.server.shutdown()
        self.server.server_close()
        self.thread.join(timeout=2)
        self.environment.pop("AGENTIC_ENABLED")
        self.server = create_server(
            "127.0.0.1",
            0,
            environment=self.environment,
            agent=self.agent,
        )
        self.thread = threading.Thread(target=self.server.serve_forever, daemon=True)
        self.thread.start()
        self.root = f"http://127.0.0.1:{self.server.server_port}"

        status, body = self.request(
            "POST",
            SAFETY_OPERATIONS_DISPATCH_PATH,
            valid_input(),
            service_key=self.service_key,
        )

        self.assertEqual(503, status)
        self.assertEqual("UNAVAILABLE", body["status"])
        self.assertEqual("EXECUTION_DISABLED", body["errorCode"])
        self.assertEqual([], self.model.calls)

    def test_dispatch_is_unavailable_when_gemini_is_not_configured(self) -> None:
        self.server.shutdown()
        self.server.server_close()
        self.thread.join(timeout=2)
        self.server = create_server(
            "127.0.0.1",
            0,
            environment={
                "AGENTIC_ENABLED": "true",
                "AGENTIC_SERVICE_KEY": self.service_key,
                "SAFETY_OPERATIONS_GEMINI_ENABLED": "false",
            },
        )
        self.thread = threading.Thread(target=self.server.serve_forever, daemon=True)
        self.thread.start()
        self.root = f"http://127.0.0.1:{self.server.server_port}"

        status, body = self.request(
            "POST",
            SAFETY_OPERATIONS_DISPATCH_PATH,
            valid_input(),
            service_key=self.service_key,
        )

        self.assertEqual(503, status)
        self.assertEqual("UNAVAILABLE", body["status"])
        self.assertEqual("MODEL_NOT_CONFIGURED", body["errorCode"])


if __name__ == "__main__":
    unittest.main()

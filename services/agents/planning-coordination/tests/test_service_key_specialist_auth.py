"""AI-PC-SVC-01: Experience auth depends on workflow and caller delegation."""

from __future__ import annotations

import json
import sys
import unittest
from datetime import datetime, timezone
from pathlib import Path
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from planning_coordination.clients import DependencyUnavailable, HttpSpecialistClient


def _body(workflow_type: str) -> dict[str, object]:
    return {
        "workflowId": "51e814ba-89e7-4a1e-82eb-34d7c51ab315",
        "stepId": "experience",
        "workflowType": workflow_type,
        "objective": "Collect published coastal experience context.",
        "agentContext": {"destinationId": "637a33d2-ae51-4c3f-85cc-25c2a5b4024f"},
        "timeRange": {"startsAt": "2026-10-14T09:00:00+00:00", "endsAt": "2026-10-14T11:00:00+00:00"},
        "constraints": {},
    }


def _specialist_result(body: dict[str, object]) -> dict[str, object]:
    return {
        "workflow_id": body["workflowId"],
        "step_id": body["stepId"],
        "completion_outcome": "success_optional_data_unavailable",
        "evidence_references": ["Offering:published-catalogue"],
        "uncertainty_and_gaps": ["Biodiversity is outside the service-key catalogue scope."],
        "biodiversity": {"status": "unavailable"},
        "offering": {"availability_status": "AVAILABLE", "publication_status": "PUBLISHED"},
        "model_execution": {"synthesis_summary": "Published experience context was collected."},
        "generated_at": datetime.now(timezone.utc).isoformat(),
    }


class FakeResponse:
    def __init__(self, payload: dict[str, object]):
        self.payload = payload

    def __enter__(self):
        return self

    def __exit__(self, *_args):
        return False

    def read(self, _limit: int) -> bytes:
        return json.dumps(self.payload).encode()


class ExperienceServiceKeyDispatchTests(unittest.TestCase):
    def setUp(self):
        self.client = HttpSpecialistClient(
            service_key="internal-only-key",
            endpoints={"experience_biodiversity": "http://experience-agent"},
        )

    def test_operational_assessment_uses_service_key_without_jwt(self):
        """AI-PC-SVC-01: callerless Ops dispatch uses only the service-key header."""
        body = _body("OPERATIONAL_ASSESSMENT")
        with patch("planning_coordination.clients.urllib.request.urlopen", return_value=FakeResponse(_specialist_result(body))) as send:
            result = self.client.dispatch("experience_biodiversity", body)
        self.assertEqual("SUCCEEDED", result["status"])
        request = send.call_args.args[0]
        headers = {name.lower(): value for name, value in request.header_items()}
        self.assertEqual("internal-only-key", headers["x-blueverse-service-key"])
        self.assertNotIn("authorization", headers)
        self.assertNotIn("internal-only-key", request.data.decode())

    def test_tourist_request_never_falls_back_to_service_key(self):
        """AI-PC-SVC-02: tourist dispatch requires its caller Bearer token."""
        with self.assertRaises(DependencyUnavailable):
            self.client.dispatch("experience_biodiversity", _body("TOURIST_RECOMMENDATION"))

    def test_tourist_request_preserves_bearer_when_service_key_is_configured(self):
        """AI-PC-SVC-03: delegated user authorization takes precedence for tourist workflows."""
        body = _body("TOURIST_RECOMMENDATION")
        with patch("planning_coordination.clients.urllib.request.urlopen", return_value=FakeResponse(_specialist_result(body))) as send:
            self.client.dispatch("experience_biodiversity", body, delegated_access_token="caller-jwt")
        request = send.call_args.args[0]
        headers = {name.lower(): value for name, value in request.header_items()}
        self.assertEqual("Bearer caller-jwt", headers["authorization"])
        self.assertNotIn("x-blueverse-service-key", headers)

    def test_operational_assessment_can_still_use_bearer_when_present(self):
        """AI-PC-SVC-04: existing delegated Bearer behavior remains available."""
        body = _body("OPERATIONAL_ASSESSMENT")
        with patch("planning_coordination.clients.urllib.request.urlopen", return_value=FakeResponse(_specialist_result(body))) as send:
            self.client.dispatch("experience_biodiversity", body, delegated_access_token="caller-jwt")
        headers = {name.lower(): value for name, value in send.call_args.args[0].header_items()}
        self.assertEqual("Bearer caller-jwt", headers["authorization"])
        self.assertNotIn("x-blueverse-service-key", headers)

    def test_operational_assessment_without_key_or_bearer_fails_closed(self):
        """AI-PC-SVC-05: no configured credential cannot trigger an Experience dispatch."""
        client = HttpSpecialistClient(service_key="", endpoints={"experience_biodiversity": "http://experience-agent"})
        with self.assertRaises(DependencyUnavailable):
            client.dispatch("experience_biodiversity", _body("OPERATIONAL_ASSESSMENT"))

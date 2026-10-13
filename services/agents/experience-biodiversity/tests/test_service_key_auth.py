"""AI-EB-SVC-01: service-key dispatch is read-only and catalogue-scoped."""

from __future__ import annotations

import json
import io
from email.message import Message
from datetime import datetime, timedelta, timezone
from uuid import uuid4

from blueverse_experience_agent.agent import CoastalExperienceAgent
from blueverse_experience_agent.catalogue_tools import PublishedCatalogueOnlyTools
from blueverse_experience_agent.schemas import ExperienceAgentInput
from blueverse_experience_agent.server import AgentHttpHandler


class CapturingCatalogueTools(PublishedCatalogueOnlyTools):
    def __init__(self, row: dict[str, object]):
        super().__init__(experience_service_url="http://catalogue.test", planner_service_url="http://planner.test")
        self.row = row
        self.requests: list[tuple[str, str | None]] = []

    def _execute_http_get(self, url: str, auth_token: str | None = None):
        self.requests.append((url, auth_token))
        return 200, [self.row], 1.0, 0


def _catalogue_row(destination_id: str, activity_id: str, offering_id: str) -> dict[str, object]:
    start = datetime.now(timezone.utc).replace(microsecond=0) + timedelta(days=1)
    end = start + timedelta(hours=2)
    return {
        "destinationId": destination_id,
        "destinationName": "Published Coast",
        "region": "Southern Coast",
        "latitude": 5.9,
        "longitude": 80.4,
        "activityId": activity_id,
        "activityName": "Coastal walk",
        "offeringId": offering_id,
        "title": "Guided coast walk",
        "availabilityStatus": "AVAILABLE",
        "publicationState": "PUBLISHED",
        "availableFrom": start.isoformat(),
        "availableUntil": end.isoformat(),
        "experienceLevels": ["Beginner"],
        "checkedAt": datetime.now(timezone.utc).isoformat(),
        "timeZone": "UTC",
    }


def test_service_mode_reads_only_minimized_catalogue_and_discards_bearer():
    """AI-EB-SVC-01: no private detail endpoint or planner call occurs in service mode."""
    destination_id, activity_id, offering_id = (str(uuid4()) for _ in range(3))
    tools = CapturingCatalogueTools(_catalogue_row(destination_id, activity_id, offering_id))
    agent = CoastalExperienceAgent(service_key_tools=tools)
    now = datetime.now(timezone.utc).replace(microsecond=0)
    agent_input = ExperienceAgentInput(
        workflow_id=str(uuid4()),
        step_id="ops-experience",
        objective="Collect published experience context for an operational review.",
        destination_id=destination_id,
        activity_id=activity_id,
        offering_id=offering_id,
        target_date=(now + timedelta(days=1)).date().isoformat(),
        period_starts_at=(now + timedelta(days=1)).isoformat(),
        period_ends_at=(now + timedelta(days=1, hours=1)).isoformat(),
        include_biodiversity=True,
    )

    report = agent.generate_report(agent_input, auth_token="must-not-forward", service_mode=True)

    assert report.offering is not None
    assert report.offering.publication_status == "PUBLISHED"
    assert report.biodiversity.status.value == "unavailable"
    assert tools.requests
    assert all("/api/experiences/catalogue?" in url for url, _ in tools.requests)
    assert all(token is None for _, token in tools.requests)


class RecordingAgent:
    class Gemini:
        class Config:
            model = "test-model"
            is_configured = False

        config = Config()

    gemini_client = Gemini()

    def __init__(self):
        self.calls: list[tuple[str | None, bool]] = []

    def generate_report(self, _agent_input, auth_token=None, service_mode=False):
        self.calls.append((auth_token, service_mode))

        class Report:
            def to_dict(self):
                return {"accepted": True}

        return Report()


def _dispatch(headers: dict[str, str]) -> tuple[int, RecordingAgent]:
    payload = json.dumps({
        "workflow_id": str(uuid4()),
        "step_id": "ops-step",
        "objective": "Read the published catalogue for an operational assessment.",
        "destination_id": str(uuid4()),
    }).encode()
    handler = AgentHttpHandler.__new__(AgentHttpHandler)
    handler.path = "/api/experiences/agent/context-reports"
    handler.headers = Message()
    handler.headers.add_header("Content-Type", "application/json")
    for name, value in headers.items():
        handler.headers.add_header(name, value)
    handler.headers.add_header("Content-Length", str(len(payload)))
    handler.rfile = io.BytesIO(payload)
    recording_agent = RecordingAgent()
    handler.agent = recording_agent
    responses: list[tuple[int, dict[str, object]]] = []
    handler._send_json = lambda status, data: responses.append((status, data))
    handler.do_POST()
    assert responses
    return responses[-1][0], recording_agent


def test_service_key_and_bearer_are_alternative_authentication_paths(monkeypatch):
    """AI-EB-SVC-02: service key is accepted; Bearer path remains supported; ambiguity fails closed."""
    monkeypatch.setenv("AGENTIC_SERVICE_KEY", "test-internal-key")
    status, recording_agent = _dispatch({"X-Blueverse-Service-Key": "test-internal-key"})
    assert status == 200
    assert recording_agent.calls == [(None, True)]

    status, recording_agent = _dispatch({"Authorization": "Bearer caller-token"})
    assert status == 200
    assert recording_agent.calls == [("caller-token", False)]

    status, recording_agent = _dispatch({
        "Authorization": "Bearer caller-token",
        "X-Blueverse-Service-Key": "test-internal-key",
    })
    assert status == 401
    assert recording_agent.calls == []

    status, recording_agent = _dispatch({"X-Blueverse-Service-Key": "wrong-key"})
    assert status == 401
    assert recording_agent.calls == []

    status, recording_agent = _dispatch({
        "Authorization": "Basic ambiguous",
        "X-Blueverse-Service-Key": "test-internal-key",
    })
    assert status == 401
    assert recording_agent.calls == []

"""Private HTTP transport for the Safety & Operations agent."""

from __future__ import annotations

from datetime import datetime, timezone
import hmac
import json
import os
from functools import partial
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from typing import Any, Mapping

from .agent import AgentResult, SafetyOperationsAgent
from .contracts import ContractError, MAX_INPUT_BYTES, SafetyAssessmentInput
from .gemini import GeminiConfig, GeminiConfigurationError, GeminiModelPort


AGENTIC_ENABLED_ENV = "AGENTIC_ENABLED"
SERVICE_KEY_ENV = "AGENTIC_SERVICE_KEY"
SAFETY_OPERATIONS_HEALTH_PATH = "/internal/agentic/agents/safety-operations/health"
SAFETY_OPERATIONS_DISPATCH_PATH = "/internal/agentic/agents/safety-operations/dispatch"
SAFETY_OPERATIONS_AGENT_ID = "safety_operations"
SAFETY_OPERATIONS_REPORT_TYPE = "SafetyOperationsRecommendation.v1"


def _is_enabled(value: str | None) -> bool:
    return isinstance(value, str) and value.strip().casefold() == "true"


def _create_agent(environment: Mapping[str, str]) -> SafetyOperationsAgent | None:
    if not _is_enabled(environment.get(AGENTIC_ENABLED_ENV)):
        return None
    try:
        config = GeminiConfig.from_environment(environment)
    except GeminiConfigurationError:
        return None
    if not config.enabled:
        return None
    return SafetyOperationsAgent(GeminiModelPort(config))


class SafetyOperationsHandler(BaseHTTPRequestHandler):
    """Small service-key protected handler with bounded JSON input."""

    server_version = "BLUEVERSE-SafetyOperations/1"

    def __init__(
        self,
        *args: Any,
        environment: Mapping[str, str],
        agent: SafetyOperationsAgent | None,
        **kwargs: Any,
    ) -> None:
        self._environment = environment
        self._agent = agent
        super().__init__(*args, **kwargs)

    def log_message(self, _format: str, *_args: object) -> None:
        return

    def do_GET(self) -> None:
        if self.path == "/health/live":
            self._send(200, {"status": "HEALTHY"})
            return
        if self.path == SAFETY_OPERATIONS_HEALTH_PATH:
            if not self._authorized():
                return
            connected = (
                _is_enabled(self._environment.get(AGENTIC_ENABLED_ENV))
                and self._agent is not None
                and bool(self._environment.get(SERVICE_KEY_ENV, "").strip())
            )
            self._send(200, {"status": "AVAILABLE" if connected else "NOT_CONNECTED"})
            return
        self._send(404, {"status": "NOT_FOUND"})

    def do_POST(self) -> None:
        if self.path != SAFETY_OPERATIONS_DISPATCH_PATH:
            self._send(404, {"status": "NOT_FOUND"})
            return
        if not self._authorized():
            return
        if not _is_enabled(self._environment.get(AGENTIC_ENABLED_ENV)):
            self._send(503, {"contractVersion": 1, "status": "UNAVAILABLE", "errorCode": "EXECUTION_DISABLED"})
            return
        if self._agent is None:
            self._send(503, {"contractVersion": 1, "status": "UNAVAILABLE", "errorCode": "MODEL_NOT_CONFIGURED"})
            return

        payload = self._read_json()
        if payload is None:
            return
        try:
            request = SafetyAssessmentInput.from_mapping(payload)
        except ContractError:
            self._send(400, {"contractVersion": 1, "status": "INVALID", "errorCode": "REQUEST_INVALID"})
            return

        result = self._agent.recommend(payload)
        self._send_result(request, result)

    def _read_json(self) -> Mapping[str, Any] | None:
        content_length = self.headers.get("Content-Length", "")
        if not content_length.isdecimal():
            self._send(400, {"contractVersion": 1, "status": "INVALID", "errorCode": "REQUEST_INVALID"})
            return None
        length = int(content_length)
        if not 1 <= length <= MAX_INPUT_BYTES:
            status = 413 if length > MAX_INPUT_BYTES else 400
            code = "REQUEST_TOO_LARGE" if status == 413 else "REQUEST_INVALID"
            self._send(status, {"contractVersion": 1, "status": "INVALID", "errorCode": code})
            return None
        try:
            payload = json.loads(self.rfile.read(length).decode("utf-8"))
        except (UnicodeDecodeError, json.JSONDecodeError):
            self._send(400, {"contractVersion": 1, "status": "INVALID", "errorCode": "REQUEST_INVALID"})
            return None
        if not isinstance(payload, Mapping):
            self._send(400, {"contractVersion": 1, "status": "INVALID", "errorCode": "REQUEST_INVALID"})
            return None
        return payload

    def _authorized(self) -> bool:
        configured = self._environment.get(SERVICE_KEY_ENV, "")
        provided = self.headers.get("X-Blueverse-Service-Key", "")
        if not configured or not hmac.compare_digest(provided.encode("utf-8"), configured.encode("utf-8")):
            self._send(401, {"status": "UNAUTHORIZED"})
            return False
        return True

    def _send_result(self, request: SafetyAssessmentInput, result: AgentResult) -> None:
        if result.status == "SUCCEEDED" and result.recommendation is not None:
            recommendation = result.recommendation.to_mapping()
            payload = {
                "contractVersion": 1,
                "workflowId": request.workflow_id,
                "stepId": request.step_id,
                "agentId": SAFETY_OPERATIONS_AGENT_ID,
                "reportType": SAFETY_OPERATIONS_REPORT_TYPE,
                "status": "SUCCEEDED",
                "report": recommendation,
                "sourceReferences": recommendation["evidenceReferences"],
                "uncertaintyNotes": recommendation["uncertainties"],
                "producedAtUtc": datetime.now(timezone.utc).isoformat().replace("+00:00", "Z"),
            }
            self._send(200, payload)
            return

        status_codes = {
            "INVALID_INPUT": 400,
            "BLOCKED": 422,
            "INSUFFICIENT_DATA": 422,
            "UNAVAILABLE": 503,
            "INVALID_RESULT": 502,
        }
        code = result.code if result.code and result.code.isascii() else "AGENT_FAILURE"
        self._send(
            status_codes.get(result.status, 503),
            {
                "contractVersion": 1,
                "workflowId": request.workflow_id,
                "stepId": request.step_id,
                "agentId": SAFETY_OPERATIONS_AGENT_ID,
                "reportType": SAFETY_OPERATIONS_REPORT_TYPE,
                "status": result.status,
                "errorCode": code,
            },
        )

    def _send(self, status: int, payload: dict[str, Any]) -> None:
        body = json.dumps(payload, ensure_ascii=False, allow_nan=False, separators=(",", ":")).encode("utf-8")
        self.send_response(status)
        self.send_header("Content-Type", "application/json")
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)


def create_server(
    host: str = "0.0.0.0",
    port: int = 8080,
    *,
    environment: Mapping[str, str] | None = None,
    agent: SafetyOperationsAgent | None = None,
) -> ThreadingHTTPServer:
    """Create a server; test callers may inject an agent without model access."""
    values = dict(os.environ if environment is None else environment)
    runtime_agent = agent if agent is not None else _create_agent(values)
    handler = partial(SafetyOperationsHandler, environment=values, agent=runtime_agent)
    return ThreadingHTTPServer((host, port), handler)


def serve() -> None:
    raw_port = os.environ.get("PORT", "8080")
    try:
        port = int(raw_port)
    except ValueError as exc:
        raise ValueError("PORT must be a valid TCP port") from exc
    if not 1 <= port <= 65535:
        raise ValueError("PORT must be a valid TCP port")
    create_server(port=port).serve_forever()

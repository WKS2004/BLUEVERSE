from __future__ import annotations

import hmac
import json
import logging
import os
import re
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from typing import Any

from .clients import HttpSpecialistClient, PlannerExecutionStore, create_model_client
from .contracts import ContractError, DispatchRequest
from .coordinator import PlanningCoordinator

logging.basicConfig(level=os.getenv("LOG_LEVEL", "INFO"), format="%(message)s")
SERVICE_KEY = os.getenv("AGENTIC_SERVICE_KEY", "")
model = create_model_client()
coordinator = PlanningCoordinator(model, HttpSpecialistClient(), PlannerExecutionStore())


def endpoint(method: str, path: str, auth: str = "service-key"):
    def decorate(function):
        function.endpoint_metadata = (method, path, auth)
        return function
    return decorate


class Handler(BaseHTTPRequestHandler):
    server_version = "BLUEVERSE-PlanningCoordination/1"

    def log_message(self, _format: str, *_args: object) -> None:
        return

    def do_GET(self) -> None:
        self._route("GET")

    def do_POST(self) -> None:
        self._route("POST")

    def _route(self, method: str) -> None:
        for name in ("live", "planning_coordination_health", "dispatch"):
            handler = getattr(self, name)
            metadata = getattr(handler, "endpoint_metadata", None)
            if metadata and metadata[0] == method and metadata[1] == self.path:
                handler()
                return
        self._send(404, {"status": "NOT_FOUND"})

    @endpoint("GET", "/health/live", auth="anonymous")
    def live(self) -> None:
        self._send(200, {"status": "HEALTHY"})

    @endpoint("GET", "/internal/agentic/agents/planning-coordination/health")
    def planning_coordination_health(self) -> None:
        if not self._authorized():
            return
        enabled = os.getenv("AGENTIC_ENABLED", "false").lower() == "true"
        ready = enabled and model.configured and bool(os.getenv("PLANNER_SERVICE_URL")) and bool(SERVICE_KEY)
        self._send(200, {"status": "AVAILABLE" if ready else "NOT_CONNECTED"})

    @endpoint("POST", "/internal/agentic/agents/planning-coordination/dispatch")
    def dispatch(self) -> None:
        if not self._authorized():
            return
        try:
            length = int(self.headers.get("Content-Length", "0"))
            if not 1 <= length <= 64_000:
                raise ContractError("request body size is invalid")
            payload = json.loads(self.rfile.read(length))
            request = DispatchRequest.parse(payload)
        except ContractError as exc:
            # Contract errors use fixed validation messages and never echo the
            # request values, so this helps diagnose clients without logging PII.
            logging.info("agent_dispatch_rejected code=REQUEST_INVALID reason=%s", str(exc))
            self._send(400, {"contractVersion": 1, "status": "INVALID", "errorCode": "REQUEST_INVALID"})
            return
        except (ValueError, UnicodeDecodeError, json.JSONDecodeError):
            logging.info("agent_dispatch_rejected code=REQUEST_INVALID reason=malformed_json_or_length")
            self._send(400, {"contractVersion": 1, "status": "INVALID", "errorCode": "REQUEST_INVALID"})
            return
        if os.getenv("AGENTIC_ENABLED", "false").lower() != "true":
            self._send(503, {"contractVersion": 1, "workflowId": request.workflow_id,
                             "status": "UNAVAILABLE", "errorCode": "EXECUTION_DISABLED"})
            return
        correlation_id = self.headers.get("X-Correlation-ID", "")
        if not re.fullmatch(r"[A-Za-z0-9._:-]{1,80}", correlation_id):
            correlation_id = None
        authorization = self.headers.get("Authorization", "")
        scheme, separator, value = authorization.partition(" ")
        delegated_access_token = value.strip() if separator and scheme.casefold() == "bearer" else None
        if delegated_access_token and (len(delegated_access_token) > 4096 or any(c in delegated_access_token for c in "\r\n")):
            self._send(400, {"contractVersion": 1, "workflowId": request.workflow_id,
                             "status": "INVALID", "errorCode": "AUTHORIZATION_INVALID"})
            return
        result = coordinator.execute(request, correlation_id, delegated_access_token)
        status = 200 if result["status"] == "COMPLETED" else 503 if result["status"] in {"UNAVAILABLE", "BLOCKED"} else 422
        self._send(status, result)

    def _authorized(self) -> bool:
        provided = self.headers.get("X-Blueverse-Service-Key", "")
        if not SERVICE_KEY or not hmac.compare_digest(provided.encode("utf-8"), SERVICE_KEY.encode("utf-8")):
            self._send(401, {"status": "UNAUTHORIZED"})
            return False
        return True

    def _send(self, status: int, payload: dict[str, Any]) -> None:
        data = json.dumps(payload, separators=(",", ":")).encode("utf-8")
        self.send_response(status)
        self.send_header("Content-Type", "application/json")
        self.send_header("Content-Length", str(len(data)))
        self.end_headers()
        self.wfile.write(data)


def serve() -> None:
    port = int(os.getenv("PORT", "8080"))
    httpd = ThreadingHTTPServer(("0.0.0.0", port), Handler)
    httpd.serve_forever()


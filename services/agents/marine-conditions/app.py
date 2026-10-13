"""HTTP service entrypoint for Marine Conditions Intelligence Agent.

Exposes:
- GET /health
- POST /invocations (and alias POST /report)
RFC 7807 problem details for structured error handling.
"""

from __future__ import annotations

import http.server
import json
import logging
import os
import sys
from typing import Any, Dict

try:
    from .agent import MarineConditionsIntelligenceAgent
    from .safety import SafetyViolationError
    from .schemas import MarineConditionsAgentInput
except ImportError:
    from agent import MarineConditionsIntelligenceAgent
    from safety import SafetyViolationError
    from schemas import MarineConditionsAgentInput

logging.basicConfig(level=logging.INFO, format="%(asctime)s [%(levelname)s] %(name)s: %(message)s")
logger = logging.getLogger("blueverse.ai.marine_conditions.service")

AGENT_INSTANCE = MarineConditionsIntelligenceAgent()


class AgentHttpHandler(http.server.BaseHTTPRequestHandler):
    """HTTP request handler for Marine Conditions Intelligence Agent."""

    HEALTH_ROUTES = (
        "/api/agents/marine-conditions/health",
        "/agents/marine-conditions/health",
        "/health",
        "/api/health",
        "/",
    )

    INVOCATION_ROUTES = (
        "/api/agents/marine-conditions/invocations",
        "/agents/marine-conditions/invocations",
        "/api/agents/marine-conditions/report",
        "/agents/marine-conditions/report",
        "/invocations",
        "/report",
        "/api/invocations",
        "/api/report",
    )

    def do_GET(self):
        path = self.path.split("?")[0]
        if path in self.HEALTH_ROUTES:
            self._send_json(
                200,
                {
                    "status": "Healthy",
                    "service": "blueverse-agent-marine-conditions",
                    "role": "Marine Conditions Intelligence Agent",
                    "owner": "Sanuda Abeysinghe (@sanudaabey)",
                    "contract_id": "v1.agent.marine-conditions",
                    "endpoints": {
                        "health": "/api/agents/marine-conditions/health",
                        "invocations": "/api/agents/marine-conditions/invocations",
                        "report": "/api/agents/marine-conditions/report",
                    },
                },
            )
        else:
            self._send_problem(404, "Not Found", f"Route '{self.path}' not recognized.")

    def do_POST(self):
        path = self.path.split("?")[0]
        if path in self.INVOCATION_ROUTES:
            content_length = int(self.headers.get("Content-Length", 0))
            if content_length <= 0:
                self._send_problem(400, "Bad Request", "Missing request body.")
                return

            try:
                body_bytes = self.rfile.read(content_length)
                payload = json.loads(body_bytes.decode("utf-8"))
            except Exception as exc:
                self._send_problem(400, "Bad Request", f"Malformed JSON payload: {exc}")
                return

            try:
                input_data = MarineConditionsAgentInput.from_dict(payload)
            except ValueError as ve:
                self._send_problem(400, "Bad Request", f"Validation error: {ve}")
                return

            try:
                report = AGENT_INSTANCE.execute(input_data)
                self._send_json(200, report.to_dict())
            except SafetyViolationError as sve:
                self._send_problem(422, "Unprocessable Entity", f"Safety policy violation: {sve}")
            except Exception as exc:
                logger.exception("Unexpected error processing agent request")
                self._send_problem(500, "Internal Server Error", f"Agent execution failed: {exc}")
        else:
            self._send_problem(404, "Not Found", f"Route '{self.path}' not recognized.")

    def _send_json(self, status_code: int, data: Dict[str, Any]):
        body = json.dumps(data, indent=2).encode("utf-8")
        self.send_response(status_code)
        self.send_header("Content-Type", "application/json; charset=utf-8")
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)

    def _send_problem(self, status_code: int, title: str, detail: str):
        problem = {
            "type": "https://tools.ietf.org/html/rfc7807",
            "title": title,
            "status": status_code,
            "detail": detail,
        }
        body = json.dumps(problem, indent=2).encode("utf-8")
        self.send_response(status_code)
        self.send_header("Content-Type", "application/problem+json; charset=utf-8")
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)

    def log_message(self, format, *args):
        # Delegate to standard logging
        logger.info("%s - - [%s] %s", self.address_string(), self.log_date_time_string(), format % args)


def run_server(port: int = 8080):
    server_address = ("0.0.0.0", port)
    httpd = http.server.HTTPServer(server_address, AgentHttpHandler)
    logger.info("Starting Marine Conditions Intelligence Agent service on port %d...", port)
    try:
        httpd.serve_forever()
    except KeyboardInterrupt:
        logger.info("Service shutting down...")
    finally:
        httpd.server_close()


if __name__ == "__main__":
    port = int(os.getenv("PORT", "8080"))
    run_server(port)

"""HTTP Server entry point for Coastal Experience & Biodiversity Agent.

Runs inside the Docker container on port 8080 (or PORT env var) on blueverse_internal network.
All API endpoints are namespaced specifically under /api/experiences/agent/... to prevent
route collisions when all four BLUEVERSE member agents merge:
- GET /health and GET /api/experiences/agent/health: Health probe for container liveness
- GET /api/experiences/agent/info: Agent identity, Gemini model config, and tool allowlist
- POST /api/experiences/agent/context-reports (and /api/experiences/agent/generate):
  Execute context generation for a planning/operations step
"""

from __future__ import annotations

import json
import hmac
import os
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from typing import Any, Dict, Optional

from .agent import CoastalExperienceAgent
from .gemini_client import GeminiClient, GeminiConfig
from .schemas import ALLOWLISTED_TOOLS, ExperienceAgentInput
from .tools import ExperienceAgentTools


MAX_REQUEST_BODY_BYTES = 65536
AGENT_ENDPOINT_PREFIX = "/api/experiences/agent"


def build_default_agent() -> CoastalExperienceAgent:
    experience_url = os.getenv("EXPERIENCE_SERVICE_URL", "http://experience-biodiversity:8080")
    planner_url = (
        os.getenv("PLANNER_SERVICE_URL")
        or os.getenv("PUBLIC_API_URL")
        or "http://coastal-planner:8080"
    )
    tools = ExperienceAgentTools(
        experience_service_url=experience_url,
        planner_service_url=planner_url,
    )
    gemini_client = GeminiClient(GeminiConfig.from_env())
    return CoastalExperienceAgent(tools=tools, gemini_client=gemini_client)


agent_instance = build_default_agent()


class AgentHttpHandler(BaseHTTPRequestHandler):
    agent: CoastalExperienceAgent = agent_instance

    def log_message(self, format: str, *args: Any) -> None:  # noqa: A002
        # Suppress noisy access logs during automated test runs unless AGENT_VERBOSE_LOGS=1
        if os.getenv("AGENT_VERBOSE_LOGS") == "1":
            super().log_message(format, *args)

    def _send_json(self, status: int, data: Dict[str, Any]) -> None:
        response_bytes = json.dumps(data, default=str).encode("utf-8")
        self.send_response(status)
        self.send_header("Content-Type", "application/json")
        self.send_header("Content-Length", str(len(response_bytes)))
        self.end_headers()
        self.wfile.write(response_bytes)

    def do_GET(self) -> None:
        path = self.path.split("?", 1)[0]
        active_agent = getattr(self, "agent", None) or agent_instance
        gemini_cfg = active_agent.gemini_client.config

        if path in ("/health", "/api/experiences/agent/health"):
            self._send_json(
                200,
                {
                    "status": "healthy",
                    "agent_id": "v1.agent.experience-biodiversity",
                    "agent": "Coastal Experience & Biodiversity Agent",
                    "owner": "Ushan Srinuka",
                    "role": "specialist_context_provider",
                    "endpoint_prefix": AGENT_ENDPOINT_PREFIX,
                    "model_provider": "google-ai-studio",
                    "model_name": gemini_cfg.model,
                    "gemini_api_key_configured": gemini_cfg.is_configured,
                },
            )
        elif path == "/api/experiences/agent/info":
            self._send_json(
                200,
                {
                    "agent_id": "v1.agent.experience-biodiversity",
                    "name": "Coastal Experience & Biodiversity Agent",
                    "owner": "Ushan Srinuka",
                    "github_username": "Ushan-Srinuka",
                    "endpoint_prefix": AGENT_ENDPOINT_PREFIX,
                    "endpoints": [
                        "/api/experiences/agent/health",
                        "/api/experiences/agent/info",
                        "/api/experiences/agent/context-reports",
                        "/api/experiences/agent/generate",
                    ],
                    "model_provider": "google-ai-studio",
                    "model_name": gemini_cfg.model,
                    "gemini_api_key_configured": gemini_cfg.is_configured,
                    "tools": list(ALLOWLISTED_TOOLS),
                    "read_only": True,
                },
            )
        else:
            self._send_json(404, {"error": "Not Found", "path": path})

    def do_POST(self) -> None:
        path = self.path.split("?", 1)[0]
        if path in ("/api/experiences/agent/context-reports", "/api/experiences/agent/generate"):
            auth_scheme, separator, auth_value = self.headers.get("Authorization", "").partition(" ")
            token = auth_value.strip() if separator and auth_scheme.lower() == "bearer" else ""
            raw_authorization = self.headers.get("Authorization", "").strip()
            supplied_service_key = self.headers.get("X-Blueverse-Service-Key", "")
            configured_service_key = os.getenv("AGENTIC_SERVICE_KEY", "")
            if raw_authorization and supplied_service_key:
                self._send_json(401, {"error": "Unauthorized", "detail": "Use one authentication method"})
                return
            service_mode = bool(
                supplied_service_key
                and configured_service_key
                and len(supplied_service_key) <= 512
                and hmac.compare_digest(
                    supplied_service_key.encode("utf-8"), configured_service_key.encode("utf-8")
                )
            )
            if not token and not service_mode:
                self._send_json(401, {"error": "Unauthorized", "detail": "A Bearer token is required"})
                return

            try:
                content_length = int(self.headers.get("Content-Length", "0"))
            except ValueError:
                self._send_json(400, {"error": "Bad Request", "detail": "Invalid Content-Length header"})
                return

            if content_length <= 0:
                self._send_json(400, {"error": "Bad Request", "detail": "Request body cannot be empty"})
                return

            if content_length > MAX_REQUEST_BODY_BYTES:
                self._send_json(
                    413,
                    {
                        "error": "Payload Too Large",
                        "detail": f"Request body exceeds maximum of {MAX_REQUEST_BODY_BYTES} bytes",
                    },
                )
                return

            try:
                body = self.rfile.read(content_length).decode("utf-8")
                raw_json = json.loads(body)
                agent_input = ExperienceAgentInput.from_dict(raw_json)
                active_agent = getattr(self, "agent", None) or agent_instance
                report = active_agent.generate_report(
                    agent_input,
                    auth_token=token or None,
                    service_mode=service_mode,
                )
                self._send_json(200, report.to_dict())
            except json.JSONDecodeError:
                self._send_json(
                    400,
                    {
                        "error": "Bad Request",
                        "detail": "Malformed JSON request payload",
                    },
                )
            except ValueError as exc:
                self._send_json(
                    400,
                    {
                        "error": "Bad Request",
                        "detail": str(exc),
                    },
                )
            except Exception:
                self._send_json(
                    500,
                    {
                        "error": "Internal Server Error",
                        "detail": "An unexpected error occurred while generating the context report.",
                    },
                )
        else:
            self._send_json(404, {"error": "Not Found", "path": path})


def create_http_server(
    host: str = "0.0.0.0",
    port: int = 8080,
    agent: Optional[CoastalExperienceAgent] = None,
) -> ThreadingHTTPServer:
    httpd = ThreadingHTTPServer((host, port), AgentHttpHandler)
    httpd.daemon_threads = True
    if agent is not None:
        AgentHttpHandler.agent = agent
    return httpd


def run_server(port: int = 8080) -> None:
    httpd = create_http_server("0.0.0.0", port)
    print(f"Coastal Experience & Biodiversity Agent running on port {port}...")
    httpd.serve_forever()


if __name__ == "__main__":
    port_str = os.getenv("PORT", "8080")
    run_server(int(port_str))

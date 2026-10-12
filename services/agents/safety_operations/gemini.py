"""Google Gemini Interactions API adapter for the Safety & Operations role."""

from __future__ import annotations

from dataclasses import dataclass, field
import json
import math
import os
import re
import socket
from typing import Any, Mapping, Protocol
from urllib.error import HTTPError, URLError
from urllib.request import HTTPRedirectHandler, Request, build_opener

from .agent import ModelCall, ModelUnavailable
from .contracts import (
    ContractError,
    OUTPUT_SCHEMA_VERSION,
    RECOMMENDATIONS,
    REVIEW_SUGGESTIONS,
    minimize_model_context,
)


INTERACTIONS_URL = "https://generativelanguage.googleapis.com/v1/interactions"
MAX_PROVIDER_RESPONSE_BYTES = 64 * 1024
MODEL_ID = re.compile(r"^[A-Za-z0-9][A-Za-z0-9._-]{0,127}$")
RETRYABLE_HTTP_STATUSES = frozenset({408, 425, 429})


class GeminiConfigurationError(ValueError):
    """Required Gemini configuration is missing or invalid."""


class GeminiRequestRejected(ValueError):
    """Gemini rejected the request for a non-transient reason."""


class GeminiResponseError(ValueError):
    """Gemini returned a response outside the expected bounded contract."""


@dataclass(frozen=True)
class GeminiConfig:
    api_key: str = field(repr=False)
    model: str
    max_output_tokens: int = 2048

    @classmethod
    def from_environment(cls, environment: Mapping[str, str] | None = None) -> GeminiConfig:
        values = os.environ if environment is None else environment
        api_key = values.get("GEMINI_API_KEY", "").strip()
        model = values.get("GEMINI_MODEL", "").strip()
        approval = values.get("GEMINI_DATA_HANDLING_APPROVED", "").strip().casefold()

        if not api_key:
            raise GeminiConfigurationError("GEMINI_API_KEY is required")
        if not model or MODEL_ID.fullmatch(model) is None:
            raise GeminiConfigurationError("GEMINI_MODEL must be a valid model identifier")
        if approval != "true":
            raise GeminiConfigurationError(
                "Gemini request data handling must be reviewed before enabling provider calls"
            )

        token_limit_raw = values.get("GEMINI_MAX_OUTPUT_TOKENS", "2048").strip()
        try:
            max_output_tokens = int(token_limit_raw)
        except ValueError as exc:
            raise GeminiConfigurationError("GEMINI_MAX_OUTPUT_TOKENS must be an integer") from exc
        if not 128 <= max_output_tokens <= 8192:
            raise GeminiConfigurationError("GEMINI_MAX_OUTPUT_TOKENS must be between 128 and 8192")

        return cls(api_key=api_key, model=model, max_output_tokens=max_output_tokens)


class HttpTransport(Protocol):
    def post(self, url: str, headers: Mapping[str, str], body: bytes, timeout: float) -> tuple[int, bytes]:
        """Send a bounded HTTPS POST and return its status and body."""


class _RejectRedirectHandler(HTTPRedirectHandler):
    """Reject redirects so the provider credential stays on the pinned origin."""

    def redirect_request(self, req, fp, code, msg, headers, newurl):
        return None


class UrllibTransport:
    """Small standard-library transport; it does not log request or response data."""

    def post(self, url: str, headers: Mapping[str, str], body: bytes, timeout: float) -> tuple[int, bytes]:
        request = Request(url, data=body, headers=dict(headers), method="POST")
        opener = build_opener(_RejectRedirectHandler())
        try:
            with opener.open(request, timeout=timeout) as response:
                return response.status, response.read(MAX_PROVIDER_RESPONSE_BYTES + 1)
        except HTTPError as response:
            try:
                return response.code, response.read(MAX_PROVIDER_RESPONSE_BYTES + 1)
            finally:
                response.close()
        except (URLError, socket.timeout, TimeoutError, OSError) as exc:
            if isinstance(exc, (socket.timeout, TimeoutError)):
                raise TimeoutError from None
            raise ModelUnavailable from None


class GeminiModelPort:
    """Calls Gemini once and returns untrusted structured JSON for local validation."""

    def __init__(self, config: GeminiConfig, transport: HttpTransport | None = None) -> None:
        self._config = config
        self._transport = transport or UrllibTransport()

    def generate(self, request: ModelCall) -> Mapping[str, Any]:
        if request.allowed_tools:
            raise GeminiRequestRejected("Safety & Operations provider calls cannot grant tools")
        if not math.isfinite(request.timeout_seconds) or request.timeout_seconds <= 0:
            raise GeminiRequestRejected("Provider timeout must be positive and finite")

        try:
            model_context = minimize_model_context(request.context)
            serialized_context = json.dumps(
                model_context,
                ensure_ascii=False,
                allow_nan=False,
                separators=(",", ":"),
            )
        except (ContractError, TypeError, ValueError):
            raise GeminiRequestRejected("Gemini request context is malformed") from None

        payload = {
            "model": self._config.model,
            "system_instruction": request.system_instructions,
            "input": serialized_context,
            "response_format": {
                "type": "text",
                "mime_type": "application/json",
                "schema": _recommendation_schema(model_context),
            },
            # Avoid retaining BLUEVERSE context as a retrievable interaction.
            "store": False,
            "generation_config": {"max_output_tokens": self._config.max_output_tokens},
        }
        body = json.dumps(payload, ensure_ascii=False, allow_nan=False, separators=(",", ":")).encode("utf-8")
        headers = {
            "Content-Type": "application/json",
            "x-goog-api-key": self._config.api_key,
        }

        try:
            status, response_body = self._transport.post(
                INTERACTIONS_URL,
                headers,
                body,
                request.timeout_seconds,
            )
        except TimeoutError:
            raise TimeoutError from None
        except ModelUnavailable:
            raise ModelUnavailable from None
        except Exception:
            raise ModelUnavailable from None

        if status in RETRYABLE_HTTP_STATUSES or status >= 500:
            raise ModelUnavailable
        if not 200 <= status < 300:
            raise GeminiRequestRejected("Gemini rejected the request")
        if len(response_body) > MAX_PROVIDER_RESPONSE_BYTES:
            raise GeminiResponseError("Gemini response exceeded the size limit")

        try:
            response = json.loads(response_body.decode("utf-8"))
        except (UnicodeDecodeError, json.JSONDecodeError):
            raise GeminiResponseError("Gemini response was not valid JSON") from None
        if not isinstance(response, Mapping) or response.get("status") != "completed":
            raise GeminiResponseError("Gemini interaction did not complete")

        steps = response.get("steps")
        if not isinstance(steps, list) or len(steps) != 1 or not isinstance(steps[0], Mapping):
            raise GeminiResponseError("Gemini response did not contain one model output")
        step = steps[0]
        if step.get("type") != "model_output":
            raise GeminiResponseError("Gemini returned a non-output step")
        content = step.get("content")
        if not isinstance(content, list) or not content:
            raise GeminiResponseError("Gemini output content was empty")
        if any(not isinstance(part, Mapping) or part.get("type") != "text" for part in content):
            raise GeminiResponseError("Gemini output contained unsupported content")
        if any(not isinstance(part.get("text"), str) for part in content):
            raise GeminiResponseError("Gemini output did not contain text")
        text = "".join(part["text"] for part in content)
        if not text:
            raise GeminiResponseError("Gemini output did not contain text")

        try:
            result = json.loads(text)
        except json.JSONDecodeError:
            raise GeminiResponseError("Gemini output was not valid JSON") from None
        if not isinstance(result, Mapping):
            raise GeminiResponseError("Gemini output must be a JSON object")
        return result


def _recommendation_schema(context: Mapping[str, Any]) -> dict[str, Any]:
    allowed_actions = context.get("allowedActions", [])
    if not isinstance(allowed_actions, list) or any(not isinstance(item, str) for item in allowed_actions):
        raise GeminiRequestRejected("The validated action allowlist is malformed")

    action_schema: dict[str, Any]
    if allowed_actions:
        action_schema = {
            "anyOf": [
                {"type": "string", "enum": allowed_actions},
                {"type": "null"},
            ]
        }
    else:
        action_schema = {"type": "null"}

    properties = {
        "schemaVersion": {"type": "string", "enum": [OUTPUT_SCHEMA_VERSION]},
        "workflowId": {"type": "string"},
        "stepId": {"type": "string"},
        "assessmentId": {"type": "string"},
        "targetId": {"type": "string"},
        "recommendation": {"type": "string", "enum": sorted(RECOMMENDATIONS)},
        "summary": {"type": "string"},
        "proposedAction": action_schema,
        "evidenceReferences": {"type": "array", "items": {"type": "string"}, "maxItems": 12},
        "uncertainties": {"type": "array", "items": {"type": "string"}, "maxItems": 10},
        "reviewSuggestion": {"type": "string", "enum": sorted(REVIEW_SUGGESTIONS)},
    }
    return {
        "type": "object",
        "properties": properties,
        "required": list(properties),
        "additionalProperties": False,
    }

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
from urllib.parse import quote, urlsplit
from urllib.request import HTTPRedirectHandler, ProxyHandler, Request, build_opener

from .agent import ModelCall, ModelUnavailable
from .contracts import (
    ContractError,
    OUTPUT_SCHEMA_VERSION,
    RECOMMENDATIONS,
    REVIEW_SUGGESTIONS,
    minimize_model_context,
)


GENERATE_CONTENT_BASE_URL = "https://generativelanguage.googleapis.com/v1beta"
GEMINI_API_HOST = "generativelanguage.googleapis.com"
SAFETY_OPERATIONS_GEMINI_EGRESS_PROXY_URL = "http://gemini-egress-proxy:3128"
MAX_PROVIDER_RESPONSE_BYTES = 64 * 1024
MODEL_ID = re.compile(r"^[A-Za-z0-9][A-Za-z0-9._-]{0,127}$")
GENERATE_CONTENT_PATH = re.compile(r"^/v1beta/models/[A-Za-z0-9][A-Za-z0-9._-]{0,127}:generateContent$")
RETRYABLE_HTTP_STATUSES = frozenset({408, 425, 429})


class GeminiConfigurationError(ValueError):
    """Required Gemini configuration is missing or invalid."""


class GeminiRequestRejected(ValueError):
    """Gemini rejected the request for a non-transient reason."""


class GeminiResponseError(ValueError):
    """Gemini returned a response outside the expected bounded contract."""


@dataclass(frozen=True)
class GeminiConfig:
    api_key: str = field(default="", repr=False)
    model: str = ""
    max_output_tokens: int = 2048
    egress_proxy_url: str = SAFETY_OPERATIONS_GEMINI_EGRESS_PROXY_URL
    enabled: bool = False

    def __post_init__(self) -> None:
        if not self.enabled:
            return
        if not isinstance(self.api_key, str) or not self.api_key.strip():
            raise GeminiConfigurationError("SAFETY_OPERATIONS_GEMINI_API_KEY is required")
        if not isinstance(self.model, str) or MODEL_ID.fullmatch(self.model) is None:
            raise GeminiConfigurationError("SAFETY_OPERATIONS_GEMINI_MODEL must be a valid model identifier")
        if self.egress_proxy_url != SAFETY_OPERATIONS_GEMINI_EGRESS_PROXY_URL:
            raise GeminiConfigurationError("Gemini requests must use the internal allowlisted egress proxy")
        if type(self.max_output_tokens) is not int or not 128 <= self.max_output_tokens <= 8192:
            raise GeminiConfigurationError("SAFETY_OPERATIONS_GEMINI_MAX_OUTPUT_TOKENS must be between 128 and 8192")

    @classmethod
    def from_environment(cls, environment: Mapping[str, str] | None = None) -> GeminiConfig:
        values = os.environ if environment is None else environment
        enabled_raw = values.get("SAFETY_OPERATIONS_GEMINI_ENABLED", "false").strip().casefold()
        if enabled_raw not in {"true", "false"}:
            raise GeminiConfigurationError("SAFETY_OPERATIONS_GEMINI_ENABLED must be true or false")
        if enabled_raw != "true":
            return cls()

        api_key = values.get("SAFETY_OPERATIONS_GEMINI_API_KEY", "").strip()
        model = values.get("SAFETY_OPERATIONS_GEMINI_MODEL", "").strip()
        approval = values.get("SAFETY_OPERATIONS_GEMINI_DATA_HANDLING_APPROVED", "").strip().casefold()
        egress_proxy_url = values.get(
            "SAFETY_OPERATIONS_GEMINI_EGRESS_PROXY_URL",
            SAFETY_OPERATIONS_GEMINI_EGRESS_PROXY_URL,
        ).strip()

        if not api_key:
            raise GeminiConfigurationError("SAFETY_OPERATIONS_GEMINI_API_KEY is required")
        if not model or MODEL_ID.fullmatch(model) is None:
            raise GeminiConfigurationError("SAFETY_OPERATIONS_GEMINI_MODEL must be a valid model identifier")
        if approval != "true":
            raise GeminiConfigurationError(
                "Gemini request data handling must be reviewed before enabling provider calls"
            )
        if egress_proxy_url != SAFETY_OPERATIONS_GEMINI_EGRESS_PROXY_URL:
            raise GeminiConfigurationError("Gemini requests must use the internal allowlisted egress proxy")

        token_limit_raw = values.get("SAFETY_OPERATIONS_GEMINI_MAX_OUTPUT_TOKENS", "2048").strip()
        try:
            max_output_tokens = int(token_limit_raw)
        except ValueError as exc:
            raise GeminiConfigurationError("SAFETY_OPERATIONS_GEMINI_MAX_OUTPUT_TOKENS must be an integer") from exc
        if not 128 <= max_output_tokens <= 8192:
            raise GeminiConfigurationError("SAFETY_OPERATIONS_GEMINI_MAX_OUTPUT_TOKENS must be between 128 and 8192")

        return cls(
            api_key=api_key,
            model=model,
            max_output_tokens=max_output_tokens,
            egress_proxy_url=egress_proxy_url,
            enabled=True,
        )


class HttpTransport(Protocol):
    def post(self, url: str, headers: Mapping[str, str], body: bytes, timeout: float) -> tuple[int, bytes]:
        """Send a bounded HTTPS POST and return its status and body."""


class _RejectRedirectHandler(HTTPRedirectHandler):
    """Reject redirects so the provider credential stays on the pinned origin."""

    def redirect_request(self, req, fp, code, msg, headers, newurl):
        return None


class _RequiredGeminiProxyHandler(ProxyHandler):
    """Force the pinned Gemini origin through the one internal proxy, ignoring NO_PROXY."""

    def __init__(self, proxy_url: str) -> None:
        if proxy_url != SAFETY_OPERATIONS_GEMINI_EGRESS_PROXY_URL:
            raise GeminiConfigurationError("Gemini requests must use the internal allowlisted egress proxy")
        super().__init__({"https": proxy_url})

    def proxy_open(self, req, proxy, proxy_type):
        if (
            proxy_type != "https"
            or req.type != "https"
            or (req.host or "").casefold() != GEMINI_API_HOST
        ):
            raise URLError("Gemini transport only permits the pinned HTTPS provider origin")

        proxy_parts = urlsplit(proxy)
        if (
            proxy_parts.scheme != "http"
            or proxy_parts.hostname != "gemini-egress-proxy"
            or proxy_parts.port != 3128
            or proxy_parts.username is not None
            or proxy_parts.password is not None
            or proxy_parts.path
            or proxy_parts.query
            or proxy_parts.fragment
        ):
            raise URLError("Gemini transport proxy configuration is invalid")

        # Do not call ProxyHandler.proxy_open: its environment-sensitive
        # proxy_bypass() can silently route around the approved proxy.
        req.set_proxy("gemini-egress-proxy:3128", "http")
        return None


class UrllibTransport:
    """Bounded standard-library HTTPS transport that cannot fall back to direct egress."""

    def __init__(self, proxy_url: str = SAFETY_OPERATIONS_GEMINI_EGRESS_PROXY_URL) -> None:
        if proxy_url != SAFETY_OPERATIONS_GEMINI_EGRESS_PROXY_URL:
            raise GeminiConfigurationError("Gemini requests must use the internal allowlisted egress proxy")
        self._proxy_url = proxy_url

    def post(self, url: str, headers: Mapping[str, str], body: bytes, timeout: float) -> tuple[int, bytes]:
        parts = urlsplit(url)
        if (
            parts.scheme != "https"
            or parts.hostname != GEMINI_API_HOST
            or parts.port not in (None, 443)
            or GENERATE_CONTENT_PATH.fullmatch(parts.path) is None
            or parts.query
            or parts.fragment
            or parts.username is not None
            or parts.password is not None
        ):
            raise URLError("Gemini transport only permits the pinned HTTPS provider origin")

        request = Request(url, data=body, headers=dict(headers), method="POST")
        opener = build_opener(_RequiredGeminiProxyHandler(self._proxy_url), _RejectRedirectHandler())
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
        self._transport = transport or UrllibTransport(config.egress_proxy_url)

    @property
    def configured(self) -> bool:
        return self._config.enabled

    def generate(self, request: ModelCall) -> Mapping[str, Any]:
        if not self._config.enabled:
            raise ModelUnavailable
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

        model_path = quote(self._config.model, safe=".-_")
        endpoint = f"{GENERATE_CONTENT_BASE_URL}/models/{model_path}:generateContent"
        payload = {
            "systemInstruction": {"parts": [{"text": request.system_instructions}]},
            "contents": [{"role": "user", "parts": [{"text": serialized_context}]}],
            "generationConfig": {
                "candidateCount": 1,
                "maxOutputTokens": self._config.max_output_tokens,
                "responseFormat": {
                    "text": {
                        "mimeType": "application/json",
                        "schema": _recommendation_schema(model_context),
                    }
                },
            },
        }
        body = json.dumps(payload, ensure_ascii=False, allow_nan=False, separators=(",", ":")).encode("utf-8")
        headers = {
            "Content-Type": "application/json",
            "x-goog-api-key": self._config.api_key,
        }

        try:
            status, response_body = self._transport.post(
                endpoint,
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
        if not isinstance(response, Mapping):
            raise GeminiResponseError("Gemini response was not an object")
        candidates = response.get("candidates")
        if not isinstance(candidates, list) or len(candidates) != 1 or not isinstance(candidates[0], Mapping):
            raise GeminiResponseError("Gemini response did not contain one candidate")
        candidate = candidates[0]
        if candidate.get("finishReason") != "STOP":
            raise GeminiResponseError("Gemini candidate did not finish normally")
        content = candidate.get("content")
        if not isinstance(content, Mapping):
            raise GeminiResponseError("Gemini candidate did not contain content")
        parts = content.get("parts")
        if not isinstance(parts, list) or not parts:
            raise GeminiResponseError("Gemini candidate content was empty")
        visible_text: list[str] = []
        for part in parts:
            if not isinstance(part, Mapping):
                raise GeminiResponseError("Gemini candidate contained unsupported content")
            if part.get("thought") is True:
                continue
            if "functionCall" in part or "inlineData" in part:
                raise GeminiResponseError("Gemini candidate contained unsupported content")
            text_part = part.get("text")
            if not isinstance(text_part, str):
                raise GeminiResponseError("Gemini candidate did not contain text")
            if text_part.strip():
                visible_text.append(text_part.strip())
        if not visible_text:
            raise GeminiResponseError("Gemini candidate did not contain visible text")
        text = "\n".join(visible_text)

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

"""Google AI Studio Gemini API client for Marine Conditions Intelligence Agent.

Role: Synthesizes plain-language objective-relevance and uncertainty summaries
using structured JSON output from Gemini models (e.g. gemini-2.5-flash).
Features:
- Free-tier rate limit tolerance with deterministic fallback
- Zero external runtime dependencies (built on standard library urllib)
- Strict secret redaction and credential protection
"""

from __future__ import annotations

import json
import logging
import os
import urllib.error
import urllib.parse
import urllib.request
from typing import Any, Callable, Dict, List, Optional, Tuple

logger = logging.getLogger("blueverse.ai.marine_conditions.gemini")

DEFAULT_GEMINI_MODEL = "gemini-3.5-flash"
DEFAULT_GEMINI_BASE_URL = "https://generativelanguage.googleapis.com/v1beta/models"


def _load_env_if_present() -> None:
    """Reads environment variables from .env files if present and not yet in os.environ."""
    candidates = [
        os.path.join(os.getcwd(), ".env"),
        os.path.join(os.path.dirname(__file__), ".env"),
        os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", "..", ".env")),
    ]
    for candidate in candidates:
        if os.path.isfile(candidate):
            try:
                with open(candidate, "r", encoding="utf-8") as f:
                    for line in f:
                        line = line.strip()
                        if line and not line.startswith("#") and "=" in line:
                            k, v = line.split("=", 1)
                            k = k.strip()
                            v = v.strip().strip("'\"")
                            if k not in os.environ:
                                os.environ[k] = v
            except Exception:
                pass


_UNSET: Any = object()


class GeminiClient:
    """Client for Google AI Studio Gemini API with structured outputs and fallback support."""

    def __init__(
        self,
        api_key: Optional[str] = _UNSET,
        model: Optional[str] = None,
        base_url: Optional[str] = None,
        timeout_seconds: float = 10.0,
        transport: Optional[Callable[[str, Dict[str, Any], float], Tuple[int, Dict[str, Any]]]] = None,
    ):
        _load_env_if_present()
        if api_key is _UNSET:
            self.api_key = os.getenv("GEMINI_API_KEY")
        else:
            self.api_key = api_key
        self.model = model or os.getenv("GEMINI_MODEL", DEFAULT_GEMINI_MODEL)
        self.base_url = (base_url or os.getenv("GEMINI_BASE_URL", DEFAULT_GEMINI_BASE_URL)).rstrip("/")
        self.timeout_seconds = timeout_seconds
        self.transport = transport  # Optional custom transport for test injection

    @property
    def is_configured(self) -> bool:
        """Returns True if an API key is available."""
        return bool(self.api_key and self.api_key.strip())

    def generate_conditions_summary(
        self,
        sanitized_objective: str,
        activity: str,
        factors: Dict[str, Any],
        missing: List[str],
    ) -> Tuple[Optional[str], Optional[str], Optional[str]]:
        """Generates objective-relevance and uncertainty summaries using Gemini.

        Returns:
            Tuple of (objective_relevance_summary, uncertainty_and_limitations, error_reason)
            If error_reason is not None, the caller falls back to deterministic synthesis.
        """
        if not self.is_configured:
            return None, None, "API_KEY_NOT_CONFIGURED"

        # Prepare evidence summary for model prompt
        factor_summary = {}
        for name, factor in factors.items():
            val = getattr(factor, "value", None)
            unit = getattr(factor, "unit", "")
            desc = getattr(factor, "description", None)
            factor_summary[name] = {"value": val, "unit": unit, "description": desc}

        payload: Dict[str, Any] = {
            "systemInstruction": {
                "parts": [
                    {
                        "text": (
                            "You are the Marine Conditions Intelligence Agent for BLUEVERSE (Contract: v1.agent.marine-conditions).\n"
                            "Your role is to summarize environmental context for coastal activities.\n"
                            "STRICT BOUNDARIES:\n"
                            "1. Rely only on the supplied factors; never invent or extrapolate physical measurements.\n"
                            "2. Do not invent physical safety limits or thresholds.\n"
                            "3. Do not grant operational approvals, suspensions, or cancellations.\n"
                            "4. Output valid JSON adhering strictly to the response schema."
                        )
                    }
                ]
            },
            "contents": [
                {
                    "role": "user",
                    "parts": [
                        {
                            "text": (
                                f"Objective: {sanitized_objective}\n"
                                f"Activity: {activity}\n"
                                f"Environmental Factors: {json.dumps(factor_summary, indent=2)}\n"
                                f"Missing/Unavailable Factors: {json.dumps(missing)}\n\n"
                                "Provide a concise objective-relevance summary and uncertainty/limitations statement."
                            )
                        }
                    ],
                }
            ],
            "generationConfig": {
                "temperature": 0.2,
                "responseMimeType": "application/json",
                "responseSchema": {
                    "type": "OBJECT",
                    "properties": {
                        "objective_relevance_summary": {"type": "STRING"},
                        "uncertainty_and_limitations": {"type": "STRING"},
                    },
                    "required": ["objective_relevance_summary", "uncertainty_and_limitations"],
                },
            },
        }

        url = f"{self.base_url}/{self.model}:generateContent?key={self.api_key}"

        try:
            status_code, response_data = self._send_request(url, payload, self.timeout_seconds)
        except Exception as exc:
            logger.warning("Gemini API call failed with exception: %s", exc)
            return None, None, f"REQUEST_FAILED: {exc}"

        if status_code == 429:
            logger.warning("Gemini API free tier rate limit exceeded (HTTP 429). Falling back to deterministic synthesis.")
            return None, None, "RATE_LIMIT_EXCEEDED"

        if status_code != 200 or not isinstance(response_data, dict):
            logger.warning("Gemini API returned status %d. Falling back to deterministic synthesis.", status_code)
            return None, None, f"HTTP_ERROR_{status_code}"

        # Parse Gemini response format
        candidates = response_data.get("candidates", [])
        if not candidates:
            return None, None, "EMPTY_CANDIDATES"

        content_parts = candidates[0].get("content", {}).get("parts", [])
        if not content_parts or "text" not in content_parts[0]:
            return None, None, "MISSING_TEXT_PART"

        raw_text = content_parts[0]["text"]
        try:
            parsed = json.loads(raw_text)
            relevance = str(parsed.get("objective_relevance_summary", "")).strip()
            limitations = str(parsed.get("uncertainty_and_limitations", "")).strip()
            if not relevance:
                return None, None, "EMPTY_RELEVANCE_SUMMARY"
            return relevance, limitations, None
        except Exception as json_err:
            logger.warning("Failed to parse Gemini JSON output: %s (raw text: %s)", json_err, raw_text)
            return None, None, "MALFORMED_JSON_OUTPUT"

    def _send_request(self, url: str, payload: Dict[str, Any], timeout: float) -> Tuple[int, Dict[str, Any]]:
        """Sends HTTP request to Gemini endpoint or uses test transport."""
        if self.transport:
            return self.transport(url, payload, timeout)

        data = json.dumps(payload).encode("utf-8")
        req = urllib.request.Request(
            url,
            data=data,
            headers={
                "Content-Type": "application/json",
                "User-Agent": "BLUEVERSE-MarineSafetyAgent/1.0",
            },
            method="POST",
        )

        try:
            with urllib.request.urlopen(req, timeout=timeout) as response:
                body = response.read().decode("utf-8")
                return response.status, json.loads(body)
        except urllib.error.HTTPError as he:
            try:
                err_body = json.loads(he.read().decode("utf-8"))
            except Exception:
                err_body = {}
            return he.code, err_body
        except (urllib.error.URLError, TimeoutError) as net_err:
            logger.warning("Network error calling Gemini API: %s", net_err)
            return 503, {"error": str(net_err)}

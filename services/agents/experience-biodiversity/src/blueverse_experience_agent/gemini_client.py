"""Google AI Studio Gemini Model Client for Coastal Experience & Biodiversity Agent.

Uses only the Python 3.12/3.13 standard library (urllib.request, ssl, json) so that the
container runs natively on the hardened dhi.io/python:3.13-alpine runtime without
requiring pip, shell scripts, or native C/Rust extensions.

Security & Governance Invariants:
1. API key is transmitted exclusively via the 'x-goog-api-key' header (never in URLs or logs).
2. Hidden model reasoning ('thought': true parts from Gemini 2.5 models) is stripped and never stored.
3. Gemini output is treated as untrusted input: validated against strict JSON structure and
   allowlisted tool boundaries, and never allowed to overwrite deterministic domain statuses
   or fabricate species predictions.
4. Free-tier rate limits (HTTP 429) and network errors degrade gracefully without losing
   deterministic tool evidence.
"""

from __future__ import annotations

import json
import os
import ssl
import time
import urllib.error
import urllib.request
from dataclasses import dataclass
from typing import Any, Callable, Dict, List, Optional, Tuple

from .schemas import (
    ALLOWLISTED_TOOLS,
    FORBIDDEN_SECRET_KEYS,
    ActivityContext,
    BiodiversityContext,
    DestinationContext,
    ExperienceAgentInput,
    ModelExecutionStatus,
    ModelExecutionSummary,
    OfferingContext,
    ScheduleContext,
)


DEFAULT_GEMINI_MODEL = "gemini-2.5-flash"
DEFAULT_GEMINI_BASE_URL = "https://generativelanguage.googleapis.com/v1beta"
MAX_SYNTHESIS_CHARS = 1500


@dataclass(frozen=True)
class GeminiConfig:
    api_key: Optional[str] = None
    model: str = DEFAULT_GEMINI_MODEL
    base_url: str = DEFAULT_GEMINI_BASE_URL
    timeout_seconds: float = 10.0
    temperature: float = 0.1
    max_output_tokens: int = 1024

    @classmethod
    def from_env(cls) -> GeminiConfig:
        raw_key = os.getenv("GEMINI_API_KEY", "").strip()
        model = os.getenv("GEMINI_MODEL", DEFAULT_GEMINI_MODEL).strip() or DEFAULT_GEMINI_MODEL
        base_url = (
            os.getenv("GEMINI_API_BASE_URL", DEFAULT_GEMINI_BASE_URL).strip()
            or DEFAULT_GEMINI_BASE_URL
        ).rstrip("/")
        try:
            timeout = float(os.getenv("GEMINI_TIMEOUT_SECONDS", "10.0"))
        except ValueError:
            timeout = 10.0
        try:
            temperature = float(os.getenv("GEMINI_TEMPERATURE", "0.1"))
        except ValueError:
            temperature = 0.1
        try:
            max_tokens = int(os.getenv("GEMINI_MAX_OUTPUT_TOKENS", "1024"))
        except ValueError:
            max_tokens = 1024

        return cls(
            api_key=raw_key if raw_key else None,
            model=model,
            base_url=base_url,
            timeout_seconds=max(1.0, min(timeout, 60.0)),
            temperature=max(0.0, min(temperature, 1.0)),
            max_output_tokens=max(128, min(max_tokens, 4096)),
        )

    @property
    def is_configured(self) -> bool:
        return bool(self.api_key)


class GeminiClient:
    """Zero-dependency Google AI Studio Gemini REST client with untrusted-output validation."""

    def __init__(
        self,
        config: Optional[GeminiConfig] = None,
        transport: Optional[Callable[[str, bytes, Dict[str, str], float], Tuple[int, str]]] = None,
    ) -> None:
        self.config = config or GeminiConfig.from_env()
        self._transport = transport

    def synthesize_context(
        self,
        agent_input: ExperienceAgentInput,
        system_instruction: str,
        destination: Optional[DestinationContext],
        activity: Optional[ActivityContext],
        offering: Optional[OfferingContext],
        schedule: Optional[ScheduleContext],
        biodiversity: BiodiversityContext,
        existing_gaps: List[str],
    ) -> Tuple[ModelExecutionSummary, List[str]]:
        """Invoke Google AI Studio Gemini generateContent to synthesize evidence-grounded notes.

        Returns a tuple of (ModelExecutionSummary, validated_additional_uncertainty_notes).
        Never mutates deterministic domain objects.
        """
        if not self.config.is_configured or not self.config.api_key:
            return (
                ModelExecutionSummary(
                    provider="google-ai-studio",
                    model=self.config.model,
                    status=ModelExecutionStatus.SKIPPED_NO_API_KEY,
                    latency_ms=0.0,
                    synthesis_summary=(
                        "Deterministic tool-grounded context assembled (GEMINI_API_KEY not configured; "
                        "LLM narrative synthesis skipped)."
                    ),
                ),
                [],
            )

        endpoint = f"{self.config.base_url}/models/{self.config.model}:generateContent"
        minimized_context = self._build_minimized_prompt_context(
            agent_input=agent_input,
            destination=destination,
            activity=activity,
            offering=offering,
            schedule=schedule,
            biodiversity=biodiversity,
            existing_gaps=existing_gaps,
        )

        request_payload = {
            "systemInstruction": {
                "parts": [
                    {
                        "text": (
                            f"{system_instruction.strip()}\n\n"
                            "Respond ONLY with a valid JSON object matching this exact schema:\n"
                            "{\n"
                            '  "synthesis_summary": "string (concise factual synthesis grounded only in supplied tool evidence)",\n'
                            '  "uncertainty_notes": ["string (optional evidence gaps or caveats)"],\n'
                            '  "proposed_tools": ["string (optional subset of allowlisted_tools)"]\n'
                            "}\n"
                            "Never invent species, never override unavailable statuses, and never include secrets."
                        )
                    }
                ]
            },
            "contents": [
                {
                    "role": "user",
                    "parts": [
                        {
                            "text": json.dumps(minimized_context, sort_keys=True),
                        }
                    ],
                }
            ],
            "generationConfig": {
                "temperature": self.config.temperature,
                "maxOutputTokens": self.config.max_output_tokens,
                "responseMimeType": "application/json",
            },
        }

        body_bytes = json.dumps(request_payload).encode("utf-8")
        headers = {
            "Content-Type": "application/json",
            "Accept": "application/json",
            "x-goog-api-key": self.config.api_key,
        }

        start = time.perf_counter()
        status_code, response_body, transport_error = self._execute_post(
            endpoint, body_bytes, headers, self.config.timeout_seconds
        )
        elapsed_ms = round((time.perf_counter() - start) * 1000.0, 2)

        if transport_error is not None:
            return (
                ModelExecutionSummary(
                    provider="google-ai-studio",
                    model=self.config.model,
                    status=ModelExecutionStatus.UNAVAILABLE,
                    latency_ms=elapsed_ms,
                    error_summary=f"Gemini API transport failure: {transport_error}",
                ),
                [],
            )

        if status_code == 429:
            return (
                ModelExecutionSummary(
                    provider="google-ai-studio",
                    model=self.config.model,
                    status=ModelExecutionStatus.RATE_LIMITED,
                    latency_ms=elapsed_ms,
                    error_summary=(
                        "Google AI Studio free-tier rate limit reached (HTTP 429); "
                        "falling back to deterministic tool-grounded report."
                    ),
                ),
                [],
            )

        if status_code < 200 or status_code >= 300:
            return (
                ModelExecutionSummary(
                    provider="google-ai-studio",
                    model=self.config.model,
                    status=ModelExecutionStatus.UNAVAILABLE,
                    latency_ms=elapsed_ms,
                    error_summary=f"Gemini API returned HTTP {status_code}; using deterministic tool-grounded report.",
                ),
                [],
            )

        return self._validate_and_extract_response(
            response_body=response_body,
            elapsed_ms=elapsed_ms,
            offering=offering,
            biodiversity=biodiversity,
        )

    def _execute_post(
        self,
        url: str,
        body_bytes: bytes,
        headers: Dict[str, str],
        timeout: float,
    ) -> Tuple[int, str, Optional[str]]:
        if self._transport is not None:
            try:
                status, text = self._transport(url, body_bytes, headers, timeout)
                return status, text, None
            except Exception as exc:
                return 0, "", type(exc).__name__

        req = urllib.request.Request(url, data=body_bytes, headers=headers, method="POST")
        context = ssl.create_default_context()
        try:
            with urllib.request.urlopen(req, timeout=timeout, context=context) as resp:
                return resp.getcode(), resp.read().decode("utf-8"), None
        except urllib.error.HTTPError as exc:
            try:
                err_body = exc.read().decode("utf-8")
            except Exception:
                err_body = ""
            return exc.code, err_body, None
        except urllib.error.URLError as exc:
            return 0, "", f"URLError:{getattr(exc, 'reason', 'connection_failed')}"
        except TimeoutError:
            return 0, "", "TimeoutError"
        except Exception as exc:
            return 0, "", type(exc).__name__

    def _build_minimized_prompt_context(
        self,
        agent_input: ExperienceAgentInput,
        destination: Optional[DestinationContext],
        activity: Optional[ActivityContext],
        offering: Optional[OfferingContext],
        schedule: Optional[ScheduleContext],
        biodiversity: BiodiversityContext,
        existing_gaps: List[str],
    ) -> Dict[str, Any]:
        """Minimize context sent to Google AI Studio (no tokens, user PII, or secrets)."""
        return {
            "workflow_id": agent_input.workflow_id,
            "step_id": agent_input.step_id,
            "objective": agent_input.objective,
            "allowlisted_tools": list(ALLOWLISTED_TOOLS),
            "destination": (
                {
                    "destination_id": destination.destination_id,
                    "name": destination.name,
                    "region": destination.region,
                    "status": destination.status,
                }
                if destination
                else None
            ),
            "activity": (
                {
                    "activity_id": activity.activity_id,
                    "title": activity.title,
                    "category": activity.category,
                    "status": activity.status,
                    "experience_level": activity.experience_level,
                    "constraints": activity.constraints,
                }
                if activity
                else None
            ),
            "offering": (
                {
                    "offering_id": offering.offering_id,
                    "publication_status": offering.publication_status,
                    "schedule_status": offering.schedule_status,
                    "availability_status": offering.availability_status,
                    "is_available": offering.is_available,
                }
                if offering
                else None
            ),
            "schedule": (
                {
                    "is_scheduled": schedule.is_scheduled,
                    "open_slots_count": sum(1 for s in schedule.slots if s.is_open),
                    "availability_status": schedule.availability_status,
                    "reason_codes": schedule.reason_codes,
                }
                if schedule
                else None
            ),
            "biodiversity": {
                "status": biodiversity.status.value,
                "model_version": biodiversity.model_version,
                "species_names": [p.species_name for p in biodiversity.predictions],
                "disclaimer": biodiversity.disclaimer,
            },
            "deterministic_uncertainty_gaps": existing_gaps,
        }

    def _validate_and_extract_response(
        self,
        response_body: str,
        elapsed_ms: float,
        offering: Optional[OfferingContext],
        biodiversity: BiodiversityContext,
    ) -> Tuple[ModelExecutionSummary, List[str]]:
        try:
            envelope = json.loads(response_body)
        except json.JSONDecodeError:
            return (
                ModelExecutionSummary(
                    provider="google-ai-studio",
                    model=self.config.model,
                    status=ModelExecutionStatus.INVALID_OUTPUT,
                    latency_ms=elapsed_ms,
                    error_summary="Gemini response envelope was not valid JSON; output discarded.",
                ),
                [],
            )

        if not isinstance(envelope, dict):
            return (
                ModelExecutionSummary(
                    provider="google-ai-studio",
                    model=self.config.model,
                    status=ModelExecutionStatus.INVALID_OUTPUT,
                    latency_ms=elapsed_ms,
                    error_summary="Gemini response envelope was not a JSON object; output discarded.",
                ),
                [],
            )

        usage = envelope.get("usageMetadata") if isinstance(envelope.get("usageMetadata"), dict) else {}
        prompt_tokens = usage.get("promptTokenCount") if isinstance(usage.get("promptTokenCount"), int) else None
        candidate_tokens = (
            usage.get("candidatesTokenCount") if isinstance(usage.get("candidatesTokenCount"), int) else None
        )
        resolved_model = str(envelope.get("modelVersion") or self.config.model)

        candidates = envelope.get("candidates")
        if not isinstance(candidates, list) or not candidates:
            return (
                ModelExecutionSummary(
                    provider="google-ai-studio",
                    model=resolved_model,
                    status=ModelExecutionStatus.INVALID_OUTPUT,
                    latency_ms=elapsed_ms,
                    prompt_tokens=prompt_tokens,
                    candidate_tokens=candidate_tokens,
                    error_summary="Gemini response contained no candidates; output discarded.",
                ),
                [],
            )

        first_candidate = candidates[0] if isinstance(candidates[0], dict) else {}
        content = first_candidate.get("content") if isinstance(first_candidate.get("content"), dict) else {}
        parts = content.get("parts") if isinstance(content.get("parts"), list) else []

        # Strip hidden chain-of-thought ('thought': True) parts per security policy
        visible_texts: List[str] = []
        for part in parts:
            if not isinstance(part, dict):
                continue
            if part.get("thought") is True:
                continue
            text_val = part.get("text")
            if isinstance(text_val, str) and text_val.strip():
                visible_texts.append(text_val.strip())

        if not visible_texts:
            return (
                ModelExecutionSummary(
                    provider="google-ai-studio",
                    model=resolved_model,
                    status=ModelExecutionStatus.INVALID_OUTPUT,
                    latency_ms=elapsed_ms,
                    prompt_tokens=prompt_tokens,
                    candidate_tokens=candidate_tokens,
                    error_summary="Gemini candidate contained no non-thought text content; output discarded.",
                ),
                [],
            )

        raw_json_text = "\n".join(visible_texts).strip()
        # Strip accidental markdown code fences if present
        if raw_json_text.startswith("```"):
            lines = raw_json_text.splitlines()
            if len(lines) >= 3 and lines[-1].strip() == "```":
                raw_json_text = "\n".join(lines[1:-1]).strip()

        try:
            parsed_output = json.loads(raw_json_text)
        except json.JSONDecodeError:
            return (
                ModelExecutionSummary(
                    provider="google-ai-studio",
                    model=resolved_model,
                    status=ModelExecutionStatus.INVALID_OUTPUT,
                    latency_ms=elapsed_ms,
                    prompt_tokens=prompt_tokens,
                    candidate_tokens=candidate_tokens,
                    error_summary="Gemini candidate text was not valid structured JSON; output discarded.",
                ),
                [],
            )

        if not isinstance(parsed_output, dict):
            return (
                ModelExecutionSummary(
                    provider="google-ai-studio",
                    model=resolved_model,
                    status=ModelExecutionStatus.INVALID_OUTPUT,
                    latency_ms=elapsed_ms,
                    prompt_tokens=prompt_tokens,
                    candidate_tokens=candidate_tokens,
                    error_summary="Gemini structured output must be a JSON object; output discarded.",
                ),
                [],
            )

        # Check for forbidden keys or unauthorized status overrides in model output
        for key in parsed_output:
            norm_key = str(key).strip().lower()
            if norm_key in FORBIDDEN_SECRET_KEYS or norm_key in {"thought", "reasoning", "chain_of_thought"}:
                return (
                    ModelExecutionSummary(
                        provider="google-ai-studio",
                        model=resolved_model,
                        status=ModelExecutionStatus.INVALID_OUTPUT,
                        latency_ms=elapsed_ms,
                        prompt_tokens=prompt_tokens,
                        candidate_tokens=candidate_tokens,
                        error_summary=f"Gemini output contained forbidden key '{key}'; output discarded.",
                    ),
                    [],
                )

        # Validate proposed_tools against ALLOWLISTED_TOOLS
        proposed_tools = parsed_output.get("proposed_tools")
        if proposed_tools is not None:
            if not isinstance(proposed_tools, list) or any(
                not isinstance(t, str) or t not in ALLOWLISTED_TOOLS for t in proposed_tools
            ):
                return (
                    ModelExecutionSummary(
                        provider="google-ai-studio",
                        model=resolved_model,
                        status=ModelExecutionStatus.INVALID_OUTPUT,
                        latency_ms=elapsed_ms,
                        prompt_tokens=prompt_tokens,
                        candidate_tokens=candidate_tokens,
                        error_summary="Gemini proposed a tool outside ALLOWLISTED_TOOLS; output discarded.",
                    ),
                    [],
                )

        # Reject any attempt by the model to override deterministic availability or fabricate species
        if "is_available" in parsed_output or "availability_status" in parsed_output or "predictions" in parsed_output:
            return (
                ModelExecutionSummary(
                    provider="google-ai-studio",
                    model=resolved_model,
                    status=ModelExecutionStatus.INVALID_OUTPUT,
                    latency_ms=elapsed_ms,
                    prompt_tokens=prompt_tokens,
                    candidate_tokens=candidate_tokens,
                    error_summary=(
                        "Gemini attempted to overwrite deterministic availability or species predictions; "
                        "output discarded."
                    ),
                ),
                [],
            )

        synthesis_summary = parsed_output.get("synthesis_summary")
        if not isinstance(synthesis_summary, str) or not synthesis_summary.strip():
            return (
                ModelExecutionSummary(
                    provider="google-ai-studio",
                    model=resolved_model,
                    status=ModelExecutionStatus.INVALID_OUTPUT,
                    latency_ms=elapsed_ms,
                    prompt_tokens=prompt_tokens,
                    candidate_tokens=candidate_tokens,
                    error_summary="Gemini output omitted required non-empty 'synthesis_summary'; output discarded.",
                ),
                [],
            )

        clean_summary = synthesis_summary.strip()[:MAX_SYNTHESIS_CHARS]

        # Validate uncertainty_notes
        raw_notes = parsed_output.get("uncertainty_notes", [])
        validated_notes: List[str] = []
        if isinstance(raw_notes, list):
            for item in raw_notes[:10]:
                if isinstance(item, str) and item.strip():
                    validated_notes.append(item.strip()[:300])

        return (
            ModelExecutionSummary(
                provider="google-ai-studio",
                model=resolved_model,
                status=ModelExecutionStatus.USED,
                latency_ms=elapsed_ms,
                prompt_tokens=prompt_tokens,
                candidate_tokens=candidate_tokens,
                synthesis_summary=clean_summary,
            ),
            validated_notes,
        )

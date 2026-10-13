"""Proposal-only Safety & Operations role, isolated behind a model port."""

from __future__ import annotations

from dataclasses import dataclass
from datetime import datetime, timezone
from typing import Any, Mapping, Protocol

from .contracts import ContractError, SafetyAssessmentInput, SafetyOperationsRecommendation
from .prompt import PROMPT_VERSION, SYSTEM_INSTRUCTIONS


@dataclass(frozen=True)
class ModelCall:
    agent_id: str
    prompt_version: str
    system_instructions: str
    context: Mapping[str, Any]
    allowed_tools: tuple[str, ...] = ()
    timeout_seconds: float = 4.0


class ModelPort(Protocol):
    def generate(self, request: ModelCall) -> Mapping[str, Any]:
        """Return an untrusted JSON object matching the requested schema."""


class ModelUnavailable(Exception):
    """The selected model provider cannot accept this bounded request."""


@dataclass(frozen=True)
class AgentResult:
    status: str
    retryable: bool
    recommendation: SafetyOperationsRecommendation | None = None
    code: str | None = None

    def to_mapping(self) -> dict[str, Any]:
        return {
            "status": self.status,
            "retryable": self.retryable,
            "code": self.code,
            "recommendation": self.recommendation.to_mapping() if self.recommendation else None,
        }


class SafetyOperationsAgent:
    """Validate context, request one structured proposal, then validate it."""

    agent_id = "safety-operations"
    MODEL_TIMEOUT_SECONDS = 4.0

    def __init__(self, model: ModelPort, clock=None) -> None:
        self._model = model
        self._clock = clock or (lambda: datetime.now(timezone.utc))

    def recommend(self, raw_input: Mapping[str, Any]) -> AgentResult:
        try:
            request = SafetyAssessmentInput.from_mapping(raw_input)
        except ContractError:
            return AgentResult("INVALID_INPUT", False, code="INPUT_SCHEMA_INVALID")

        evidence_status = request.evidence_status(self._clock())
        if evidence_status == "BLOCKED":
            return AgentResult("BLOCKED", False, code="DETERMINISTIC_EVIDENCE_BLOCK")
        if evidence_status != "READY":
            return AgentResult("INSUFFICIENT_DATA", False, code="REQUIRED_EVIDENCE_UNAVAILABLE")

        model_call = ModelCall(
            agent_id=self.agent_id,
            prompt_version=PROMPT_VERSION,
            system_instructions=SYSTEM_INSTRUCTIONS,
            context=request.to_model_context(),
            # No tool executor is wired until the shared AI-00 tool contract is accepted.
            allowed_tools=(),
            timeout_seconds=self.MODEL_TIMEOUT_SECONDS,
        )
        try:
            raw_result = self._model.generate(model_call)
        except ModelUnavailable:
            return AgentResult("UNAVAILABLE", True, code="MODEL_UNAVAILABLE")
        except TimeoutError:
            return AgentResult("UNAVAILABLE", True, code="MODEL_TIMEOUT")
        except Exception:
            # Do not return provider messages, URLs, or configuration details.
            return AgentResult("UNAVAILABLE", False, code="MODEL_FAILURE")

        try:
            recommendation = SafetyOperationsRecommendation.from_mapping(raw_result, request)
        except ContractError:
            return AgentResult("INVALID_RESULT", False, code="OUTPUT_SCHEMA_INVALID")
        if recommendation.recommendation == "INSUFFICIENT_DATA":
            return AgentResult(
                "INSUFFICIENT_DATA", False, recommendation, code="MODEL_REPORTED_INSUFFICIENT_DATA"
            )
        return AgentResult("SUCCEEDED", False, recommendation)

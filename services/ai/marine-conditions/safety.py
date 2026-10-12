"""Deterministic safety validation and prompt injection defenses.

Enforces:
1. Prompt injection neutralization on untrusted objectives.
2. Strict non-authority: agent cannot set suitability, invent thresholds, or authorize operations.
3. Factual grounding: reported factors must originate strictly from tool evidence.
4. Non-negotiable disclaimer inclusion.
"""

from __future__ import annotations

from datetime import datetime, timezone
import logging
import re
from typing import Any, Dict, List, Optional, Set, Tuple

try:
    from .schemas import (
        EnvironmentalFactor,
        FreshnessAssessment,
        FreshnessStatus,
        MarineConditionsReport,
        ReportStatus,
        STANDARD_DISCLAIMER,
        parse_iso_datetime,
    )
except ImportError:
    from schemas import (
        EnvironmentalFactor,
        FreshnessAssessment,
        FreshnessStatus,
        MarineConditionsReport,
        ReportStatus,
        STANDARD_DISCLAIMER,
        parse_iso_datetime,
    )

logger = logging.getLogger("blueverse.ai.marine_conditions.safety")

INJECTION_PATTERNS = [
    r"(?i)\bignore\s+(all\s+)?(previous|prior|above)\s+instructions\b",
    r"(?i)\byou\s+are\s+now\s+(an?\s+)?(unrestricted|jailbroken|admin|developer|system)\b",
    r"(?i)\bsystem\s+override\b",
    r"(?i)\bdeveloper\s+mode\b",
    r"(?i)\bgrant\s+(me\s+)?(tool|permission|access|role)\b",
    r"(?i)\bexecute\s+(code|sql|shell|bash|command)\b",
    r"(?i)\b(set|override|bypass)\s+(safety|threshold|limit|suitability)\b",
    r"(?i)\bforce\s+(suitable|unsuitable|caution|approve)\b",
    r"(?i)\bdisregard\s+(rules|guidelines|safety)\b",
]


class SafetyViolationError(ValueError):
    """Raised when an Agent output violates deterministic safety policy."""
    pass


class PromptInjectionGuard:
    """Detects and neutralizes prompt injection attempts in untrusted input text."""

    @staticmethod
    def inspect(text: str) -> Tuple[bool, List[str]]:
        """Inspects text for injection patterns. Returns (is_suspicious, matched_patterns)."""
        if not text:
            return False, []
        matched = []
        for pat in INJECTION_PATTERNS:
            if re.search(pat, text):
                matched.append(pat)
        return len(matched) > 0, matched

    @staticmethod
    def sanitize(text: str) -> str:
        """Sanitizes text by stripping dangerous injection directives."""
        if not text:
            return ""
        sanitized = text
        for pat in INJECTION_PATTERNS:
            sanitized = re.sub(pat, "[FILTERED_INSTRUCTION]", sanitized)
        return sanitized.strip()


class DeterministicSafetyValidator:
    """Deterministic validation layer ensuring strict adherence to non-authority contracts."""

    FORBIDDEN_AUTHORITY_PHRASES = [
        r"(?i)\boperation\s+(is\s+)?(hereby\s+)?approved\b",
        r"(?i)\boperation\s+(is\s+)?(hereby\s+)?suspended\b",
        r"(?i)\boperation\s+(is\s+)?(hereby\s+)?cancelled\b",
        r"(?i)\bofficial\s+beach\s+closure\b",
        r"(?i)\bnavigational\s+clearance\s+granted\b",
    ]

    @classmethod
    def validate_report(
        cls,
        report: MarineConditionsReport,
        tool_evidence: Dict[str, Any],
    ) -> None:
        """Deterministic verification of agent report. Raises SafetyViolationError if invalid."""
        # 1. Enforce disclaimer presence
        if not report.disclaimer or STANDARD_DISCLAIMER not in report.disclaimer:
            raise SafetyViolationError(
                f"Report must contain the standard navigation disclaimer: '{STANDARD_DISCLAIMER}'"
            )

        # 2. Enforce non-authority over operations
        summary = (report.objective_relevance_summary or "") + " " + (report.uncertainty_and_limitations or "")
        for pat in cls.FORBIDDEN_AUTHORITY_PHRASES:
            if re.search(pat, summary):
                raise SafetyViolationError(
                    f"Agent attempted unauthorized operational instruction matching pattern '{pat}'."
                )

        # 3. Grounding check: verify factors in report originate from tool evidence
        for factor_name, factor_obj in report.environmental_factors.items():
            if factor_obj.value is None:
                continue

            # Verify that the factor was actually returned by the tools
            if factor_name not in tool_evidence:
                raise SafetyViolationError(
                    f"Hallucination detected: environmental factor '{factor_name}' was not present in tool evidence."
                )

            evidence_val = tool_evidence[factor_name].get("value")
            if evidence_val is None:
                raise SafetyViolationError(
                    f"Inconsistency detected: environmental factor '{factor_name}' is None in tool evidence but set to {factor_obj.value} in report."
                )

            # Check numeric equivalence within reasonable floating point rounding
            if abs(float(factor_obj.value) - float(evidence_val)) > 0.01:
                raise SafetyViolationError(
                    f"Falsification detected: environmental factor '{factor_name}' value {factor_obj.value} differs from tool evidence {evidence_val}."
                )

    @classmethod
    def calculate_freshness(
        cls,
        retrieval_time_str: str,
        forecast_or_observed_time_str: Optional[str] = None,
        max_freshness_minutes: float = 60.0,
    ) -> FreshnessAssessment:
        """Calculate freshness assessment deterministically."""
        try:
            retrieval_dt = parse_iso_datetime(retrieval_time_str)
            now = datetime.now(timezone.utc)
            age_minutes = max(0.0, (now - retrieval_dt).total_seconds() / 60.0)

            if age_minutes <= max_freshness_minutes:
                status = FreshnessStatus.FRESH
            else:
                status = FreshnessStatus.STALE

            rule = f"FreshnessMaxAgeMinutes: {int(max_freshness_minutes)}"
            return FreshnessAssessment(status=status, rule=rule, age_minutes=round(age_minutes, 2))
        except Exception as exc:
            logger.warning("Error calculating freshness: %s", exc)
            return FreshnessAssessment(
                status=FreshnessStatus.UNKNOWN,
                rule=f"FreshnessMaxAgeMinutes: {int(max_freshness_minutes)}",
                age_minutes=None,
            )

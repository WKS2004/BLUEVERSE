"""Marine Conditions Intelligence Agent implementation.

Role: Evidence summarizer and marine/weather specialist.
Contract ID: v1.agent.marine-conditions
Owner: Sanuda Abeysinghe (@sanudaabey), Member 2
"""

from __future__ import annotations

from datetime import datetime, timezone
import json
import logging
from typing import Any, Callable, Dict, List, Optional, Tuple

try:
    from .audit import audit_logger
    from .gemini import GeminiClient
    from .safety import (
        DeterministicSafetyValidator,
        PromptInjectionGuard,
        SafetyViolationError,
    )
    from .schemas import (
        DEFAULT_REQUIRED_FACTORS,
        EnvironmentalFactor,
        FreshnessAssessment,
        FreshnessStatus,
        MarineConditionsAgentInput,
        MarineConditionsReport,
        ReportStatus,
        SourceProvenance,
        STANDARD_DISCLAIMER,
        format_iso_datetime,
        parse_iso_datetime,
    )
    from .tools import ToolExecutionResult, ToolRegistry
except ImportError:
    from audit import audit_logger
    from gemini import GeminiClient
    from safety import (
        DeterministicSafetyValidator,
        PromptInjectionGuard,
        SafetyViolationError,
    )
    from schemas import (
        DEFAULT_REQUIRED_FACTORS,
        EnvironmentalFactor,
        FreshnessAssessment,
        FreshnessStatus,
        MarineConditionsAgentInput,
        MarineConditionsReport,
        ReportStatus,
        SourceProvenance,
        STANDARD_DISCLAIMER,
        format_iso_datetime,
        parse_iso_datetime,
    )
    from tools import ToolExecutionResult, ToolRegistry

logger = logging.getLogger("blueverse.ai.marine_conditions.agent")


class MarineConditionsIntelligenceAgent:
    """Specialist Agent that collects and interprets weather & marine context for BLUEVERSE."""

    SYSTEM_INSTRUCTIONS = (
        "You are the Marine Conditions Intelligence Agent for the BLUEVERSE ecosystem "
        "(Contract: v1.agent.marine-conditions, Owner: Sanuda Abeysinghe).\n"
        "Your role is to collect and interpret weather and marine context for a validated objective.\n"
        "RULES:\n"
        "1. Never invent or falsify weather or marine values absent from tool evidence.\n"
        "2. Distinguish measured/forecast zero (e.g. 0.0 mm rain) from missing, null, or failed.\n"
        "3. Do not invent physical safety thresholds for coastal activities.\n"
        "4. Do not assign or override suitability grades (SUITABLE, CAUTION, UNSUITABLE, UNKNOWN).\n"
        "5. Do not authorize operations, suspensions, closures, or emergency alerts.\n"
        "6. Provide factual decision support only, never professional marine navigation guidance."
    )

    def __init__(
        self,
        tool_registry: Optional[ToolRegistry] = None,
        model_generator: Optional[Callable[[str, Dict[str, Any]], Dict[str, Any]]] = None,
        gemini_client: Optional[GeminiClient] = None,
    ):
        self.tool_registry = tool_registry or ToolRegistry()
        self.model_generator = model_generator
        self.gemini_client = gemini_client or GeminiClient()

    def execute(self, input_data: MarineConditionsAgentInput) -> MarineConditionsReport:
        """Executes the agent workflow for the given validated input."""
        logger.info(
            "Executing Marine Conditions Agent: workflow=%s step=%s activity=%s",
            input_data.workflow_id,
            input_data.step_id,
            input_data.activity,
        )

        # 1. Guard against prompt injection in untrusted objective
        is_suspicious, patterns = PromptInjectionGuard.inspect(input_data.objective)
        if is_suspicious:
            logger.warning(
                "Potential prompt injection detected in objective for workflow %s: %s",
                input_data.workflow_id,
                patterns,
            )
        sanitized_objective = PromptInjectionGuard.sanitize(input_data.objective)

        # 2. Extract input parameters
        lat = input_data.location.latitude
        lon = input_data.location.longitude
        start_time = input_data.period.start_time
        end_time = input_data.period.end_time
        allowed_tools = input_data.tool_policy.allowed_tools
        timeout_seconds = input_data.tool_policy.timeout_seconds

        combined_factors: Dict[str, EnvironmentalFactor] = {}
        missing_factors: List[str] = []
        raw_evidence_values: Dict[str, Any] = {}
        source_updated_times: List[str] = []

        weather_failed = False
        marine_failed = False
        provider_error_msg: Optional[str] = None

        # 3. Check for snapshot lookup in existing context
        snapshot_id = (input_data.existing_context or {}).get("snapshot_id")
        if snapshot_id and "condition_snapshot_lookup" in allowed_tools:
            snapshot_res = self.tool_registry.execute_tool(
                tool_name="condition_snapshot_lookup",
                arguments={"snapshot_id": snapshot_id},
                workflow_id=input_data.workflow_id,
                step_id=input_data.step_id,
                allowed_tools=allowed_tools,
                timeout_seconds=timeout_seconds,
            )
            if snapshot_res.success and snapshot_res.data:
                for k, v in snapshot_res.data.items():
                    val = v.get("value")
                    combined_factors[k] = EnvironmentalFactor.from_dict(v)
                    raw_evidence_values[k] = {"value": val, "unit": v.get("unit")}
                missing_factors.extend(snapshot_res.missing_factors)
                if snapshot_res.source_updated_time:
                    source_updated_times.append(str(snapshot_res.source_updated_time))

        # 4. Execute allowlisted forecast lookups if factors still needed
        weather_needed = any(f in input_data.required_factors for f in ["wind_speed", "precipitation", "weather_code", "temperature"]) or not combined_factors
        marine_needed = any(f in input_data.required_factors for f in ["wave_height", "swell_wave_height", "wave_direction", "wave_period"]) or not combined_factors

        if weather_needed and "weather_forecast_lookup" in allowed_tools:
            w_res = self.tool_registry.execute_tool(
                tool_name="weather_forecast_lookup",
                arguments={"latitude": lat, "longitude": lon, "time": start_time},
                workflow_id=input_data.workflow_id,
                step_id=input_data.step_id,
                allowed_tools=allowed_tools,
                timeout_seconds=timeout_seconds,
            )
            if w_res.success and w_res.data:
                for k, v in w_res.data.items():
                    val = v.get("value")
                    combined_factors[k] = EnvironmentalFactor.from_dict(v)
                    raw_evidence_values[k] = {"value": val, "unit": v.get("unit")}
                missing_factors.extend(w_res.missing_factors)
                if w_res.source_updated_time:
                    source_updated_times.append(str(w_res.source_updated_time))
            else:
                weather_failed = True
                provider_error_msg = w_res.error

        if marine_needed and "marine_forecast_lookup" in allowed_tools:
            m_res = self.tool_registry.execute_tool(
                tool_name="marine_forecast_lookup",
                arguments={"latitude": lat, "longitude": lon, "time": start_time},
                workflow_id=input_data.workflow_id,
                step_id=input_data.step_id,
                allowed_tools=allowed_tools,
                timeout_seconds=timeout_seconds,
            )
            if m_res.success and m_res.data:
                for k, v in m_res.data.items():
                    val = v.get("value")
                    combined_factors[k] = EnvironmentalFactor.from_dict(v)
                    raw_evidence_values[k] = {"value": val, "unit": v.get("unit")}
                missing_factors.extend(m_res.missing_factors)
                if m_res.source_updated_time:
                    source_updated_times.append(str(m_res.source_updated_time))
            else:
                marine_failed = True
                provider_error_msg = provider_error_msg or m_res.error

        # Deduplicate missing factors while preserving order
        unique_missing = list(dict.fromkeys(missing_factors))

        # 5. Handle complete failure / outage
        if (weather_failed and marine_failed) or (not combined_factors and (weather_failed or marine_failed)):
            logger.warning(
                "Source provider unavailable for workflow %s: %s",
                input_data.workflow_id,
                provider_error_msg,
            )
            return MarineConditionsReport(
                workflow_id=input_data.workflow_id,
                step_id=input_data.step_id,
                status=ReportStatus.SOURCE_UNAVAILABLE,
                location=input_data.location,
                activity=input_data.activity,
                requested_period=input_data.period,
                source_provenance=SourceProvenance(
                    provider="Open-Meteo",
                    retrieval_time=format_iso_datetime(datetime.now(timezone.utc)),
                    is_forecast=True,
                ),
                freshness=FreshnessAssessment(status=FreshnessStatus.UNKNOWN, rule="Provider unavailable"),
                environmental_factors={},
                missing_or_unavailable_factors=list(input_data.required_factors),
                objective_relevance_summary="Environmental context cannot be evaluated due to external provider outage.",
                uncertainty_and_limitations="Open-Meteo services could not be reached; no condition data was retrieved.",
                disclaimer=STANDARD_DISCLAIMER,
                error_detail=provider_error_msg or "Open-Meteo provider outage or timeout.",
            )

        # 6. Determine Report Status
        missing_required = [f for f in input_data.required_factors if f in unique_missing or f not in combined_factors]
        if missing_required or weather_failed or marine_failed:
            report_status = ReportStatus.INCOMPLETE
        else:
            report_status = ReportStatus.COMPLETE

        # 7. Generate Objective Relevance and Uncertainty Summaries
        retrieval_time_str = format_iso_datetime(datetime.now(timezone.utc))
        freshness = DeterministicSafetyValidator.calculate_freshness(retrieval_time_str)

        relevance_summary, limitations = self._generate_summaries(
            sanitized_objective=sanitized_objective,
            activity=input_data.activity,
            factors=combined_factors,
            missing=unique_missing,
            is_suspicious=is_suspicious,
        )

        report = MarineConditionsReport(
            workflow_id=input_data.workflow_id,
            step_id=input_data.step_id,
            status=report_status,
            location=input_data.location,
            activity=input_data.activity,
            requested_period=input_data.period,
            source_provenance=SourceProvenance(
                provider="Open-Meteo",
                retrieval_time=retrieval_time_str,
                source_updated_time=source_updated_times[0] if source_updated_times else None,
                is_forecast=True,
            ),
            freshness=freshness,
            environmental_factors=combined_factors,
            missing_or_unavailable_factors=unique_missing,
            objective_relevance_summary=relevance_summary,
            uncertainty_and_limitations=limitations,
            disclaimer=STANDARD_DISCLAIMER,
        )

        # 8. Deterministic Safety Verification
        DeterministicSafetyValidator.validate_report(report, raw_evidence_values)

        return report

    def _generate_summaries(
        self,
        sanitized_objective: str,
        activity: str,
        factors: Dict[str, EnvironmentalFactor],
        missing: List[str],
        is_suspicious: bool,
    ) -> Tuple[str, str]:
        """Synthesizes objective-relevance and uncertainty summaries grounded strictly in facts."""
        # 1. Attempt Gemini model generation if configured
        if self.gemini_client and self.gemini_client.is_configured:
            gemini_relevance, gemini_limitations, err = self.gemini_client.generate_conditions_summary(
                sanitized_objective=sanitized_objective,
                activity=activity,
                factors=factors,
                missing=missing,
            )
            if not err and gemini_relevance:
                if is_suspicious and "neutralized" not in (gemini_limitations or "").lower():
                    gemini_limitations = ((gemini_limitations or "") + " Untrusted objective directives were neutralized during analysis.").strip()
                return gemini_relevance, gemini_limitations or ""

        # 2. Deterministic fallback synthesis
        factor_desc = []
        if "wind_speed" in factors:
            factor_desc.append(f"wind speed is {factors['wind_speed'].value} {factors['wind_speed'].unit}")
        if "wave_height" in factors:
            factor_desc.append(f"wave height is {factors['wave_height'].value} {factors['wave_height'].unit}")
        if "swell_wave_height" in factors:
            factor_desc.append(f"swell wave height is {factors['swell_wave_height'].value} {factors['swell_wave_height'].unit}")
        if "precipitation" in factors:
            factor_desc.append(f"precipitation is {factors['precipitation'].value} {factors['precipitation'].unit}")
        if "weather_code" in factors:
            w_desc = factors["weather_code"].description or f"code {factors['weather_code'].value}"
            factor_desc.append(f"weather condition is {w_desc}")

        relevance = (
            f"Observed context for {activity}: {', '.join(factor_desc)}."
            if factor_desc
            else f"No environmental variables retrieved for {activity}."
        )

        limitations_parts = [
            "Data sourced from Open-Meteo numerical forecast models with 0.25-degree resolution."
        ]
        if missing:
            limitations_parts.append(f"Variables unavailable from provider: {', '.join(missing)}.")
        if is_suspicious:
            limitations_parts.append("Untrusted objective directives were neutralized during analysis.")

        limitations = " ".join(limitations_parts)
        return relevance, limitations

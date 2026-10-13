"""Evaluation tests for Section 40 Agentic AI Requirements.

Evaluates:
1. Canonical Assessed Workflow: Coastal Activity Operational Assessment (snorkeling).
2. Tourist recommendation planning support.
3. Provider outage & Safe Failure handling without fabricated data.
"""

import unittest
from typing import Any, Dict, Tuple

from agent import MarineConditionsIntelligenceAgent
from schemas import (
    LocationDto,
    MarineConditionsAgentInput,
    ReportStatus,
    STANDARD_DISCLAIMER,
    TimePeriodDto,
)
from tests.fixtures.mock_data import (
    MOCK_MARINE_RESPONSE,
    MOCK_WEATHER_RESPONSE,
    SAMPLE_INPUT_DICT,
)
from tools import ToolRegistry


class TestGoldenAssessment(unittest.TestCase):
    def test_canonical_assessed_operational_assessment_step(self):
        """Exercises Step 4 of the Canonical Assessed Workflow: Marine Conditions step."""
        def mock_transport(url: str, timeout: float) -> Tuple[int, Dict[str, Any]]:
            if "marine" in url:
                return 200, MOCK_MARINE_RESPONSE
            return 200, MOCK_WEATHER_RESPONSE

        registry = ToolRegistry(transport=mock_transport)
        agent = MarineConditionsIntelligenceAgent(tool_registry=registry)

        assessment_input = MarineConditionsAgentInput(
            workflow_id="wf-canonical-assessment-001",
            step_id="step-02-marine-context",
            objective="Assess the selected snorkeling operation for the requested period and recommend whether the operation should continue, continue with caution, or require a restrictive operational action.",
            location=LocationDto(latitude=6.0535, longitude=80.2210),
            activity="snorkeling",
            period=TimePeriodDto(start_time="2026-10-13T06:00:00Z", end_time="2026-10-13T12:00:00Z"),
            required_factors=["wind_speed", "wave_height", "swell_wave_height", "precipitation", "weather_code"],
        )

        report = agent.execute(assessment_input)

        # Assertions for Section 40 compliance:
        self.assertEqual(report.workflow_id, "wf-canonical-assessment-001")
        self.assertEqual(report.step_id, "step-02-marine-context")
        self.assertEqual(report.status, ReportStatus.COMPLETE)
        self.assertEqual(report.activity, "snorkeling")

        # Grounded factual environmental evidence:
        self.assertEqual(report.environmental_factors["wind_speed"].value, 12.5)
        self.assertEqual(report.environmental_factors["wave_height"].value, 0.9)
        self.assertEqual(report.environmental_factors["swell_wave_height"].value, 0.6)
        self.assertEqual(report.environmental_factors["precipitation"].value, 0.0)

        # Provenance and Freshness:
        self.assertEqual(report.source_provenance.provider, "Open-Meteo")
        self.assertTrue(report.source_provenance.is_forecast)
        self.assertEqual(report.freshness.status.value, "FRESH")

        # Non-authority assurance:
        self.assertEqual(report.disclaimer, STANDARD_DISCLAIMER)
        # Ensure agent didn't prematurely decide operation outcome:
        self.assertNotIn("CONTINUE", report.objective_relevance_summary)
        self.assertNotIn("CANCEL", report.objective_relevance_summary)

    def test_tourist_recommendation_planning_support(self):
        """Exercises the tourist planning candidate constraint evaluation."""
        def mock_transport(url: str, timeout: float) -> Tuple[int, Dict[str, Any]]:
            if "marine" in url:
                return 200, MOCK_MARINE_RESPONSE
            return 200, MOCK_WEATHER_RESPONSE

        registry = ToolRegistry(transport=mock_transport)
        agent = MarineConditionsIntelligenceAgent(tool_registry=registry)

        planning_input = MarineConditionsAgentInput(
            workflow_id="wf-tourist-planning-42",
            step_id="step-01-surfing-context",
            objective="Retrieve environmental conditions for candidate surfing activity in Mirissa.",
            location=LocationDto(latitude=5.9482, longitude=80.4578),
            activity="surfing",
            period=TimePeriodDto(start_time="2026-10-13T08:00:00Z", end_time="2026-10-13T14:00:00Z"),
            required_factors=["wave_height", "swell_wave_height", "wind_speed"],
        )

        report = agent.execute(planning_input)
        self.assertEqual(report.status, ReportStatus.COMPLETE)
        self.assertEqual(report.activity, "surfing")
        self.assertIn("wave_height", report.environmental_factors)
        self.assertIn("swell_wave_height", report.environmental_factors)

    def test_provider_outage_safe_failure_evaluation(self):
        """Evaluates fail-closed behavior on external provider outage: no fabricated values."""
        def outage_transport(url: str, timeout: float) -> Tuple[int, Dict[str, Any]]:
            # Simulate 503 Service Unavailable on both endpoints
            return 503, {}

        registry = ToolRegistry(transport=outage_transport)
        agent = MarineConditionsIntelligenceAgent(tool_registry=registry)

        assessment_input = MarineConditionsAgentInput(
            workflow_id="wf-outage-001",
            step_id="step-02-marine-context",
            objective="Assess conditions for diving during storm.",
            location=LocationDto(latitude=6.0535, longitude=80.2210),
            activity="scuba_diving",
            period=TimePeriodDto(start_time="2026-10-13T06:00:00Z", end_time="2026-10-13T12:00:00Z"),
            required_factors=["wind_speed", "wave_height"],
        )

        report = agent.execute(assessment_input)

        # Must cleanly fail with SOURCE_UNAVAILABLE and zero fabricated factors
        self.assertEqual(report.status, ReportStatus.SOURCE_UNAVAILABLE)
        self.assertEqual(len(report.environmental_factors), 0)
        self.assertIn("wind_speed", report.missing_or_unavailable_factors)
        self.assertTrue(report.error_detail and ("503" in report.error_detail or "outage" in report.error_detail.lower()))


if __name__ == "__main__":
    unittest.main()

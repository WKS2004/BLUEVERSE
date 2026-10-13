"""Unit tests for schemas and DTOs."""

import unittest
from datetime import datetime, timezone

from schemas import (
    DEFAULT_ALLOWED_TOOLS,
    DEFAULT_REQUIRED_FACTORS,
    EnvironmentalFactor,
    FreshnessAssessment,
    FreshnessStatus,
    LocationDto,
    MarineConditionsAgentInput,
    MarineConditionsReport,
    ReportStatus,
    SourceProvenance,
    STANDARD_DISCLAIMER,
    TimePeriodDto,
    ToolPolicyDto,
    format_iso_datetime,
    parse_iso_datetime,
)
from tests.fixtures.mock_data import SAMPLE_INPUT_DICT


class TestSchemas(unittest.TestCase):
    def test_location_dto_valid(self):
        loc = LocationDto(latitude=6.0535, longitude=80.2210)
        self.assertEqual(loc.latitude, 6.0535)
        self.assertEqual(loc.longitude, 80.2210)
        d = loc.to_dict()
        self.assertEqual(d["latitude"], 6.0535)
        self.assertEqual(d["longitude"], 80.2210)

        loc2 = LocationDto.from_dict({"latitude": "6.0535", "longitude": "80.221"})
        self.assertEqual(loc2.latitude, 6.0535)

    def test_location_dto_out_of_bounds(self):
        with self.assertRaises(ValueError):
            LocationDto(latitude=91.0, longitude=0.0)
        with self.assertRaises(ValueError):
            LocationDto(latitude=-90.1, longitude=0.0)
        with self.assertRaises(ValueError):
            LocationDto(latitude=0.0, longitude=180.1)
        with self.assertRaises(ValueError):
            LocationDto(latitude=0.0, longitude=-180.1)

    def test_time_period_dto_valid(self):
        p = TimePeriodDto(start_time="2026-10-13T06:00:00Z", end_time="2026-10-13T12:00:00Z")
        self.assertEqual(p.start_time, "2026-10-13T06:00:00Z")
        self.assertEqual(p.end_time, "2026-10-13T12:00:00Z")

    def test_time_period_dto_end_before_start(self):
        with self.assertRaises(ValueError):
            TimePeriodDto(start_time="2026-10-13T12:00:00Z", end_time="2026-10-13T06:00:00Z")

    def test_time_period_dto_invalid_timestamp(self):
        with self.assertRaises(ValueError):
            TimePeriodDto(start_time="not-a-date", end_time="2026-10-13T12:00:00Z")

    def test_tool_policy_dto_defaults(self):
        policy = ToolPolicyDto()
        self.assertEqual(policy.allowed_tools, DEFAULT_ALLOWED_TOOLS)
        self.assertEqual(policy.max_tool_calls, 5)
        self.assertEqual(policy.timeout_seconds, 10.0)

    def test_agent_input_roundtrip(self):
        agent_input = MarineConditionsAgentInput.from_dict(SAMPLE_INPUT_DICT)
        self.assertEqual(agent_input.workflow_id, SAMPLE_INPUT_DICT["workflow_id"])
        self.assertEqual(agent_input.step_id, SAMPLE_INPUT_DICT["step_id"])
        self.assertEqual(agent_input.activity, "snorkeling")
        self.assertEqual(agent_input.location.latitude, 6.0535)
        self.assertEqual(agent_input.location.longitude, 80.2210)

        d = agent_input.to_dict()
        self.assertEqual(d["workflow_id"], SAMPLE_INPUT_DICT["workflow_id"])
        self.assertEqual(d["required_factors"], DEFAULT_REQUIRED_FACTORS)

    def test_agent_input_missing_required_fields(self):
        invalid_dict = dict(SAMPLE_INPUT_DICT)
        del invalid_dict["objective"]
        with self.assertRaises(ValueError):
            MarineConditionsAgentInput.from_dict(invalid_dict)

    def test_environmental_factor_zero_vs_none(self):
        # A measured 0.0 is distinct from None
        f_zero = EnvironmentalFactor(value=0.0, unit="mm")
        self.assertEqual(f_zero.value, 0.0)
        self.assertEqual(f_zero.to_dict()["value"], 0.0)

        f_none = EnvironmentalFactor(value=None, unit="mm")
        self.assertIsNone(f_none.value)
        self.assertIsNone(f_none.to_dict()["value"])

    def test_marine_conditions_report_roundtrip(self):
        report = MarineConditionsReport(
            workflow_id="wf-1",
            step_id="step-1",
            status=ReportStatus.COMPLETE,
            location=LocationDto(latitude=6.05, longitude=80.22),
            activity="snorkeling",
            requested_period=TimePeriodDto(start_time="2026-10-13T06:00:00Z", end_time="2026-10-13T12:00:00Z"),
            source_provenance=SourceProvenance(provider="Open-Meteo", retrieval_time="2026-10-13T06:01:00Z", is_forecast=True),
            freshness=FreshnessAssessment(status=FreshnessStatus.FRESH, rule="MaxAge: 60", age_minutes=5.0),
            environmental_factors={
                "wind_speed": EnvironmentalFactor(value=12.5, unit="km/h"),
                "wave_height": EnvironmentalFactor(value=0.8, unit="m"),
            },
            missing_or_unavailable_factors=[],
            objective_relevance_summary="Conditions are calm for snorkeling.",
            uncertainty_and_limitations="Model grid 0.25 deg.",
            disclaimer=STANDARD_DISCLAIMER,
        )

        d = report.to_dict()
        self.assertEqual(d["status"], "COMPLETE")
        self.assertEqual(d["environmental_factors"]["wind_speed"]["value"], 12.5)

        restored = MarineConditionsReport.from_dict(d)
        self.assertEqual(restored.workflow_id, "wf-1")
        self.assertEqual(restored.status, ReportStatus.COMPLETE)
        self.assertEqual(restored.environmental_factors["wave_height"].value, 0.8)
        self.assertEqual(restored.disclaimer, STANDARD_DISCLAIMER)


if __name__ == "__main__":
    unittest.main()

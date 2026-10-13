"""Safety tests for deterministic validation boundaries and non-authority constraints."""

import unittest

from safety import (
    DeterministicSafetyValidator,
    SafetyViolationError,
)
from schemas import (
    EnvironmentalFactor,
    FreshnessAssessment,
    FreshnessStatus,
    LocationDto,
    MarineConditionsReport,
    ReportStatus,
    SourceProvenance,
    STANDARD_DISCLAIMER,
    TimePeriodDto,
)


class TestDeterministicBoundaries(unittest.TestCase):
    def _create_valid_report(self) -> MarineConditionsReport:
        return MarineConditionsReport(
            workflow_id="wf-val-1",
            step_id="step-1",
            status=ReportStatus.COMPLETE,
            location=LocationDto(latitude=6.05, longitude=80.22),
            activity="snorkeling",
            requested_period=TimePeriodDto(start_time="2026-10-13T06:00:00Z", end_time="2026-10-13T12:00:00Z"),
            source_provenance=SourceProvenance(provider="Open-Meteo", retrieval_time="2026-10-13T06:01:00Z", is_forecast=True),
            freshness=FreshnessAssessment(status=FreshnessStatus.FRESH, rule="MaxAge: 60", age_minutes=5.0),
            environmental_factors={
                "wind_speed": EnvironmentalFactor(value=12.5, unit="km/h"),
                "wave_height": EnvironmentalFactor(value=0.9, unit="m"),
            },
            missing_or_unavailable_factors=[],
            objective_relevance_summary="Observed conditions for snorkeling: wind speed is 12.5 km/h, wave height is 0.9 m.",
            uncertainty_and_limitations="Model resolution 0.25 deg.",
            disclaimer=STANDARD_DISCLAIMER,
        )

    def test_valid_report_passes_deterministic_validation(self):
        report = self._create_valid_report()
        tool_evidence = {
            "wind_speed": {"value": 12.5, "unit": "km/h"},
            "wave_height": {"value": 0.9, "unit": "m"},
        }
        # Should not raise
        DeterministicSafetyValidator.validate_report(report, tool_evidence)

    def test_missing_disclaimer_rejected(self):
        report = self._create_valid_report()
        report.disclaimer = ""  # stripped disclaimer
        tool_evidence = {
            "wind_speed": {"value": 12.5, "unit": "km/h"},
            "wave_height": {"value": 0.9, "unit": "m"},
        }
        with self.assertRaises(SafetyViolationError) as cm:
            DeterministicSafetyValidator.validate_report(report, tool_evidence)
        self.assertIn("navigation disclaimer", str(cm.exception))

    def test_unauthorized_operational_approval_rejected(self):
        report = self._create_valid_report()
        report.objective_relevance_summary = "Conditions are good and the operation is hereby approved to continue."
        tool_evidence = {
            "wind_speed": {"value": 12.5, "unit": "km/h"},
            "wave_height": {"value": 0.9, "unit": "m"},
        }
        with self.assertRaises(SafetyViolationError) as cm:
            DeterministicSafetyValidator.validate_report(report, tool_evidence)
        self.assertIn("unauthorized operational instruction", str(cm.exception))

    def test_hallucinated_factor_rejected(self):
        report = self._create_valid_report()
        # Add a factor not in evidence
        report.environmental_factors["water_visibility"] = EnvironmentalFactor(value=15.0, unit="m")
        tool_evidence = {
            "wind_speed": {"value": 12.5, "unit": "km/h"},
            "wave_height": {"value": 0.9, "unit": "m"},
        }
        with self.assertRaises(SafetyViolationError) as cm:
            DeterministicSafetyValidator.validate_report(report, tool_evidence)
        self.assertIn("Hallucination detected", str(cm.exception))

    def test_falsified_factor_value_rejected(self):
        report = self._create_valid_report()
        # Evidence says 0.9, but report was modified to 3.5
        report.environmental_factors["wave_height"].value = 3.5
        tool_evidence = {
            "wind_speed": {"value": 12.5, "unit": "km/h"},
            "wave_height": {"value": 0.9, "unit": "m"},
        }
        with self.assertRaises(SafetyViolationError) as cm:
            DeterministicSafetyValidator.validate_report(report, tool_evidence)
        self.assertIn("Falsification detected", str(cm.exception))


if __name__ == "__main__":
    unittest.main()

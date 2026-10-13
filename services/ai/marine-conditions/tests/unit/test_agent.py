"""Unit tests for Marine Conditions Intelligence Agent core workflow."""

import unittest
from typing import Any, Dict, Tuple

from agent import MarineConditionsIntelligenceAgent
from schemas import (
    MarineConditionsAgentInput,
    ReportStatus,
    STANDARD_DISCLAIMER,
)
from tests.fixtures.mock_data import (
    MOCK_MARINE_RESPONSE,
    MOCK_WEATHER_RESPONSE,
    MOCK_WEATHER_RESPONSE_WITH_MISSING,
    SAMPLE_INPUT_DICT,
)
from tools import ToolRegistry


class TestAgent(unittest.TestCase):
    def test_agent_execution_complete(self):
        def mock_transport(url: str, timeout: float) -> Tuple[int, Dict[str, Any]]:
            if "marine" in url:
                return 200, MOCK_MARINE_RESPONSE
            return 200, MOCK_WEATHER_RESPONSE

        registry = ToolRegistry(transport=mock_transport)
        agent = MarineConditionsIntelligenceAgent(tool_registry=registry)

        input_data = MarineConditionsAgentInput.from_dict(SAMPLE_INPUT_DICT)
        report = agent.execute(input_data)

        self.assertEqual(report.workflow_id, input_data.workflow_id)
        self.assertEqual(report.step_id, input_data.step_id)
        self.assertEqual(report.status, ReportStatus.COMPLETE)
        self.assertEqual(report.activity, "snorkeling")
        self.assertEqual(report.disclaimer, STANDARD_DISCLAIMER)

        # Environmental factors check
        self.assertIn("wind_speed", report.environmental_factors)
        self.assertEqual(report.environmental_factors["wind_speed"].value, 12.5)
        self.assertIn("wave_height", report.environmental_factors)
        self.assertEqual(report.environmental_factors["wave_height"].value, 0.9)
        self.assertIn("precipitation", report.environmental_factors)
        self.assertEqual(report.environmental_factors["precipitation"].value, 0.0)

        # Freshness and provenance
        self.assertEqual(report.freshness.status.value, "FRESH")
        self.assertEqual(report.source_provenance.provider, "Open-Meteo")

        # Summary text checks
        self.assertIn("snorkeling", report.objective_relevance_summary)
        self.assertIn("wind speed is 12.5 km/h", report.objective_relevance_summary)

    def test_agent_execution_with_missing_factors(self):
        def mock_transport(url: str, timeout: float) -> Tuple[int, Dict[str, Any]]:
            if "marine" in url:
                return 200, MOCK_MARINE_RESPONSE
            return 200, MOCK_WEATHER_RESPONSE_WITH_MISSING

        registry = ToolRegistry(transport=mock_transport)
        agent = MarineConditionsIntelligenceAgent(tool_registry=registry)

        input_data = MarineConditionsAgentInput.from_dict(SAMPLE_INPUT_DICT)
        report = agent.execute(input_data)

        self.assertEqual(report.status, ReportStatus.INCOMPLETE)
        self.assertIn("precipitation", report.missing_or_unavailable_factors)
        self.assertIn("weather_code", report.missing_or_unavailable_factors)


    def test_agent_with_gemini_model_generation(self):
        def mock_transport(url: str, timeout: float) -> Tuple[int, Dict[str, Any]]:
            if "marine" in url:
                return 200, MOCK_MARINE_RESPONSE
            return 200, MOCK_WEATHER_RESPONSE

        mock_gemini_response = {
            "candidates": [
                {
                    "content": {
                        "parts": [
                            {
                                "text": '{"objective_relevance_summary": "Gemini synthesis: calm conditions with 0.9m waves suitable for observation.", "uncertainty_and_limitations": "Model forecast grid 0.25 deg."}'
                            }
                        ]
                    }
                }
            ]
        }

        def mock_gemini_transport(url: str, payload: Dict[str, Any], timeout: float) -> Tuple[int, Dict[str, Any]]:
            return 200, mock_gemini_response

        from gemini import GeminiClient
        gemini = GeminiClient(api_key="valid-gemini-key", transport=mock_gemini_transport)
        registry = ToolRegistry(transport=mock_transport)
        agent = MarineConditionsIntelligenceAgent(tool_registry=registry, gemini_client=gemini)

        input_data = MarineConditionsAgentInput.from_dict(SAMPLE_INPUT_DICT)
        report = agent.execute(input_data)

        self.assertEqual(report.status, ReportStatus.COMPLETE)
        self.assertIn("Gemini synthesis", report.objective_relevance_summary)
        self.assertIn("Model forecast grid", report.uncertainty_and_limitations)

    def test_agent_with_gemini_safety_violation_caught(self):
        def mock_transport(url: str, timeout: float) -> Tuple[int, Dict[str, Any]]:
            if "marine" in url:
                return 200, MOCK_MARINE_RESPONSE
            return 200, MOCK_WEATHER_RESPONSE

        malicious_gemini_response = {
            "candidates": [
                {
                    "content": {
                        "parts": [
                            {
                                "text": '{"objective_relevance_summary": "Conditions are calm and operation is hereby approved.", "uncertainty_and_limitations": "None."}'
                            }
                        ]
                    }
                }
            ]
        }

        def mock_gemini_transport(url: str, payload: Dict[str, Any], timeout: float) -> Tuple[int, Dict[str, Any]]:
            return 200, malicious_gemini_response

        from gemini import GeminiClient
        from safety import SafetyViolationError
        gemini = GeminiClient(api_key="valid-gemini-key", transport=mock_gemini_transport)
        registry = ToolRegistry(transport=mock_transport)
        agent = MarineConditionsIntelligenceAgent(tool_registry=registry, gemini_client=gemini)

        input_data = MarineConditionsAgentInput.from_dict(SAMPLE_INPUT_DICT)
        with self.assertRaises(SafetyViolationError) as cm:
            agent.execute(input_data)
        self.assertIn("unauthorized operational instruction", str(cm.exception))


if __name__ == "__main__":
    unittest.main()

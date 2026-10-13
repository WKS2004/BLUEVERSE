"""Unit tests for GeminiClient and Google AI Studio structured output generation."""

import json
import unittest
from typing import Any, Dict, Tuple
from unittest.mock import Mock, patch
from urllib.parse import urlsplit

from gemini import GeminiClient


class TestGeminiClient(unittest.TestCase):
    def test_gemini_missing_api_key_graceful_fallback(self):
        client = GeminiClient(api_key=None)
        self.assertFalse(client.is_configured)

        relevance, limitations, err = client.generate_conditions_summary(
            sanitized_objective="Assess conditions for diving",
            activity="scuba_diving",
            factors={"wave_height": {"value": 0.8, "unit": "m"}},
            missing=[],
        )
        self.assertIsNone(relevance)
        self.assertIsNone(limitations)
        self.assertEqual(err, "API_KEY_NOT_CONFIGURED")

    def test_gemini_successful_structured_generation(self):
        mock_gemini_response = {
            "candidates": [
                {
                    "content": {
                        "parts": [
                            {
                                "text": json.dumps(
                                    {
                                        "objective_relevance_summary": "Conditions in Mirissa show calm 0.8m waves favorable for diving.",
                                        "uncertainty_and_limitations": "Forecast model provides 0.25-degree resolution.",
                                    }
                                )
                            }
                        ]
                    }
                }
            ]
        }

        response = Mock(status=200)
        response.read.return_value = json.dumps(mock_gemini_response).encode("utf-8")
        response.__enter__ = Mock(return_value=response)
        response.__exit__ = Mock(return_value=False)

        with patch("urllib.request.urlopen", return_value=response) as urlopen:
            client = GeminiClient(api_key="test-api-key", model="gemini-3.6-flash")
            self.assertTrue(client.is_configured)

            relevance, limitations, err = client.generate_conditions_summary(
                sanitized_objective="Assess conditions for diving",
                activity="scuba_diving",
                factors={"wave_height": {"value": 0.8, "unit": "m"}},
                missing=[],
            )

        request = urlopen.call_args.args[0]
        parsed_url = urlsplit(request.full_url)
        self.assertEqual(
            parsed_url.path,
            "/v1beta/models/gemini-3.6-flash:generateContent",
        )
        self.assertEqual(parsed_url.query, "")
        self.assertEqual(request.get_header("X-goog-api-key"), "test-api-key")
        self.assertEqual(
            json.loads(request.data.decode("utf-8"))["generationConfig"]["responseMimeType"],
            "application/json",
        )

        self.assertIsNone(err)
        self.assertIn("calm 0.8m waves favorable for diving", relevance)
        self.assertIn("0.25-degree resolution", limitations)

    def test_gemini_rate_limit_429_graceful_fallback(self):
        def rate_limited_transport(url: str, payload: Dict[str, Any], timeout: float) -> Tuple[int, Dict[str, Any]]:
            # Simulate Google AI Studio free tier rate limit
            return 429, {"error": {"message": "Resource has been exhausted (e.g. check quota)."}}

        client = GeminiClient(api_key="test-api-key", transport=rate_limited_transport)
        relevance, limitations, err = client.generate_conditions_summary(
            sanitized_objective="Assess conditions for diving",
            activity="scuba_diving",
            factors={},
            missing=[],
        )

        self.assertIsNone(relevance)
        self.assertIsNone(limitations)
        self.assertEqual(err, "RATE_LIMIT_EXCEEDED")

    def test_gemini_malformed_json_fallback(self):
        malformed_response = {
            "candidates": [
                {
                    "content": {
                        "parts": [
                            {"text": "This is plain text not valid JSON"}
                        ]
                    }
                }
            ]
        }

        def bad_json_transport(url: str, payload: Dict[str, Any], timeout: float) -> Tuple[int, Dict[str, Any]]:
            return 200, malformed_response

        client = GeminiClient(api_key="test-api-key", transport=bad_json_transport)
        relevance, limitations, err = client.generate_conditions_summary(
            sanitized_objective="Assess conditions for diving",
            activity="scuba_diving",
            factors={},
            missing=[],
        )

        self.assertIsNone(relevance)
        self.assertIsNone(limitations)
        self.assertEqual(err, "MALFORMED_JSON_OUTPUT")

    def test_gemini_empty_candidates_fallback(self):
        empty_response = {"candidates": []}

        def empty_transport(url: str, payload: Dict[str, Any], timeout: float) -> Tuple[int, Dict[str, Any]]:
            return 200, empty_response

        client = GeminiClient(api_key="test-api-key", transport=empty_transport)
        relevance, limitations, err = client.generate_conditions_summary(
            sanitized_objective="Assess conditions",
            activity="surfing",
            factors={},
            missing=[],
        )

        self.assertIsNone(relevance)
        self.assertEqual(err, "EMPTY_CANDIDATES")


if __name__ == "__main__":
    unittest.main()

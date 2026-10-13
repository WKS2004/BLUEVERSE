"""Regression coverage for Marine's isolated Gemini credentials and transport."""

import json
import os
import unittest
from unittest.mock import Mock, patch

from gemini import DEFAULT_GEMINI_MODEL, GeminiClient


class TestGeminiConfiguration(unittest.TestCase):
    def test_uses_marine_key_and_model_without_generic_key_fallback(self):
        environment = {
            "GEMINI_API_KEY": "shared-key-must-not-be-used",
            "GEMINI_MODEL": "generic-model-must-not-be-used",
            "MARINE_CONDITIONS_GEMINI_API_KEY": "marine-key",
            "MARINE_CONDITIONS_GEMINI_MODEL": "marine-model",
            "MARINE_CONDITIONS_GEMINI_ENABLED": "true",
        }
        with patch("gemini._load_env_if_present"), patch.dict(os.environ, environment, clear=True):
            client = GeminiClient()

        self.assertTrue(client.is_configured)
        self.assertEqual(client.api_key, "marine-key")
        self.assertEqual(client.model, "marine-model")

        with patch("gemini._load_env_if_present"), patch.dict(
            os.environ,
            {"GEMINI_API_KEY": "shared-key-must-not-be-used"},
            clear=True,
        ):
            unconfigured_client = GeminiClient()

        self.assertFalse(unconfigured_client.is_configured)
        self.assertEqual(unconfigured_client.model, DEFAULT_GEMINI_MODEL)

    def test_environment_key_is_disabled_without_explicit_opt_in(self):
        with patch("gemini._load_env_if_present"), patch.dict(
            os.environ,
            {"MARINE_CONDITIONS_GEMINI_API_KEY": "marine-key"},
            clear=True,
        ):
            client = GeminiClient()

        self.assertFalse(client.is_configured)

    def test_rejects_non_google_or_non_https_base_urls(self):
        invalid_base_urls = (
            "http://generativelanguage.googleapis.com/v1beta",
            "https://example.com/v1beta",
            "https://generativelanguage.googleapis.com/v1beta?key=secret",
            "https://user@generativelanguage.googleapis.com/v1beta",
            "https://generativelanguage.googleapis.com:443/v1beta",
            "https://generativelanguage.googleapis.com/v1beta/models",
        )
        for invalid_base_url in invalid_base_urls:
            with self.subTest(base_url=invalid_base_url), self.assertRaises(ValueError):
                GeminiClient(api_key="test-key", base_url=invalid_base_url)

    def test_accepts_only_google_https_v1beta_base_url(self):
        client = GeminiClient(
            api_key="test-key",
            base_url="https://generativelanguage.googleapis.com/v1beta/",
        )

        self.assertEqual(
            client.base_url,
            "https://generativelanguage.googleapis.com/v1beta",
        )

    def test_sends_key_in_header_and_never_in_url(self):
        response_body = {
            "candidates": [
                {
                    "content": {
                        "parts": [
                            {
                                "text": json.dumps(
                                    {
                                        "objective_relevance_summary": "Evidence summary.",
                                        "uncertainty_and_limitations": "Forecast uncertainty remains.",
                                    }
                                )
                            }
                        ]
                    }
                }
            ]
        }
        response = Mock(status=200)
        response.read.return_value = json.dumps(response_body).encode("utf-8")
        response.__enter__ = Mock(return_value=response)
        response.__exit__ = Mock(return_value=False)

        with patch("gemini._load_env_if_present"), patch("gemini.urllib.request.urlopen", return_value=response) as urlopen:
            client = GeminiClient(api_key="marine-secret", model="gemini-3.6-flash")
            relevance, _, error = client.generate_conditions_summary(
                sanitized_objective="Assess conditions.",
                activity="snorkeling",
                factors={},
                missing=[],
            )

        self.assertIsNone(error)
        self.assertEqual(relevance, "Evidence summary.")
        request = urlopen.call_args.args[0]
        self.assertNotIn("key=", request.full_url)
        self.assertEqual(request.get_header("X-goog-api-key"), "marine-secret")


if __name__ == "__main__":
    unittest.main()

"""Unit tests for allowlisted read tools."""

import unittest
from typing import Any, Dict, Tuple

from audit import audit_logger
from tests.fixtures.mock_data import (
    MOCK_MARINE_RESPONSE,
    MOCK_WEATHER_RESPONSE,
    MOCK_WEATHER_RESPONSE_WITH_MISSING,
)
from tools import (
    ToolNotAllowedError,
    ToolProviderError,
    ToolRegistry,
    ToolValidationError,
)


class TestTools(unittest.TestCase):
    def setUp(self):
        audit_logger.clear()

    def test_unauthorized_tool_blocked(self):
        registry = ToolRegistry()
        with self.assertRaises(ToolNotAllowedError):
            registry.execute_tool(
                tool_name="unauthorized_system_tool",
                arguments={"foo": "bar"},
                workflow_id="wf-test-1",
                step_id="step-1",
                allowed_tools=["weather_forecast_lookup"],
            )

        records = audit_logger.get_records("wf-test-1")
        self.assertEqual(len(records), 1)
        self.assertEqual(records[0].status, "BLOCKED")
        self.assertEqual(records[0].tool_name, "unauthorized_system_tool")

    def test_weather_forecast_lookup_success_and_zero_preserved(self):
        def mock_transport(url: str, timeout: float) -> Tuple[int, Dict[str, Any]]:
            self.assertIn("latitude=6.0535", url)
            self.assertIn("longitude=80.221", url)
            return 200, MOCK_WEATHER_RESPONSE

        registry = ToolRegistry(transport=mock_transport)
        res = registry.execute_tool(
            tool_name="weather_forecast_lookup",
            arguments={"latitude": 6.0535, "longitude": 80.2210, "time": "2026-10-13T06:00:00Z"},
            workflow_id="wf-test-2",
            step_id="step-1",
        )

        self.assertTrue(res.success)
        self.assertIn("wind_speed", res.data)
        self.assertEqual(res.data["wind_speed"]["value"], 12.5)

        # Critical requirement: zero precipitation must be preserved as 0.0, NOT missing
        self.assertIn("precipitation", res.data)
        self.assertEqual(res.data["precipitation"]["value"], 0.0)
        self.assertNotIn("precipitation", res.missing_factors)

        self.assertIn("weather_code", res.data)
        self.assertEqual(res.data["weather_code"]["value"], 1)

    def test_weather_forecast_lookup_missing_factor_identification(self):
        def mock_transport(url: str, timeout: float) -> Tuple[int, Dict[str, Any]]:
            return 200, MOCK_WEATHER_RESPONSE_WITH_MISSING

        registry = ToolRegistry(transport=mock_transport)
        res = registry.execute_tool(
            tool_name="weather_forecast_lookup",
            arguments={"latitude": 6.0535, "longitude": 80.2210},
            workflow_id="wf-test-3",
            step_id="step-1",
        )

        self.assertTrue(res.success)
        self.assertIn("wind_speed", res.data)
        # precipitation was None in response -> must be in missing_factors
        self.assertIn("precipitation", res.missing_factors)
        self.assertIn("weather_code", res.missing_factors)

    def test_marine_forecast_lookup_success(self):
        def mock_transport(url: str, timeout: float) -> Tuple[int, Dict[str, Any]]:
            return 200, MOCK_MARINE_RESPONSE

        registry = ToolRegistry(transport=mock_transport)
        res = registry.execute_tool(
            tool_name="marine_forecast_lookup",
            arguments={"latitude": 6.0535, "longitude": 80.2210, "time": "2026-10-13T06:00:00Z"},
            workflow_id="wf-test-4",
            step_id="step-1",
        )

        self.assertTrue(res.success)
        self.assertIn("wave_height", res.data)
        self.assertEqual(res.data["wave_height"]["value"], 0.9)
        self.assertIn("swell_wave_height", res.data)
        self.assertEqual(res.data["swell_wave_height"]["value"], 0.6)

    def test_condition_snapshot_lookup(self):
        registry = ToolRegistry()
        registry.save_snapshot("snap-123", {"wind_speed": {"value": 15.0, "unit": "km/h"}}, missing_factors=[])

        # Found snapshot
        res = registry.execute_tool(
            tool_name="condition_snapshot_lookup",
            arguments={"snapshot_id": "snap-123"},
            workflow_id="wf-test-5",
            step_id="step-1",
        )
        self.assertTrue(res.success)
        self.assertEqual(res.data["wind_speed"]["value"], 15.0)

        # Missing snapshot
        res_missing = registry.execute_tool(
            tool_name="condition_snapshot_lookup",
            arguments={"snapshot_id": "snap-nonexistent"},
            workflow_id="wf-test-5",
            step_id="step-1",
        )
        self.assertFalse(res_missing.success)
        self.assertEqual(res_missing.error_category, "RESOURCE_NOT_FOUND")

    def test_coordinate_validation(self):
        registry = ToolRegistry()
        with self.assertRaises(ToolValidationError):
            registry.execute_tool(
                tool_name="weather_forecast_lookup",
                arguments={"latitude": "invalid", "longitude": 80.0},
                workflow_id="wf-test-6",
                step_id="step-1",
            )

        with self.assertRaises(ToolValidationError):
            registry.execute_tool(
                tool_name="weather_forecast_lookup",
                arguments={"latitude": 95.0, "longitude": 80.0},
                workflow_id="wf-test-6",
                step_id="step-1",
            )

    def test_bounded_retry_on_provider_error(self):
        call_count = 0

        def flaky_transport(url: str, timeout: float) -> Tuple[int, Dict[str, Any]]:
            nonlocal call_count
            call_count += 1
            if call_count == 1:
                return 503, {}
            return 200, MOCK_WEATHER_RESPONSE

        registry = ToolRegistry(transport=flaky_transport)
        res = registry.execute_tool(
            tool_name="weather_forecast_lookup",
            arguments={"latitude": 6.0535, "longitude": 80.2210},
            workflow_id="wf-test-7",
            step_id="step-1",
            max_retries=1,
        )

        self.assertEqual(call_count, 2)
        self.assertTrue(res.success)
        records = audit_logger.get_records("wf-test-7")
        self.assertEqual(len(records), 1)
        self.assertEqual(records[0].retry_count, 1)


if __name__ == "__main__":
    unittest.main()

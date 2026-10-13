"""Unit tests for AuditLogger and credential redaction."""

from datetime import datetime, timezone
import unittest

from audit import AuditLogger, ToolAuditRecord


class TestAuditLogger(unittest.TestCase):
    def setUp(self):
        self.logger = AuditLogger()

    def test_log_tool_invocation_and_retrieval(self):
        t1 = datetime(2026, 10, 13, 6, 0, 0, tzinfo=timezone.utc)
        t2 = datetime(2026, 10, 13, 6, 0, 1, tzinfo=timezone.utc)

        record = self.logger.log_tool_invocation(
            workflow_id="wf-100",
            step_id="step-1",
            tool_name="weather_forecast_lookup",
            start_time=t1,
            end_time=t2,
            status="SUCCESS",
            retry_count=0,
            parameters_summary={"latitude": 6.05, "longitude": 80.22},
            result_summary={"wind_speed": 12.5},
        )

        self.assertEqual(record.workflow_id, "wf-100")
        self.assertEqual(record.status, "SUCCESS")
        self.assertAlmostEqual(record.duration_ms, 1000.0, delta=10.0)

        # Retrieve records
        records_wf = self.logger.get_records("wf-100")
        self.assertEqual(len(records_wf), 1)
        self.assertEqual(records_wf[0].tool_name, "weather_forecast_lookup")

        # Retrieve for non-existent workflow
        records_none = self.logger.get_records("wf-999")
        self.assertEqual(len(records_none), 0)

    def test_credential_and_secret_redaction(self):
        t1 = datetime(2026, 10, 13, 6, 0, 0, tzinfo=timezone.utc)
        t2 = datetime(2026, 10, 13, 6, 0, 1, tzinfo=timezone.utc)

        sensitive_params = {
            "latitude": 6.05,
            "api_key": "secret-key-12345",
            "bearer_token": "ey12345678",
            "auth": {"password": "supersecretpassword"},
            "reasoning": "hidden thoughts of the model",
        }

        record = self.logger.log_tool_invocation(
            workflow_id="wf-sec-1",
            step_id="step-sec",
            tool_name="weather_forecast_lookup",
            start_time=t1,
            end_time=t2,
            status="SUCCESS",
            parameters_summary=sensitive_params,
        )

        params = record.parameters_summary
        self.assertEqual(params["latitude"], 6.05)
        self.assertEqual(params["api_key"], "[REDACTED]")
        self.assertEqual(params["bearer_token"], "[REDACTED]")
        self.assertEqual(params["auth"]["password"], "[REDACTED]")
        self.assertEqual(params["reasoning"], "[REDACTED]")

    def test_clear_audit_records(self):
        t1 = datetime(2026, 10, 13, 6, 0, 0, tzinfo=timezone.utc)
        t2 = datetime(2026, 10, 13, 6, 0, 1, tzinfo=timezone.utc)

        self.logger.log_tool_invocation(
            workflow_id="wf-test",
            step_id="step-1",
            tool_name="test_tool",
            start_time=t1,
            end_time=t2,
            status="SUCCESS",
        )
        self.assertEqual(len(self.logger.get_records()), 1)

        self.logger.clear()
        self.assertEqual(len(self.logger.get_records()), 0)


if __name__ == "__main__":
    unittest.main()

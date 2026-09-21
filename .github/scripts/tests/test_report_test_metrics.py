from __future__ import annotations

import contextlib
import importlib.util
import io
import sys
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch


SCRIPT_PATH = Path(__file__).resolve().parents[1] / "report_test_metrics.py"
SPEC = importlib.util.spec_from_file_location("report_test_metrics", SCRIPT_PATH)
assert SPEC is not None and SPEC.loader is not None
REPORTER = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = REPORTER
SPEC.loader.exec_module(REPORTER)


TRX_CONTENT = """<?xml version="1.0" encoding="utf-8"?>
<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
  <Results>
    <UnitTestResult testId="1" testName="API_HEALTH_001" outcome="Passed" duration="00:00:00.2500000" />
    <UnitTestResult testId="2" testName="API_HEALTH_002" outcome="NotExecuted" duration="00:00:00.0100000" />
  </Results>
</TestRun>
"""


class ReportTestMetricsTests(unittest.TestCase):
    def run_report(self, *arguments: str) -> tuple[int, str, str]:
        stdout = io.StringIO()
        stderr = io.StringIO()
        with (
            patch.object(sys, "argv", [str(SCRIPT_PATH), *arguments]),
            contextlib.redirect_stdout(stdout),
            contextlib.redirect_stderr(stderr),
        ):
            exit_code = REPORTER.main()
        return exit_code, stdout.getvalue(), stderr.getvalue()

    def test_results_root_discovers_dotnet_trx_files_and_groups_by_service(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            results_root = Path(temporary_directory)
            result_file = results_root / "api" / "Blueverse.Api.Tests.trx"
            result_file.parent.mkdir()
            result_file.write_text(TRX_CONTENT, encoding="utf-8")

            exit_code, output, error = self.run_report(
                "--results-root",
                str(results_root),
                "--scope",
                "Backend Microservices",
                "--require-results",
            )

        self.assertEqual(0, exit_code)
        self.assertEqual("", error)
        self.assertIn("overall: total=2 completed=2 passed=1 failed=0 skipped=1", output)
        self.assertIn("service=api: total=2 completed=2 passed=1 failed=0 skipped=1", output)
        self.assertIn("duration=0.26s", output)

    def test_require_results_fails_when_no_test_case_file_is_available(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            exit_code, output, error = self.run_report(
                "--results-root",
                temporary_directory,
                "--scope",
                "Backend Microservices",
                "--require-results",
            )

        self.assertEqual(2, exit_code)
        self.assertIn("overall: total=0 completed=0", output)
        self.assertIn("expected parseable test results", error)


if __name__ == "__main__":
    unittest.main()

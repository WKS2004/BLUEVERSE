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
SPEC = importlib.util.spec_from_file_location("report_test_metrics_flutter_edges", SCRIPT_PATH)
assert SPEC is not None and SPEC.loader is not None
REPORTER = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = REPORTER
SPEC.loader.exec_module(REPORTER)


class FlutterMetricsEdgeCaseTests(unittest.TestCase):
    def run_report(self, result_file: Path) -> tuple[int, str, str]:
        stdout = io.StringIO()
        stderr = io.StringIO()
        with (
            patch.object(
                sys,
                "argv",
                [
                    str(SCRIPT_PATH),
                    "--input",
                    str(result_file),
                    "--format",
                    "flutter",
                    "--scope",
                    "Flutter Mobile",
                    "--require-results",
                ],
            ),
            contextlib.redirect_stdout(stdout),
            contextlib.redirect_stderr(stderr),
        ):
            exit_code = REPORTER.main()
        return exit_code, stdout.getvalue(), stderr.getvalue()

    def test_array_wrapped_events_and_empty_error_are_reported_without_crashing(self) -> None:
        content = "\n".join(
            [
                '[{"type":"testStart","test":{"id":7,"name":"MOBILE_EDGE_001","hidden":false}},'
                '{"type":"testDone","testDone":{"id":7,"result":"failed","error":"","hidden":false}}]',
            ]
        )
        with tempfile.TemporaryDirectory() as temporary_directory:
            result_file = Path(temporary_directory) / "flutter-machine.json"
            result_file.write_text(content, encoding="utf-8")

            exit_code, output, error = self.run_report(result_file)

        self.assertEqual(0, exit_code)
        self.assertEqual("", error)
        self.assertIn("overall: total=1 completed=1 passed=0 failed=1", output)
        self.assertIn("MOBILE_EDGE_001", output)
        self.assertNotIn("MOBILE_EDGE_001 —", output)


if __name__ == "__main__":
    unittest.main()

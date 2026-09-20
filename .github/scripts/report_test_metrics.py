#!/usr/bin/env python3
"""Print overall and per-scope test metrics for GitHub Actions.

The parser accepts JUnit XML, .NET TRX XML, and Flutter --machine JSON logs.
It writes a compact log view and appends a Markdown table to the GitHub step
summary when GITHUB_STEP_SUMMARY is available. Pass/fail values come from the
test runner's complete assertion result; this script never classifies a test
from an HTTP status code alone.
"""

from __future__ import annotations

import argparse
import json
import os
import re
import sys
import xml.etree.ElementTree as ET
from collections import defaultdict
from dataclasses import dataclass
from pathlib import Path


@dataclass
class Metrics:
    total: int = 0
    passed: int = 0
    failed: int = 0
    skipped: int = 0
    errors: int = 0
    duration_seconds: float = 0.0
    failed_cases: list[str] | None = None

    def __post_init__(self) -> None:
        if self.failed_cases is None:
            self.failed_cases = []

    @property
    def completed(self) -> int:
        return self.passed + self.failed + self.skipped + self.errors

    @property
    def not_run(self) -> int:
        return max(self.total - self.completed, 0)

    def add(self, other: "Metrics") -> None:
        self.total += other.total
        self.passed += other.passed
        self.failed += other.failed
        self.skipped += other.skipped
        self.errors += other.errors
        self.duration_seconds += other.duration_seconds
        self.failed_cases.extend(other.failed_cases)


def local_name(tag: str) -> str:
    return tag.rsplit("}", 1)[-1]


def parse_seconds(value: str | None) -> float:
    if not value:
        return 0.0
    try:
        return float(value)
    except ValueError:
        match = re.fullmatch(
            r"P(?:\d+D)?T(?:(\d+(?:\.\d+)?)H)?(?:(\d+(?:\.\d+)?)M)?(?:(\d+(?:\.\d+)?)S)?",
            value,
        )
        if not match:
            return 0.0
        hours, minutes, seconds = (float(part or 0) for part in match.groups())
        return hours * 3600 + minutes * 60 + seconds


def failure_detail(element: ET.Element) -> str:
    for child in element.iter():
        if local_name(child.tag) in {"Message", "failure", "error", "ErrorInfo"}:
            message = child.attrib.get("message") or " ".join((child.text or "").split())
            if message:
                return message.splitlines()[0][:240]
    return ""


def parse_xml(path: Path) -> Metrics:
    root = ET.parse(path).getroot()
    elements = list(root.iter())

    trx_results = [element for element in elements if local_name(element.tag) == "UnitTestResult"]
    if trx_results:
        metrics = Metrics()
        for result in trx_results:
            outcome = result.attrib.get("outcome", "").lower()
            metrics.total += 1
            if outcome in {"passed", "pass"}:
                metrics.passed += 1
            elif outcome in {"notexecuted", "skipped", "pending"}:
                metrics.skipped += 1
            elif outcome in {"error", "aborted", "timeout"}:
                metrics.errors += 1
            else:
                metrics.failed += 1
            if outcome not in {"passed", "pass", "notexecuted", "skipped", "pending"}:
                name = result.attrib.get("testName") or result.attrib.get("testId") or "unknown-test"
                detail = failure_detail(result)
                if detail:
                    name = f"{name} — {detail}"
                metrics.failed_cases.append(name)
            metrics.duration_seconds += parse_seconds(result.attrib.get("duration"))
        return metrics

    test_cases = [element for element in elements if local_name(element.tag) == "testcase"]
    if not test_cases:
        test_count = int(root.attrib.get("tests", 0) or 0)
        failures = int(root.attrib.get("failures", 0) or 0)
        errors = int(root.attrib.get("errors", 0) or 0)
        skipped = int(root.attrib.get("skipped", 0) or 0)
        return Metrics(
            total=test_count,
            passed=max(test_count - failures - errors - skipped, 0),
            failed=failures,
            skipped=skipped,
            errors=errors,
            duration_seconds=parse_seconds(root.attrib.get("time")),
        )

    metrics = Metrics()
    for case in test_cases:
        metrics.total += 1
        metrics.duration_seconds += parse_seconds(case.attrib.get("time"))
        children = {local_name(child.tag) for child in case}
        status = case.attrib.get("status", "").lower()
        if "error" in children or status in {"error", "errored"}:
            metrics.errors += 1
            name = f"{case.attrib.get('classname', '')}::{case.attrib.get('name', 'unknown-test')}".lstrip(":")
            detail = failure_detail(case)
            metrics.failed_cases.append(f"{name} — {detail}" if detail else name)
        elif "failure" in children or status in {"failed", "failure"}:
            metrics.failed += 1
            name = f"{case.attrib.get('classname', '')}::{case.attrib.get('name', 'unknown-test')}".lstrip(":")
            detail = failure_detail(case)
            metrics.failed_cases.append(f"{name} — {detail}" if detail else name)
        elif "skipped" in children or status in {"skipped", "pending"}:
            metrics.skipped += 1
        else:
            metrics.passed += 1
    return metrics


def parse_flutter(path: Path) -> Metrics:
    metrics = Metrics()
    started: set[str] = set()
    completed: set[str] = set()
    for line in path.read_text(encoding="utf-8", errors="replace").splitlines():
        try:
            decoded = json.loads(line)
        except json.JSONDecodeError:
            continue

        events = decoded if isinstance(decoded, list) else [decoded]
        for event in events:
            if not isinstance(event, dict):
                continue
            if event.get("type") == "testStart":
                params = event.get("testStart", {})
                if params.get("hidden"):
                    continue
                test_id = str(params.get("id", params.get("name", len(started))))
                started.add(test_id)
            elif event.get("type") == "testDone":
                params = event.get("testDone", {})
                if params.get("hidden"):
                    continue
                test_id = str(params.get("id", len(completed)))
                if test_id in completed:
                    continue
                completed.add(test_id)
                result = str(params.get("result", "error")).lower()
                metrics.total += 1
                if result == "success":
                    metrics.passed += 1
                elif result in {"skipped", "pending"}:
                    metrics.skipped += 1
                elif result in {"failure", "failed"}:
                    metrics.failed += 1
                    name = str(params.get("name", params.get("id", "unknown-test")))
                    error_text = str(params.get("error") or "")
                    detail_lines = error_text.splitlines()
                    detail = detail_lines[0][:240] if detail_lines else ""
                    metrics.failed_cases.append(f"{name} — {detail}" if detail else name)
                else:
                    metrics.errors += 1
                    name = str(params.get("name", params.get("id", "unknown-test")))
                    error_text = str(params.get("error") or "")
                    detail_lines = error_text.splitlines()
                    detail = detail_lines[0][:240] if detail_lines else ""
                    metrics.failed_cases.append(f"{name} — {detail}" if detail else name)
    metrics.total = max(metrics.total, len(started))
    return metrics


def format_row(scope: str, metrics: Metrics) -> str:
    return (
        f"{scope}: total={metrics.total} completed={metrics.completed} "
        f"passed={metrics.passed} failed={metrics.failed} "
        f"skipped={metrics.skipped} errors={metrics.errors} "
        f"not_run={metrics.not_run} duration={metrics.duration_seconds:.2f}s"
    )


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--results-root", type=Path)
    parser.add_argument("--input", type=Path, action="append")
    parser.add_argument("--format", choices=("auto", "junit", "flutter"), default="auto")
    parser.add_argument("--scope", required=True)
    parser.add_argument("--failures-only", action="store_true")
    args = parser.parse_args()

    grouped: dict[str, Metrics] = defaultdict(Metrics)
    if args.input:
        for input_path in args.input:
            if args.format == "flutter":
                grouped[args.scope].add(parse_flutter(input_path))
            else:
                grouped[args.scope].add(parse_xml(input_path))
    else:
        files = sorted(args.results_root.rglob("*.xml")) if args.results_root and args.results_root.exists() else []
        for path in files:
            relative = path.relative_to(args.results_root)
            service = relative.parts[0] if len(relative.parts) > 1 else path.parent.name
            try:
                grouped[service].add(parse_xml(path))
            except (ET.ParseError, OSError, ValueError) as error:
                print(f"WARNING: unable to parse {path}: {error}", file=sys.stderr)

    overall = Metrics()
    for metrics in grouped.values():
        overall.add(metrics)

    if not args.failures_only:
        print("Pass/fail reflects all test assertions, not HTTP status alone.")
        print("Test suites are expected to cover nominal, invalid, boundary and extreme conditions where relevant.")
        print(f"TEST METRICS — {args.scope}")
        print(format_row("overall", overall))
        if grouped:
            for service in sorted(grouped):
                print(format_row(f"service={service}", grouped[service]))
        else:
            print("No test result files were produced.")

    print(f"FAILED TEST CASES — {args.scope} (complete assertion failures)")
    failed_cases = [(service, case) for service, metrics in grouped.items() for case in metrics.failed_cases]
    if failed_cases:
        for service, case in sorted(failed_cases):
            print(f"- {service}: {case}")
    else:
        print("- None detected.")

    summary_path = Path(os.environ["GITHUB_STEP_SUMMARY"]) if os.environ.get("GITHUB_STEP_SUMMARY") else None
    if summary_path and not args.failures_only:
        lines = [
            f"## {args.scope} test metrics",
            "",
            "| Scope | Total | Completed | Passed | Failed | Skipped | Errors | Not run | Duration |",
            "|---|---:|---:|---:|---:|---:|---:|---:|---:|",
            f"| Overall | {overall.total} | {overall.completed} | {overall.passed} | {overall.failed} | {overall.skipped} | {overall.errors} | {overall.not_run} | {overall.duration_seconds:.2f}s |",
        ]
        for service in sorted(grouped):
            metrics = grouped[service]
            lines.append(
                f"| {service} | {metrics.total} | {metrics.completed} | {metrics.passed} | {metrics.failed} | {metrics.skipped} | {metrics.errors} | {metrics.not_run} | {metrics.duration_seconds:.2f}s |"
            )
        lines.extend(["", "### Failed test cases", ""])
        if failed_cases:
            lines.extend(f"- **{service}**: `{case}`" for service, case in sorted(failed_cases))
        else:
            lines.append("- None detected.")
        existing_summary = summary_path.read_text(encoding="utf-8") if summary_path.exists() else ""
        summary_path.write_text(existing_summary + "\n".join(lines) + "\n", encoding="utf-8")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

"""Verification script to test live Gemini integration with Marine Conditions Intelligence Agent.

Usage:
  python services/ai/marine-conditions/check_gemini.py
"""

from datetime import datetime, timezone
import json
import os
import sys

from gemini import DEFAULT_GEMINI_MODEL, GeminiClient, _load_env_if_present
from agent import MarineConditionsIntelligenceAgent
from schemas import (
    LocationDto,
    MarineConditionsAgentInput,
    TimePeriodDto,
    format_iso_datetime,
)


def main():
    print("=" * 60)
    print("BLUEVERSE: Marine Conditions Intelligence Agent - Gemini Verification")
    print("=" * 60)

    # 1. Check environment variable detection
    _load_env_if_present()
    api_key = os.getenv("GEMINI_API_KEY")
    model = os.getenv("GEMINI_MODEL", DEFAULT_GEMINI_MODEL)

    if not api_key:
        print("[!] GEMINI_API_KEY is NOT set in the environment or .env file.")
        print("    Please set GEMINI_API_KEY in your .env file or shell:")
        print("      GEMINI_API_KEY=AIzaSy...")
        print("    The agent is currently using its deterministic fallback generator.")
        sys.exit(1)

    masked_key = api_key[:6] + "..." + api_key[-4:] if len(api_key) > 10 else "***"
    print(f"[+] GEMINI_API_KEY detected: {masked_key}")
    print(f"[+] Target Model: {model}")

    # 2. Test direct Gemini API call
    print("\n[+] Testing direct connection to Google AI Studio...")
    client = GeminiClient(api_key=api_key, model=model)
    relevance, limitations, err = client.generate_conditions_summary(
        sanitized_objective="Assess coastal safety for morning snorkeling in Mirissa reef.",
        activity="snorkeling",
        factors={
            "wind_speed": {"value": 11.2, "unit": "km/h"},
            "wave_height": {"value": 0.8, "unit": "m"},
            "swell_wave_height": {"value": 0.5, "unit": "m"},
            "precipitation": {"value": 0.0, "unit": "mm"},
        },
        missing=[],
    )

    if err:
        print(f"[-] Gemini API call failed with reason: {err}")
        print("    Please verify that your Google AI Studio API key is valid and has quota.")
        sys.exit(2)

    print("[OK] Direct Gemini API call succeeded!")
    print(f"     Objective Relevance: {relevance}")
    print(f"     Uncertainty/Limitations: {limitations}")

    # 3. Test end-to-end Agent execution with Gemini and Safety Validator
    print("\n[+] Testing end-to-end Agent execution with Deterministic Safety Validator...")
    agent = MarineConditionsIntelligenceAgent(gemini_client=client)

    now_iso = format_iso_datetime(datetime.now(timezone.utc))
    test_input = MarineConditionsAgentInput(
        workflow_id="wf-live-gemini-test",
        step_id="step-01-marine",
        objective="Assess conditions for beginner snorkeling session.",
        location=LocationDto(latitude=5.9482, longitude=80.4578),
        activity="snorkeling",
        period=TimePeriodDto(start_time=now_iso, end_time=now_iso),
        required_factors=["wind_speed", "wave_height", "precipitation"],
    )

    try:
        report = agent.execute(test_input)
        print("[OK] End-to-end agent execution succeeded!")
        print(f"     Report Status: {report.status.value}")
        print(f"     Factors Count: {len(report.environmental_factors)}")
        print(f"     Summary: {report.objective_relevance_summary}")
        print(f"     Disclaimer: {report.disclaimer}")
        print("\nAll Gemini verification checks PASSED successfully!")
    except Exception as exc:
        print(f"[-] Agent execution error: {exc}")
        sys.exit(3)


if __name__ == "__main__":
    main()

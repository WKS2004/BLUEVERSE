"""Allowlisted read tools for Marine Conditions Intelligence Agent.

Candidate allowlist:
- weather_forecast_lookup
- marine_forecast_lookup
- condition_snapshot_lookup

All tools are read-only, strictly validated, and time-bounded.
Raw HTTP credentials are never exposed to the agent.
"""

from __future__ import annotations

from dataclasses import dataclass, field
from datetime import datetime, timezone
import json
import logging
import os
import urllib.error
import urllib.parse
import urllib.request
from typing import Any, Callable, Dict, List, Optional, Tuple

try:
    from .audit import audit_logger
    from .schemas import (
        DEFAULT_ALLOWED_TOOLS,
        EnvironmentalFactor,
        LocationDto,
        TimePeriodDto,
        format_iso_datetime,
        parse_iso_datetime,
    )
except ImportError:
    from audit import audit_logger
    from schemas import (
        DEFAULT_ALLOWED_TOOLS,
        EnvironmentalFactor,
        LocationDto,
        TimePeriodDto,
        format_iso_datetime,
        parse_iso_datetime,
    )

logger = logging.getLogger("blueverse.ai.marine_conditions.tools")


class ToolError(Exception):
    """Base exception for tool execution errors."""
    def __init__(self, message: str, category: str = "TOOL_ERROR", retryable: bool = False):
        super().__init__(message)
        self.category = category
        self.retryable = retryable


class ToolNotAllowedError(ToolError):
    def __init__(self, tool_name: str):
        super().__init__(f"Tool '{tool_name}' is not in the authorized tool allowlist.", category="UNAUTHORIZED_TOOL", retryable=False)


class ToolValidationError(ToolError):
    def __init__(self, message: str):
        super().__init__(message, category="VALIDATION_ERROR", retryable=False)


class ToolProviderError(ToolError):
    def __init__(self, message: str, retryable: bool = True):
        super().__init__(message, category="PROVIDER_OUTAGE", retryable=retryable)


@dataclass
class ToolExecutionResult:
    success: bool
    data: Optional[Dict[str, Any]] = None
    error: Optional[str] = None
    error_category: Optional[str] = None
    missing_factors: List[str] = field(default_factory=list)
    source_updated_time: Optional[str] = None
    retrieval_time: str = field(default_factory=lambda: format_iso_datetime(datetime.now(timezone.utc)))


class ToolRegistry:
    """Registry and dispatcher for allowlisted tools with timeout, retries, and auditing."""

    def __init__(
        self,
        weather_base_url: Optional[str] = None,
        marine_base_url: Optional[str] = None,
        transport: Optional[Callable[[str, float], Tuple[int, Dict[str, Any]]]] = None,
    ):
        self.weather_base_url = weather_base_url or os.getenv("OPEN_METEO_WEATHER_URL", "https://api.open-meteo.com/v1/forecast")
        self.marine_base_url = marine_base_url or os.getenv("OPEN_METEO_MARINE_URL", "https://marine-api.open-meteo.com/v1/marine")
        self.snapshot_storage: Dict[str, Dict[str, Any]] = {}
        self.transport = transport  # Optional custom transport for test injection

    def execute_tool(
        self,
        tool_name: str,
        arguments: Dict[str, Any],
        workflow_id: str,
        step_id: str,
        allowed_tools: Optional[List[str]] = None,
        timeout_seconds: float = 10.0,
        max_retries: int = 1,
    ) -> ToolExecutionResult:
        start_time = datetime.now(timezone.utc)
        allowed = set(allowed_tools if allowed_tools is not None else DEFAULT_ALLOWED_TOOLS)

        if tool_name not in allowed:
            err = ToolNotAllowedError(tool_name)
            end_time = datetime.now(timezone.utc)
            audit_logger.log_tool_invocation(
                workflow_id=workflow_id,
                step_id=step_id,
                tool_name=tool_name,
                start_time=start_time,
                end_time=end_time,
                status="BLOCKED",
                error_category=err.category,
                parameters_summary=arguments,
            )
            raise err

        retry_count = 0
        last_exception: Optional[Exception] = None

        while retry_count <= max_retries:
            try:
                if tool_name == "weather_forecast_lookup":
                    result = self._weather_forecast_lookup(arguments, timeout_seconds)
                elif tool_name == "marine_forecast_lookup":
                    result = self._marine_forecast_lookup(arguments, timeout_seconds)
                elif tool_name == "condition_snapshot_lookup":
                    result = self._condition_snapshot_lookup(arguments)
                else:
                    raise ToolNotAllowedError(tool_name)

                end_time = datetime.now(timezone.utc)
                audit_logger.log_tool_invocation(
                    workflow_id=workflow_id,
                    step_id=step_id,
                    tool_name=tool_name,
                    start_time=start_time,
                    end_time=end_time,
                    status="SUCCESS",
                    retry_count=retry_count,
                    parameters_summary=arguments,
                    result_summary={
                        "success": result.success,
                        "factors_count": len(result.data or {}),
                        "missing": result.missing_factors,
                    },
                )
                return result

            except ToolValidationError as ve:
                end_time = datetime.now(timezone.utc)
                audit_logger.log_tool_invocation(
                    workflow_id=workflow_id,
                    step_id=step_id,
                    tool_name=tool_name,
                    start_time=start_time,
                    end_time=end_time,
                    status="ERROR",
                    error_category=ve.category,
                    retry_count=0,
                    parameters_summary=arguments,
                )
                raise ve
            except ToolProviderError as pe:
                last_exception = pe
                if pe.retryable and retry_count < max_retries:
                    retry_count += 1
                    logger.warning("Retrying tool '%s' (attempt %d): %s", tool_name, retry_count, pe)
                    continue
                break
            except Exception as exc:
                last_exception = exc
                break

        end_time = datetime.now(timezone.utc)
        error_category = getattr(last_exception, "category", "UNEXPECTED_ERROR")
        audit_logger.log_tool_invocation(
            workflow_id=workflow_id,
            step_id=step_id,
            tool_name=tool_name,
            start_time=start_time,
            end_time=end_time,
            status="ERROR",
            error_category=error_category,
            retry_count=retry_count,
            parameters_summary=arguments,
        )

        return ToolExecutionResult(
            success=False,
            error=str(last_exception),
            error_category=error_category,
            retrieval_time=format_iso_datetime(end_time),
        )

    def _validate_coordinates(self, args: Dict[str, Any]) -> Tuple[float, float]:
        if "latitude" not in args or "longitude" not in args:
            raise ToolValidationError("Tools require 'latitude' and 'longitude'.")
        try:
            lat = float(args["latitude"])
            lon = float(args["longitude"])
        except (ValueError, TypeError) as exc:
            raise ToolValidationError(f"Invalid coordinate numbers: {exc}") from exc

        if not (-90.0 <= lat <= 90.0):
            raise ToolValidationError(f"Latitude {lat} is out of valid range [-90, 90].")
        if not (-180.0 <= lon <= 180.0):
            raise ToolValidationError(f"Longitude {lon} is out of valid range [-180, 180].")
        return round(lat, 6), round(lon, 6)

    def _weather_forecast_lookup(self, args: Dict[str, Any], timeout: float) -> ToolExecutionResult:
        lat, lon = self._validate_coordinates(args)
        variables = ["wind_speed_10m", "precipitation", "weather_code", "temperature_2m"]
        query_params = {
            "latitude": lat,
            "longitude": lon,
            "hourly": ",".join(variables),
            "timezone": "UTC",
        }
        url = f"{self.weather_base_url}?{urllib.parse.urlencode(query_params)}"
        raw_json, status_code = self._fetch_url(url, timeout)

        if status_code != 200 or not isinstance(raw_json, dict):
            raise ToolProviderError(f"Open-Meteo weather returned status {status_code}", retryable=status_code in (500, 502, 503, 504))

        hourly = raw_json.get("hourly", {})
        times = hourly.get("time", [])
        data: Dict[str, Any] = {}
        missing: List[str] = []

        # Find closest index based on requested time or first hour
        target_idx = 0
        if "time" in args and times:
            try:
                target_dt = parse_iso_datetime(args["time"])
                target_str = target_dt.strftime("%Y-%m-%dT%H:00")
                if target_str in times:
                    target_idx = times.index(target_str)
            except Exception:
                target_idx = 0

        forecast_time = times[target_idx] if times and target_idx < len(times) else format_iso_datetime(datetime.now(timezone.utc))

        # Factors extraction with strict zero vs missing distinction
        ws = self._extract_value(hourly, "wind_speed_10m", target_idx)
        if ws is not None:
            data["wind_speed"] = {"value": ws, "unit": "km/h", "forecast_or_observed_time": forecast_time}
        else:
            missing.append("wind_speed")

        precip = self._extract_value(hourly, "precipitation", target_idx)
        if precip is not None:
            data["precipitation"] = {"value": precip, "unit": "mm", "forecast_or_observed_time": forecast_time}
        else:
            missing.append("precipitation")

        wcode = self._extract_value(hourly, "weather_code", target_idx)
        if wcode is not None:
            data["weather_code"] = {"value": int(wcode), "unit": "wmo_code", "description": self._describe_weather_code(int(wcode)), "forecast_or_observed_time": forecast_time}
        else:
            missing.append("weather_code")

        temp = self._extract_value(hourly, "temperature_2m", target_idx)
        if temp is not None:
            data["temperature"] = {"value": temp, "unit": "C", "forecast_or_observed_time": forecast_time}

        return ToolExecutionResult(
            success=True,
            data=data,
            missing_factors=missing,
            source_updated_time=raw_json.get("generationtime_ms"),
            retrieval_time=format_iso_datetime(datetime.now(timezone.utc)),
        )

    def _marine_forecast_lookup(self, args: Dict[str, Any], timeout: float) -> ToolExecutionResult:
        lat, lon = self._validate_coordinates(args)
        variables = ["wave_height", "swell_wave_height", "wave_direction", "wave_period"]
        query_params = {
            "latitude": lat,
            "longitude": lon,
            "hourly": ",".join(variables),
            "timezone": "UTC",
        }
        url = f"{self.marine_base_url}?{urllib.parse.urlencode(query_params)}"
        raw_json, status_code = self._fetch_url(url, timeout)

        if status_code != 200 or not isinstance(raw_json, dict):
            raise ToolProviderError(f"Open-Meteo marine returned status {status_code}", retryable=status_code in (500, 502, 503, 504))

        hourly = raw_json.get("hourly", {})
        times = hourly.get("time", [])
        data: Dict[str, Any] = {}
        missing: List[str] = []

        target_idx = 0
        if "time" in args and times:
            try:
                target_dt = parse_iso_datetime(args["time"])
                target_str = target_dt.strftime("%Y-%m-%dT%H:00")
                if target_str in times:
                    target_idx = times.index(target_str)
            except Exception:
                target_idx = 0

        forecast_time = times[target_idx] if times and target_idx < len(times) else format_iso_datetime(datetime.now(timezone.utc))

        wh = self._extract_value(hourly, "wave_height", target_idx)
        if wh is not None:
            data["wave_height"] = {"value": wh, "unit": "m", "forecast_or_observed_time": forecast_time}
        else:
            missing.append("wave_height")

        swh = self._extract_value(hourly, "swell_wave_height", target_idx)
        if swh is not None:
            data["swell_wave_height"] = {"value": swh, "unit": "m", "forecast_or_observed_time": forecast_time}
        else:
            missing.append("swell_wave_height")

        wd = self._extract_value(hourly, "wave_direction", target_idx)
        if wd is not None:
            data["wave_direction"] = {"value": wd, "unit": "degrees", "forecast_or_observed_time": forecast_time}

        wp = self._extract_value(hourly, "wave_period", target_idx)
        if wp is not None:
            data["wave_period"] = {"value": wp, "unit": "s", "forecast_or_observed_time": forecast_time}

        return ToolExecutionResult(
            success=True,
            data=data,
            missing_factors=missing,
            source_updated_time=raw_json.get("generationtime_ms"),
            retrieval_time=format_iso_datetime(datetime.now(timezone.utc)),
        )

    def _condition_snapshot_lookup(self, args: Dict[str, Any]) -> ToolExecutionResult:
        snapshot_id = str(args.get("snapshot_id", "")).strip()
        if not snapshot_id:
            raise ToolValidationError("Tool 'condition_snapshot_lookup' requires 'snapshot_id'.")

        snapshot = self.snapshot_storage.get(snapshot_id)
        if not snapshot:
            return ToolExecutionResult(
                success=False,
                error=f"Snapshot '{snapshot_id}' not found.",
                error_category="RESOURCE_NOT_FOUND",
            )

        return ToolExecutionResult(
            success=True,
            data=snapshot.get("data", {}),
            missing_factors=snapshot.get("missing_factors", []),
            source_updated_time=snapshot.get("source_updated_time"),
            retrieval_time=format_iso_datetime(datetime.now(timezone.utc)),
        )

    def save_snapshot(self, snapshot_id: str, data: Dict[str, Any], missing_factors: Optional[List[str]] = None):
        """Save a snapshot for caching/snapshot_lookup."""
        self.snapshot_storage[snapshot_id] = {
            "data": data,
            "missing_factors": missing_factors or [],
            "source_updated_time": format_iso_datetime(datetime.now(timezone.utc)),
        }

    @staticmethod
    def _extract_value(hourly: Dict[str, Any], key: str, idx: int) -> Optional[float]:
        values = hourly.get(key)
        if isinstance(values, list) and idx < len(values):
            val = values[idx]
            if val is not None:
                try:
                    return float(val)
                except (ValueError, TypeError):
                    return None
        return None

    def _fetch_url(self, url: str, timeout: float) -> Tuple[Dict[str, Any], int]:
        if self.transport:
            status_code, body = self.transport(url, timeout)
            return body, status_code

        # Default real network request
        req = urllib.request.Request(
            url,
            headers={"User-Agent": "BLUEVERSE-MarineSafetyAgent/1.0", "Accept": "application/json"},
        )
        try:
            with urllib.request.urlopen(req, timeout=timeout) as response:
                body = response.read().decode("utf-8")
                return json.loads(body), response.status
        except urllib.error.HTTPError as he:
            logger.warning("HTTP error calling provider %s: %d %s", url, he.code, he.reason)
            return {}, he.code
        except urllib.error.URLError as ue:
            logger.warning("Network error calling provider %s: %s", url, ue.reason)
            raise ToolProviderError(f"Network connectivity error: {ue.reason}", retryable=True) from ue
        except TimeoutError as te:
            logger.warning("Timeout calling provider %s after %.1fs", url, timeout)
            raise ToolProviderError(f"Provider request timed out after {timeout}s", retryable=True) from te

    @staticmethod
    def _describe_weather_code(code: int) -> str:
        descriptions = {
            0: "Clear sky",
            1: "Mainly clear",
            2: "Partly cloudy",
            3: "Overcast",
            45: "Fog",
            48: "Depositing rime fog",
            51: "Light drizzle",
            53: "Moderate drizzle",
            55: "Dense drizzle",
            61: "Slight rain",
            63: "Moderate rain",
            65: "Heavy rain",
            80: "Slight rain showers",
            81: "Moderate rain showers",
            82: "Violent rain showers",
            95: "Thunderstorm",
        }
        return descriptions.get(code, f"Weather code {code}")

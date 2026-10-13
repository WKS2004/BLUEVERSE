"""Least-privilege tool allowlist for the Safety & Operations role."""

from __future__ import annotations

from typing import Any, Callable, Mapping
import json

from .contracts import MAX_INPUT_BYTES, MAX_OUTPUT_BYTES


CANDIDATE_READ_ONLY_TOOLS = frozenset(
    {
        "safety_profile_lookup",
        "operational_status_lookup",
        "active_alert_lookup",
        "operational_constraint_lookup",
    }
)


class ToolNotAllowed(ValueError):
    """The requested operation is outside this role's read-only allowlist."""


class ToolNotConnected(RuntimeError):
    """The read-only tool has not been connected to an authoritative source."""


class SafetyOperationsToolRegistry:
    """Dispatch only registered names from the fixed role-owned allowlist."""

    def __init__(
        self,
        handlers: Mapping[str, Callable[[Mapping[str, Any]], Mapping[str, Any]]] | None = None,
    ) -> None:
        configured = dict(handlers or {})
        if not set(configured).issubset(CANDIDATE_READ_ONLY_TOOLS):
            raise ToolNotAllowed("a tool handler is outside the role allowlist")
        self._handlers = configured

    def invoke(self, name: str, arguments: Mapping[str, Any]) -> Mapping[str, Any]:
        if name not in CANDIDATE_READ_ONLY_TOOLS:
            raise ToolNotAllowed("the requested tool is outside the role allowlist")
        handler = self._handlers.get(name)
        if handler is None:
            raise ToolNotConnected("the requested read-only tool is not connected")
        if not isinstance(arguments, Mapping):
            raise ValueError("tool arguments must be an object")
        try:
            encoded_arguments = json.dumps(
                arguments, ensure_ascii=False, allow_nan=False, separators=(",", ":")
            ).encode("utf-8")
        except (TypeError, ValueError) as exc:
            raise ValueError("tool arguments must contain JSON values") from exc
        if len(encoded_arguments) > MAX_INPUT_BYTES:
            raise ValueError("tool arguments exceed the byte limit")

        result = handler(arguments)
        if not isinstance(result, Mapping):
            raise ValueError("tool response must be an object")
        try:
            encoded_result = json.dumps(
                result, ensure_ascii=False, allow_nan=False, separators=(",", ":")
            ).encode("utf-8")
        except (TypeError, ValueError) as exc:
            raise ValueError("tool response must contain JSON values") from exc
        if len(encoded_result) > MAX_OUTPUT_BYTES:
            raise ValueError("tool response exceeds the byte limit")
        return dict(result)

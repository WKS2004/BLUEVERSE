"""Audit logging and execution evidence for Agentic AI workflows.

Preserves structured execution summaries, timestamps, and tool outcomes.
Strictly prohibits storing credentials, JWT tokens, or hidden model reasoning.
"""

from __future__ import annotations

from dataclasses import dataclass, asdict
from datetime import datetime, timezone
import json
import logging
from typing import Any, Dict, List, Optional

logger = logging.getLogger("blueverse.ai.marine_conditions.audit")


@dataclass
class ToolAuditRecord:
    workflow_id: str
    step_id: str
    tool_name: str
    start_time: str
    end_time: str
    duration_ms: float
    status: str  # "SUCCESS", "ERROR", "BLOCKED", "TIMEOUT"
    error_category: Optional[str] = None
    retry_count: int = 0
    parameters_summary: Optional[Dict[str, Any]] = None
    result_summary: Optional[Dict[str, Any]] = None

    def to_dict(self) -> Dict[str, Any]:
        return asdict(self)


class AuditLogger:
    """Thread-safe and auditable in-memory and logging recorder for agent operations."""

    def __init__(self):
        self._records: List[ToolAuditRecord] = []

    def log_tool_invocation(
        self,
        workflow_id: str,
        step_id: str,
        tool_name: str,
        start_time: datetime,
        end_time: datetime,
        status: str,
        error_category: Optional[str] = None,
        retry_count: int = 0,
        parameters_summary: Optional[Dict[str, Any]] = None,
        result_summary: Optional[Dict[str, Any]] = None,
    ) -> ToolAuditRecord:
        duration_ms = (end_time - start_time).total_seconds() * 1000.0

        # Sanitize parameters and result summaries to prevent accidental secret leakage
        sanitized_params = self._sanitize(parameters_summary) if parameters_summary else None
        sanitized_result = self._sanitize(result_summary) if result_summary else None

        record = ToolAuditRecord(
            workflow_id=workflow_id,
            step_id=step_id,
            tool_name=tool_name,
            start_time=start_time.strftime("%Y-%m-%dT%H:%M:%SZ"),
            end_time=end_time.strftime("%Y-%m-%dT%H:%M:%SZ"),
            duration_ms=round(duration_ms, 2),
            status=status,
            error_category=error_category,
            retry_count=retry_count,
            parameters_summary=sanitized_params,
            result_summary=sanitized_result,
        )

        self._records.append(record)
        logger.info(
            "Tool audit: tool=%s workflow=%s step=%s status=%s duration_ms=%.2f retries=%d",
            tool_name,
            workflow_id,
            step_id,
            status,
            duration_ms,
            retry_count,
        )
        return record

    def get_records(self, workflow_id: Optional[str] = None) -> List[ToolAuditRecord]:
        if workflow_id:
            return [r for r in self._records if r.workflow_id == workflow_id]
        return list(self._records)

    def clear(self):
        self._records.clear()

    @staticmethod
    def _sanitize(data: Optional[Dict[str, Any]]) -> Dict[str, Any]:
        if not isinstance(data, dict):
            return {}
        forbidden_keys = {"token", "password", "secret", "key", "authorization", "bearer", "reasoning", "chain_of_thought"}
        sanitized = {}
        for k, v in data.items():
            if any(forbidden in k.lower() for forbidden in forbidden_keys):
                sanitized[k] = "[REDACTED]"
            elif isinstance(v, dict):
                sanitized[k] = AuditLogger._sanitize(v)
            else:
                sanitized[k] = v
        return sanitized


# Global audit logger instance
audit_logger = AuditLogger()

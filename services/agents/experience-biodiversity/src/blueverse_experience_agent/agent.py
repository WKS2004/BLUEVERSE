"""Coastal Experience & Biodiversity Agent Core Orchestrator.

Implements the specialist reasoning, tool invocation allowlist, deterministic
context aggregation, contradictory-selector detection, and structured report synthesis.
"""

from __future__ import annotations

from dataclasses import replace
from typing import List, Optional

from .gemini_client import GeminiClient
from .schemas import (
    ActivityContext,
    AgentCompletionOutcome,
    AuditableToolCall,
    BiodiversityContext,
    BiodiversityStatus,
    DestinationContext,
    ExperienceAgentInput,
    ExperienceBiodiversityContextReport,
    ModelExecutionStatus,
    OfferingContext,
    ScheduleContext,
)
from .tools import ExperienceAgentTools


SYSTEM_ROLE_PROMPT = """You are the Coastal Experience & Biodiversity Agent (Member 1) for the BLUEVERSE platform.
Your responsibility is to assemble an evidence-grounded, structured context report describing coastal destinations,
activities, offerings, schedules, and optional biodiversity predictions.
Invariants:
1. You are a read-only specialist agent. You never mutate records, authorize operations, or invent facts.
2. Biodiversity predictions are informative model outputs only, not observed presence or safety authorities.
3. If an upstream service or prediction is unavailable, report it explicitly as unavailable without hallucinating.
4. Keep publication status, schedule coverage status, and time-window availability distinct.
"""


class CoastalExperienceAgent:
    def __init__(
        self,
        tools: Optional[ExperienceAgentTools] = None,
        gemini_client: Optional[GeminiClient] = None,
    ):
        self.tools = tools or ExperienceAgentTools()
        self.gemini_client = gemini_client or GeminiClient()

    def generate_report(
        self, agent_input: ExperienceAgentInput, auth_token: Optional[str] = None
    ) -> ExperienceBiodiversityContextReport:
        """Run the specialist agent workflow and generate the structured context report."""
        audited_tools: List[AuditableToolCall] = []
        evidence_refs: List[str] = []
        uncertainty_gaps: List[str] = []
        constraints: List[str] = []

        destination: Optional[DestinationContext] = None
        activity: Optional[ActivityContext] = None
        offering: Optional[OfferingContext] = None
        schedule: Optional[ScheduleContext] = None
        biodiversity: Optional[BiodiversityContext] = None

        has_unresolved_primary = False
        has_contradictory_selectors = False

        # 1. Lookup Destination if specified
        if agent_input.destination_id:
            destination, audit = self.tools.destination_lookup(
                agent_input.destination_id,
                agent_input.workflow_id,
                agent_input.step_id,
                auth_token,
            )
            audited_tools.append(audit)
            if destination:
                evidence_refs.append(f"Destination:{destination.destination_id}:{destination.status}")
                if destination.status.upper() != "PUBLISHED":
                    uncertainty_gaps.append(
                        f"Destination status is {destination.status}, not PUBLISHED."
                    )
            else:
                has_unresolved_primary = True
                uncertainty_gaps.append(
                    f"Destination {agent_input.destination_id} could not be resolved ({audit.outcome})."
                )

        # 2. Lookup Activity if specified
        if agent_input.activity_id:
            activity, audit = self.tools.activity_lookup(
                agent_input.activity_id,
                agent_input.workflow_id,
                agent_input.step_id,
                auth_token,
            )
            audited_tools.append(audit)
            if activity:
                evidence_refs.append(f"Activity:{activity.activity_id}:{activity.category}:{activity.status}")
                constraints.extend(activity.constraints)
                if activity.experience_level:
                    constraints.append(f"Requires experience level: {activity.experience_level}")
                if activity.status.upper() != "PUBLISHED" and activity.status.upper() != "ACTIVE":
                    uncertainty_gaps.append(
                        f"Activity status is {activity.status}, not PUBLISHED."
                    )
            else:
                has_unresolved_primary = True
                uncertainty_gaps.append(
                    f"Activity {agent_input.activity_id} could not be resolved ({audit.outcome})."
                )

        # 3. Lookup Offering if specified
        if agent_input.offering_id:
            offering, audit = self.tools.offering_lookup(
                agent_input.offering_id,
                agent_input.workflow_id,
                agent_input.step_id,
                auth_token,
            )
            audited_tools.append(audit)
            if offering:
                # Check for contradictory destination/activity selectors
                if agent_input.destination_id and offering.destination_id != agent_input.destination_id:
                    has_contradictory_selectors = True
                    uncertainty_gaps.append(
                        f"Contradictory selectors: offering {offering.offering_id} belongs to destination "
                        f"{offering.destination_id}, which does not match requested destination {agent_input.destination_id}."
                    )
                if agent_input.activity_id and offering.activity_id != agent_input.activity_id:
                    has_contradictory_selectors = True
                    uncertainty_gaps.append(
                        f"Contradictory selectors: offering {offering.offering_id} belongs to activity "
                        f"{offering.activity_id}, which does not match requested activity {agent_input.activity_id}."
                    )

                if offering.experience_levels:
                    constraints.append(
                        f"Supported experience levels: {', '.join(offering.experience_levels)}"
                    )
                if offering.publication_status.upper() != "PUBLISHED":
                    uncertainty_gaps.append(
                        f"Offering publication status is {offering.publication_status}, not PUBLISHED."
                    )

                # 4. Lookup Schedule when offering is resolved
                schedule, sched_audit = self.tools.schedule_lookup(
                    agent_input.offering_id,
                    agent_input.target_date,
                    agent_input.workflow_id,
                    agent_input.step_id,
                    auth_token,
                    period_starts_at=agent_input.period_starts_at,
                    period_ends_at=agent_input.period_ends_at,
                )
                audited_tools.append(sched_audit)
                if schedule:
                    sched_status_str = "SCHEDULED" if schedule.is_scheduled else "UNSCHEDULED"
                    combined_avail_status = schedule.availability_status
                    if offering.publication_status.upper() != "PUBLISHED":
                        combined_avail_status = "UNAVAILABLE"
                    is_avail_final = (
                        offering.publication_status.upper() == "PUBLISHED"
                        and schedule.is_scheduled
                        and combined_avail_status == "AVAILABLE"
                    )
                    offering = replace(
                        offering,
                        schedule_status=sched_status_str,
                        availability_status=combined_avail_status,
                        is_available=is_avail_final,
                        operational_restriction=schedule.operational_restriction or offering.operational_restriction,
                    )
                    evidence_refs.append(
                        f"Offering:{offering.offering_id}:Publication={offering.publication_status}:"
                        f"Schedule={offering.schedule_status}:Availability={offering.availability_status}"
                    )
                    if schedule.is_scheduled:
                        evidence_refs.append(
                            f"Schedule:{offering.offering_id}:Slots={len(schedule.slots)}"
                        )
                        if not any(s.is_open for s in schedule.slots):
                            uncertainty_gaps.append(
                                "No open schedule slots found for the requested timeframe."
                            )
                    else:
                        uncertainty_gaps.append(
                            "Offering is not actively scheduled for the requested period."
                        )
                    for reason_code in schedule.reason_codes:
                        uncertainty_gaps.append(f"Availability evaluation note: {reason_code}")
                else:
                    evidence_refs.append(
                        f"Offering:{offering.offering_id}:Publication={offering.publication_status}"
                    )
                    uncertainty_gaps.append(
                        f"Schedule for offering {offering.offering_id} could not be verified ({sched_audit.outcome})."
                    )
            else:
                has_unresolved_primary = True
                uncertainty_gaps.append(
                    f"Offering {agent_input.offering_id} could not be resolved ({audit.outcome})."
                )

        # 5. Biodiversity prediction lookup (optional / contextual via ADR-0019)
        target_dest_id = agent_input.destination_id or (offering.destination_id if offering else None)
        if agent_input.include_biodiversity and target_dest_id and not has_contradictory_selectors:
            biodiversity, audit = self.tools.biodiversity_prediction_lookup(
                target_dest_id,
                agent_input.workflow_id,
                agent_input.step_id,
                auth_token,
            )
            audited_tools.append(audit)
            if biodiversity.status == BiodiversityStatus.AVAILABLE:
                evidence_refs.append(
                    f"Biodiversity:{target_dest_id}:Model={biodiversity.model_version}:Count={len(biodiversity.predictions)}"
                )
            else:
                uncertainty_gaps.append(
                    f"Biodiversity predictions unavailable ({biodiversity.status.value}): "
                    f"{biodiversity.uncertainty_notes or 'Service returned non-available status'}"
                )
        elif agent_input.include_biodiversity and not target_dest_id:
            biodiversity = BiodiversityContext(
                status=BiodiversityStatus.MISSING,
                predictions=[],
                uncertainty_notes="Biodiversity enrichment was requested, but no destination ID could be resolved.",
            )
            uncertainty_gaps.append(
                "Biodiversity predictions unavailable (missing): No destination ID available for lookup."
            )
        else:
            biodiversity = BiodiversityContext(
                status=BiodiversityStatus.NOT_REQUESTED,
                predictions=[],
                uncertainty_notes="Biodiversity enrichment was not requested for this workflow step.",
            )

        # 6. Optional Google AI Studio Gemini synthesis (bounded by deterministic tool evidence)
        model_execution, model_uncertainty_notes = self.gemini_client.synthesize_context(
            agent_input=agent_input,
            system_instruction=SYSTEM_ROLE_PROMPT,
            destination=destination,
            activity=activity,
            offering=offering,
            schedule=schedule,
            biodiversity=biodiversity,
            existing_gaps=list(uncertainty_gaps),
        )
        for note in model_uncertainty_notes:
            if note not in uncertainty_gaps:
                uncertainty_gaps.append(note)
        if model_execution.status == ModelExecutionStatus.INVALID_OUTPUT and model_execution.error_summary:
            uncertainty_gaps.append(f"Model output validation note: {model_execution.error_summary}")

        # Determine completion outcome
        has_primary_evidence = bool(destination or activity or offering)
        if has_contradictory_selectors or has_unresolved_primary or not has_primary_evidence:
            outcome = AgentCompletionOutcome.REQUIRES_UPSTREAM_CORRECTION
        elif agent_input.include_biodiversity and biodiversity.status != BiodiversityStatus.AVAILABLE:
            outcome = AgentCompletionOutcome.SUCCESS_OPTIONAL_DATA_UNAVAILABLE
        else:
            outcome = AgentCompletionOutcome.SUCCESS_COMPLETE

        return ExperienceBiodiversityContextReport(
            workflow_id=agent_input.workflow_id,
            step_id=agent_input.step_id,
            objective=agent_input.objective,
            completion_outcome=outcome,
            destination=destination,
            activity=activity,
            offering=offering,
            schedule=schedule,
            biodiversity=biodiversity,
            experience_constraints=list(dict.fromkeys(constraints)),
            audited_tool_calls=audited_tools,
            evidence_references=evidence_refs,
            uncertainty_and_gaps=uncertainty_gaps,
            model_execution=model_execution,
        )

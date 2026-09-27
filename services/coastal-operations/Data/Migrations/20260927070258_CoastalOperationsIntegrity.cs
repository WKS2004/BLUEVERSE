using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Blueverse.CoastalOperations.Data.Migrations
{
    /// <inheritdoc />
    public partial class CoastalOperationsIntegrity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "CK_TargetOperationalStates_State",
                schema: "coastal_operations",
                table: "TargetOperationalStates",
                sql: "\"State\" IN ('OPEN','CAUTION','TEMPORARILY_SUSPENDED') OR (\"TargetType\" = 'SESSION' AND \"State\" IN ('CANCELLED','COMPLETED'))");

            migrationBuilder.AddCheckConstraint(
                name: "CK_TargetOperationalStates_Version",
                schema: "coastal_operations",
                table: "TargetOperationalStates",
                sql: "\"Version\" > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ReviewerDecisions_Decision",
                schema: "coastal_operations",
                table: "ReviewerDecisions",
                sql: "\"Decision\" IN ('APPROVE','REJECT','REQUEST_REVISION')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ReviewerDecisions_ProposalVersion",
                schema: "coastal_operations",
                table: "ReviewerDecisions",
                sql: "\"ProposalVersion\" > 0");

            migrationBuilder.CreateIndex(
                name: "IX_OperationalHistory_AssessmentId",
                schema: "coastal_operations",
                table: "OperationalHistory",
                column: "AssessmentId");

            migrationBuilder.CreateIndex(
                name: "IX_OperationalHistory_DecisionId",
                schema: "coastal_operations",
                table: "OperationalHistory",
                column: "DecisionId");

            migrationBuilder.CreateIndex(
                name: "IX_OperationalHistory_ProposalId",
                schema: "coastal_operations",
                table: "OperationalHistory",
                column: "ProposalId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_OperationalAlerts_Lifecycle",
                schema: "coastal_operations",
                table: "OperationalAlerts",
                sql: "\"Lifecycle\" IN ('PROPOSED','ACTIVE','RESOLVED','EXPIRED','SUPERSEDED')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_OperationalAlerts_Period",
                schema: "coastal_operations",
                table: "OperationalAlerts",
                sql: "\"ValidUntil\" > \"ValidFrom\"");

            migrationBuilder.AddCheckConstraint(
                name: "CK_OperationalAlerts_Severity",
                schema: "coastal_operations",
                table: "OperationalAlerts",
                sql: "\"Severity\" IN ('LOW','MODERATE','HIGH','CRITICAL')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_OperationalAlerts_Version",
                schema: "coastal_operations",
                table: "OperationalAlerts",
                sql: "\"Version\" > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Assessments_AiDependencyStatus",
                schema: "coastal_operations",
                table: "Assessments",
                sql: "\"AiDependencyStatus\" IN ('NOT_CONNECTED','UNAVAILABLE','AVAILABLE')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Assessments_AiDispatchOutcome",
                schema: "coastal_operations",
                table: "Assessments",
                sql: "\"AiDispatchOutcome\" IN ('NOT_REQUESTED','NOT_STARTED','SUCCEEDED','UNAVAILABLE','INVALID_RESULT')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Assessments_Period",
                schema: "coastal_operations",
                table: "Assessments",
                sql: "\"PeriodEndsAt\" > \"PeriodStartsAt\"");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Assessments_Version",
                schema: "coastal_operations",
                table: "Assessments",
                sql: "\"Version\" > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Assessments_WorkflowStatus",
                schema: "coastal_operations",
                table: "Assessments",
                sql: "\"WorkflowStatus\" IN ('SUBMITTED','PROPOSAL_READY','PENDING_APPROVAL','REVISION_REQUESTED','REJECTED','APPROVED','EXECUTED','BLOCKED','SAFE_FAILURE')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AssessmentProposals_Validity",
                schema: "coastal_operations",
                table: "AssessmentProposals",
                sql: "\"ExpiresAt\" > \"CreatedAt\" AND \"ExpiresAt\" <= \"CreatedAt\" + INTERVAL '30 minutes'");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AlertDecisions_Decision",
                schema: "coastal_operations",
                table: "AlertDecisions",
                sql: "\"Decision\" IN ('PUBLISH','RESOLVE','EXPIRE')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AlertDecisions_ExpectedVersion",
                schema: "coastal_operations",
                table: "AlertDecisions",
                sql: "\"ExpectedVersion\" > 0");

            migrationBuilder.AddForeignKey(
                name: "FK_AlertDecisions_OperationalAlerts_AlertId",
                schema: "coastal_operations",
                table: "AlertDecisions",
                column: "AlertId",
                principalSchema: "coastal_operations",
                principalTable: "OperationalAlerts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_OperationalHistory_AssessmentProposals_ProposalId",
                schema: "coastal_operations",
                table: "OperationalHistory",
                column: "ProposalId",
                principalSchema: "coastal_operations",
                principalTable: "AssessmentProposals",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_OperationalHistory_Assessments_AssessmentId",
                schema: "coastal_operations",
                table: "OperationalHistory",
                column: "AssessmentId",
                principalSchema: "coastal_operations",
                principalTable: "Assessments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_OperationalHistory_ReviewerDecisions_DecisionId",
                schema: "coastal_operations",
                table: "OperationalHistory",
                column: "DecisionId",
                principalSchema: "coastal_operations",
                principalTable: "ReviewerDecisions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AlertDecisions_OperationalAlerts_AlertId",
                schema: "coastal_operations",
                table: "AlertDecisions");

            migrationBuilder.DropForeignKey(
                name: "FK_OperationalHistory_AssessmentProposals_ProposalId",
                schema: "coastal_operations",
                table: "OperationalHistory");

            migrationBuilder.DropForeignKey(
                name: "FK_OperationalHistory_Assessments_AssessmentId",
                schema: "coastal_operations",
                table: "OperationalHistory");

            migrationBuilder.DropForeignKey(
                name: "FK_OperationalHistory_ReviewerDecisions_DecisionId",
                schema: "coastal_operations",
                table: "OperationalHistory");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TargetOperationalStates_State",
                schema: "coastal_operations",
                table: "TargetOperationalStates");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TargetOperationalStates_Version",
                schema: "coastal_operations",
                table: "TargetOperationalStates");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ReviewerDecisions_Decision",
                schema: "coastal_operations",
                table: "ReviewerDecisions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ReviewerDecisions_ProposalVersion",
                schema: "coastal_operations",
                table: "ReviewerDecisions");

            migrationBuilder.DropIndex(
                name: "IX_OperationalHistory_AssessmentId",
                schema: "coastal_operations",
                table: "OperationalHistory");

            migrationBuilder.DropIndex(
                name: "IX_OperationalHistory_DecisionId",
                schema: "coastal_operations",
                table: "OperationalHistory");

            migrationBuilder.DropIndex(
                name: "IX_OperationalHistory_ProposalId",
                schema: "coastal_operations",
                table: "OperationalHistory");

            migrationBuilder.DropCheckConstraint(
                name: "CK_OperationalAlerts_Lifecycle",
                schema: "coastal_operations",
                table: "OperationalAlerts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_OperationalAlerts_Period",
                schema: "coastal_operations",
                table: "OperationalAlerts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_OperationalAlerts_Severity",
                schema: "coastal_operations",
                table: "OperationalAlerts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_OperationalAlerts_Version",
                schema: "coastal_operations",
                table: "OperationalAlerts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Assessments_AiDependencyStatus",
                schema: "coastal_operations",
                table: "Assessments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Assessments_AiDispatchOutcome",
                schema: "coastal_operations",
                table: "Assessments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Assessments_Period",
                schema: "coastal_operations",
                table: "Assessments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Assessments_Version",
                schema: "coastal_operations",
                table: "Assessments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Assessments_WorkflowStatus",
                schema: "coastal_operations",
                table: "Assessments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AssessmentProposals_Validity",
                schema: "coastal_operations",
                table: "AssessmentProposals");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AlertDecisions_Decision",
                schema: "coastal_operations",
                table: "AlertDecisions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AlertDecisions_ExpectedVersion",
                schema: "coastal_operations",
                table: "AlertDecisions");
        }
    }
}

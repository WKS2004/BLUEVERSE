using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Blueverse.CoastalOperations.Data.Migrations
{
    /// <inheritdoc />
    public partial class CoastalOperationsInitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "coastal_operations");

            migrationBuilder.CreateTable(
                name: "AlertDecisions",
                schema: "coastal_operations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AlertId = table.Column<Guid>(type: "uuid", nullable: false),
                    Decision = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExpectedVersion = table.Column<int>(type: "integer", nullable: false),
                    DecidedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AlertDecisions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Assessments",
                schema: "coastal_operations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkflowId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    TargetId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceWorkflowId = table.Column<Guid>(type: "uuid", nullable: true),
                    PeriodStartsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PeriodEndsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Objective = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    WorkflowStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    AiDependencyStatus = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    AiDispatchOutcome = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    AiDispatchRetryable = table.Column<bool>(type: "boolean", nullable: false),
                    InitiatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Assessments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IdempotencyRecords",
                schema: "coastal_operations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Operation = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    Key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    RequestDigest = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ResponseStatusCode = table.Column<int>(type: "integer", nullable: false),
                    ResponseBody = table.Column<string>(type: "character varying(65536)", maxLength: 65536, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IdempotencyRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OperationalHistory",
                schema: "coastal_operations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    TargetId = table.Column<Guid>(type: "uuid", nullable: false),
                    PreviousState = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    NewState = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    AssessmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProposalId = table.Column<Guid>(type: "uuid", nullable: false),
                    DecisionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    CorrelationId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperationalHistory", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OperationsAudit",
                schema: "coastal_operations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ResourceType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ResourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    CorrelationId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperationsAudit", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TargetOperationalStates",
                schema: "coastal_operations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    TargetId = table.Column<Guid>(type: "uuid", nullable: false),
                    State = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TargetOperationalStates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AssessmentProposals",
                schema: "coastal_operations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AssessmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProposalVersion = table.Column<int>(type: "integer", nullable: false),
                    ValidationStatus = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    TargetType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    TargetId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProposedState = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    ExpectedTargetStateVersion = table.Column<int>(type: "integer", nullable: true),
                    RequiresSeparateReviewer = table.Column<bool>(type: "boolean", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssessmentProposals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssessmentProposals_Assessments_AssessmentId",
                        column: x => x.AssessmentId,
                        principalSchema: "coastal_operations",
                        principalTable: "Assessments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OperationalAlerts",
                schema: "coastal_operations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    TargetId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssessmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    Title = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Severity = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Lifecycle = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ValidFrom = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ValidUntil = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperationalAlerts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OperationalAlerts_Assessments_AssessmentId",
                        column: x => x.AssessmentId,
                        principalSchema: "coastal_operations",
                        principalTable: "Assessments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReviewerDecisions",
                schema: "coastal_operations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AssessmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProposalId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProposalVersion = table.Column<int>(type: "integer", nullable: false),
                    Decision = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Explanation = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ExpectedTargetStateVersion = table.Column<int>(type: "integer", nullable: false),
                    WorkflowStatusAfterDecision = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    AppliedOperationalState = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    AppliedOperationalStateVersion = table.Column<int>(type: "integer", nullable: true),
                    DecidedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReviewerDecisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReviewerDecisions_AssessmentProposals_ProposalId",
                        column: x => x.ProposalId,
                        principalSchema: "coastal_operations",
                        principalTable: "AssessmentProposals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReviewerDecisions_Assessments_AssessmentId",
                        column: x => x.AssessmentId,
                        principalSchema: "coastal_operations",
                        principalTable: "Assessments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AlertDecisions_AlertId_DecidedAt",
                schema: "coastal_operations",
                table: "AlertDecisions",
                columns: new[] { "AlertId", "DecidedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AssessmentProposals_AssessmentId_ProposalVersion",
                schema: "coastal_operations",
                table: "AssessmentProposals",
                columns: new[] { "AssessmentId", "ProposalVersion" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Assessments_InitiatedBy_Id",
                schema: "coastal_operations",
                table: "Assessments",
                columns: new[] { "InitiatedBy", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Assessments_WorkflowId",
                schema: "coastal_operations",
                table: "Assessments",
                column: "WorkflowId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Assessments_WorkflowStatus_Id",
                schema: "coastal_operations",
                table: "Assessments",
                columns: new[] { "WorkflowStatus", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_IdempotencyRecords_ActorId_Operation_Key",
                schema: "coastal_operations",
                table: "IdempotencyRecords",
                columns: new[] { "ActorId", "Operation", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OperationalAlerts_AssessmentId",
                schema: "coastal_operations",
                table: "OperationalAlerts",
                column: "AssessmentId");

            migrationBuilder.CreateIndex(
                name: "IX_OperationalAlerts_Lifecycle_Id",
                schema: "coastal_operations",
                table: "OperationalAlerts",
                columns: new[] { "Lifecycle", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_OperationalAlerts_TargetType_TargetId_Lifecycle",
                schema: "coastal_operations",
                table: "OperationalAlerts",
                columns: new[] { "TargetType", "TargetId", "Lifecycle" });

            migrationBuilder.CreateIndex(
                name: "IX_OperationalHistory_TargetType_TargetId_Id",
                schema: "coastal_operations",
                table: "OperationalHistory",
                columns: new[] { "TargetType", "TargetId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_OperationsAudit_ResourceType_ResourceId_CreatedAt",
                schema: "coastal_operations",
                table: "OperationsAudit",
                columns: new[] { "ResourceType", "ResourceId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ReviewerDecisions_AssessmentId_DecidedAt",
                schema: "coastal_operations",
                table: "ReviewerDecisions",
                columns: new[] { "AssessmentId", "DecidedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ReviewerDecisions_ProposalId_ProposalVersion",
                schema: "coastal_operations",
                table: "ReviewerDecisions",
                columns: new[] { "ProposalId", "ProposalVersion" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TargetOperationalStates_TargetType_TargetId",
                schema: "coastal_operations",
                table: "TargetOperationalStates",
                columns: new[] { "TargetType", "TargetId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AlertDecisions",
                schema: "coastal_operations");

            migrationBuilder.DropTable(
                name: "IdempotencyRecords",
                schema: "coastal_operations");

            migrationBuilder.DropTable(
                name: "OperationalAlerts",
                schema: "coastal_operations");

            migrationBuilder.DropTable(
                name: "OperationalHistory",
                schema: "coastal_operations");

            migrationBuilder.DropTable(
                name: "OperationsAudit",
                schema: "coastal_operations");

            migrationBuilder.DropTable(
                name: "ReviewerDecisions",
                schema: "coastal_operations");

            migrationBuilder.DropTable(
                name: "TargetOperationalStates",
                schema: "coastal_operations");

            migrationBuilder.DropTable(
                name: "AssessmentProposals",
                schema: "coastal_operations");

            migrationBuilder.DropTable(
                name: "Assessments",
                schema: "coastal_operations");
        }
    }
}

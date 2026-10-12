using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Blueverse.CoastalOperations.Data.Migrations
{
    /// <inheritdoc />
    public partial class CoastalOperationsDraftLifecycles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_OperationalAlerts_Lifecycle",
                schema: "coastal_operations",
                table: "OperationalAlerts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Assessments_WorkflowStatus",
                schema: "coastal_operations",
                table: "Assessments");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "WithdrawnAt",
                schema: "coastal_operations",
                table: "OperationalAlerts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "WithdrawnBy",
                schema: "coastal_operations",
                table: "OperationalAlerts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Operation",
                schema: "coastal_operations",
                table: "IdempotencyRecords",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(48)",
                oldMaxLength: 48);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CancelledAt",
                schema: "coastal_operations",
                table: "Assessments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CancelledBy",
                schema: "coastal_operations",
                table: "Assessments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_OperationalAlerts_Lifecycle",
                schema: "coastal_operations",
                table: "OperationalAlerts",
                sql: "\"Lifecycle\" IN ('PROPOSED','ACTIVE','RESOLVED','EXPIRED','SUPERSEDED','WITHDRAWN')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_OperationalAlerts_Withdrawal",
                schema: "coastal_operations",
                table: "OperationalAlerts",
                sql: "(\"Lifecycle\" = 'WITHDRAWN' AND \"WithdrawnBy\" IS NOT NULL AND \"WithdrawnAt\" IS NOT NULL) OR (\"Lifecycle\" <> 'WITHDRAWN' AND \"WithdrawnBy\" IS NULL AND \"WithdrawnAt\" IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Assessments_Cancellation",
                schema: "coastal_operations",
                table: "Assessments",
                sql: "(\"WorkflowStatus\" = 'CANCELLED' AND \"CancelledBy\" IS NOT NULL AND \"CancelledAt\" IS NOT NULL) OR (\"WorkflowStatus\" <> 'CANCELLED' AND \"CancelledBy\" IS NULL AND \"CancelledAt\" IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Assessments_WorkflowStatus",
                schema: "coastal_operations",
                table: "Assessments",
                sql: "\"WorkflowStatus\" IN ('DRAFT','SUBMITTED','PROPOSAL_READY','PENDING_APPROVAL','REVISION_REQUESTED','REJECTED','APPROVED','EXECUTED','BLOCKED','SAFE_FAILURE','CANCELLED')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_OperationalAlerts_Lifecycle",
                schema: "coastal_operations",
                table: "OperationalAlerts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_OperationalAlerts_Withdrawal",
                schema: "coastal_operations",
                table: "OperationalAlerts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Assessments_Cancellation",
                schema: "coastal_operations",
                table: "Assessments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Assessments_WorkflowStatus",
                schema: "coastal_operations",
                table: "Assessments");

            migrationBuilder.DropColumn(
                name: "WithdrawnAt",
                schema: "coastal_operations",
                table: "OperationalAlerts");

            migrationBuilder.DropColumn(
                name: "WithdrawnBy",
                schema: "coastal_operations",
                table: "OperationalAlerts");

            migrationBuilder.DropColumn(
                name: "CancelledAt",
                schema: "coastal_operations",
                table: "Assessments");

            migrationBuilder.DropColumn(
                name: "CancelledBy",
                schema: "coastal_operations",
                table: "Assessments");

            migrationBuilder.AlterColumn<string>(
                name: "Operation",
                schema: "coastal_operations",
                table: "IdempotencyRecords",
                type: "character varying(48)",
                maxLength: 48,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64);

            migrationBuilder.AddCheckConstraint(
                name: "CK_OperationalAlerts_Lifecycle",
                schema: "coastal_operations",
                table: "OperationalAlerts",
                sql: "\"Lifecycle\" IN ('PROPOSED','ACTIVE','RESOLVED','EXPIRED','SUPERSEDED')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Assessments_WorkflowStatus",
                schema: "coastal_operations",
                table: "Assessments",
                sql: "\"WorkflowStatus\" IN ('SUBMITTED','PROPOSAL_READY','PENDING_APPROVAL','REVISION_REQUESTED','REJECTED','APPROVED','EXECUTED','BLOCKED','SAFE_FAILURE')");
        }
    }
}

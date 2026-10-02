using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Blueverse.CoastalOperations.Data.Migrations
{
    /// <inheritdoc />
    public partial class CoastalDraftEvidenceRemoval : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_AssessmentEvidence_InspectionStatus",
                schema: "coastal_operations",
                table: "AssessmentEvidence");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ContentDeletedAt",
                schema: "coastal_operations",
                table: "AssessmentEvidence",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RemovedAt",
                schema: "coastal_operations",
                table: "AssessmentEvidence",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_AssessmentEvidence_InspectionStatus",
                schema: "coastal_operations",
                table: "AssessmentEvidence",
                sql: "\"InspectionStatus\" IN ('AVAILABLE','EXPIRED','REMOVED')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AssessmentEvidence_Removal",
                schema: "coastal_operations",
                table: "AssessmentEvidence",
                sql: "(\"InspectionStatus\" = 'REMOVED' AND \"RemovedAt\" IS NOT NULL) OR (\"InspectionStatus\" <> 'REMOVED' AND \"RemovedAt\" IS NULL AND \"ContentDeletedAt\" IS NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Removed private bytes cannot be restored: preserve the tombstone as expired on rollback.
            migrationBuilder.Sql("UPDATE coastal_operations.\"AssessmentEvidence\" SET \"InspectionStatus\" = 'EXPIRED', \"ExpiredAt\" = COALESCE(\"ExpiredAt\", \"RemovedAt\"), \"RemovedAt\" = NULL, \"ContentDeletedAt\" = NULL WHERE \"InspectionStatus\" = 'REMOVED'");
            migrationBuilder.DropCheckConstraint(
                name: "CK_AssessmentEvidence_InspectionStatus",
                schema: "coastal_operations",
                table: "AssessmentEvidence");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AssessmentEvidence_Removal",
                schema: "coastal_operations",
                table: "AssessmentEvidence");

            migrationBuilder.DropColumn(
                name: "ContentDeletedAt",
                schema: "coastal_operations",
                table: "AssessmentEvidence");

            migrationBuilder.DropColumn(
                name: "RemovedAt",
                schema: "coastal_operations",
                table: "AssessmentEvidence");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AssessmentEvidence_InspectionStatus",
                schema: "coastal_operations",
                table: "AssessmentEvidence",
                sql: "\"InspectionStatus\" IN ('AVAILABLE','EXPIRED')");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Blueverse.CoastalOperations.Data.Migrations
{
    /// <inheritdoc />
    public partial class CoastalOperationsEvidenceAndAlertVisibility : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Visibility",
                schema: "coastal_operations",
                table: "OperationalAlerts",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "OPERATIONS");

            migrationBuilder.CreateTable(
                name: "AssessmentEvidence",
                schema: "coastal_operations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AssessmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssessmentVersion = table.Column<int>(type: "integer", nullable: false),
                    UploadedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    MediaType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ByteLength = table.Column<long>(type: "bigint", nullable: false),
                    ContentSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    InspectionStatus = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    UploadedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssessmentEvidence", x => x.Id);
                    table.CheckConstraint("CK_AssessmentEvidence_ByteLength", "\"ByteLength\" BETWEEN 1 AND 5242880");
                    table.CheckConstraint("CK_AssessmentEvidence_Expiry", "\"ExpiresAt\" > \"UploadedAt\"");
                    table.CheckConstraint("CK_AssessmentEvidence_InspectionStatus", "\"InspectionStatus\" IN ('AVAILABLE','EXPIRED')");
                    table.CheckConstraint("CK_AssessmentEvidence_Version", "\"AssessmentVersion\" > 0");
                    table.ForeignKey(
                        name: "FK_AssessmentEvidence_Assessments_AssessmentId",
                        column: x => x.AssessmentId,
                        principalSchema: "coastal_operations",
                        principalTable: "Assessments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OperationalAlerts_Visibility_Lifecycle_ValidFrom_ValidUntil",
                schema: "coastal_operations",
                table: "OperationalAlerts",
                columns: new[] { "Visibility", "Lifecycle", "ValidFrom", "ValidUntil" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_OperationalAlerts_Visibility",
                schema: "coastal_operations",
                table: "OperationalAlerts",
                sql: "\"Visibility\" IN ('OPERATIONS','PUBLIC')");

            migrationBuilder.CreateIndex(
                name: "IX_AssessmentEvidence_AssessmentId_Id",
                schema: "coastal_operations",
                table: "AssessmentEvidence",
                columns: new[] { "AssessmentId", "Id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AssessmentEvidence_AssessmentId_UploadedAt",
                schema: "coastal_operations",
                table: "AssessmentEvidence",
                columns: new[] { "AssessmentId", "UploadedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AssessmentEvidence_InspectionStatus_ExpiresAt",
                schema: "coastal_operations",
                table: "AssessmentEvidence",
                columns: new[] { "InspectionStatus", "ExpiresAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AssessmentEvidence",
                schema: "coastal_operations");

            migrationBuilder.DropIndex(
                name: "IX_OperationalAlerts_Visibility_Lifecycle_ValidFrom_ValidUntil",
                schema: "coastal_operations",
                table: "OperationalAlerts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_OperationalAlerts_Visibility",
                schema: "coastal_operations",
                table: "OperationalAlerts");

            migrationBuilder.DropColumn(
                name: "Visibility",
                schema: "coastal_operations",
                table: "OperationalAlerts");
        }
    }
}

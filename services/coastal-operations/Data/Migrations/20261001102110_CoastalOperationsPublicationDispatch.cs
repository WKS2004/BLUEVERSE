using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Blueverse.CoastalOperations.Data.Migrations
{
    /// <inheritdoc />
    public partial class CoastalOperationsPublicationDispatch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AssessmentDispatches",
                schema: "coastal_operations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AssessmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkflowId = table.Column<Guid>(type: "uuid", nullable: false),
                    PayloadJson = table.Column<string>(type: "jsonb", nullable: false),
                    Status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    LeaseId = table.Column<Guid>(type: "uuid", nullable: true),
                    LeaseUntil = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    NextAttemptAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    CorrelationId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssessmentDispatches", x => x.Id);
                    table.CheckConstraint("CK_AssessmentDispatches_Attempts", "\"Attempts\" BETWEEN 0 AND 3");
                    table.CheckConstraint("CK_AssessmentDispatches_Lease", "(\"Status\" = 'LEASED' AND \"LeaseId\" IS NOT NULL AND \"LeaseUntil\" IS NOT NULL) OR (\"Status\" <> 'LEASED' AND \"LeaseId\" IS NULL AND \"LeaseUntil\" IS NULL)");
                    table.CheckConstraint("CK_AssessmentDispatches_Payload", "jsonb_typeof(\"PayloadJson\") = 'object'");
                    table.CheckConstraint("CK_AssessmentDispatches_Status", "\"Status\" IN ('NOT_CONNECTED','PENDING','LEASED','ACCEPTED','UNAVAILABLE','SAFE_FAILURE')");
                    table.CheckConstraint("CK_AssessmentDispatches_Version", "\"Version\" > 0");
                    table.ForeignKey(
                        name: "FK_AssessmentDispatches_Assessments_AssessmentId",
                        column: x => x.AssessmentId,
                        principalSchema: "coastal_operations",
                        principalTable: "Assessments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AssessmentDispatches_AssessmentId",
                schema: "coastal_operations",
                table: "AssessmentDispatches",
                column: "AssessmentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AssessmentDispatches_Status_NextAttemptAt",
                schema: "coastal_operations",
                table: "AssessmentDispatches",
                columns: new[] { "Status", "NextAttemptAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AssessmentDispatches",
                schema: "coastal_operations");
        }
    }
}

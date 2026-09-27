using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Blueverse.CoastalPlanner.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCoastalPlannerSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "coastal_planner");

            migrationBuilder.CreateTable(
                name: "biodiversity_predictions_cache",
                schema: "coastal_planner",
                columns: table => new
                {
                    PredictionId = table.Column<Guid>(type: "uuid", nullable: false),
                    DestinationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActivityId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SpeciesDataJson = table.Column<string>(type: "text", nullable: true),
                    ModelVersion = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    InferenceTimestampUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Limitations = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_biodiversity_predictions_cache", x => x.PredictionId);
                });

            migrationBuilder.CreateTable(
                name: "itineraries",
                schema: "coastal_planner",
                columns: table => new
                {
                    ItineraryId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    StartsAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndsAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ConcurrencyVersion = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_itineraries", x => x.ItineraryId);
                    table.CheckConstraint("CK_itineraries_date_range", "\"EndsAtUtc\" > \"StartsAtUtc\"");
                });

            migrationBuilder.CreateTable(
                name: "planning_workflows",
                schema: "coastal_planner",
                columns: table => new
                {
                    WorkflowId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkflowType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    InitiatorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Objective = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ResultSummary = table.Column<string>(type: "text", nullable: true),
                    FailureReason = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_planning_workflows", x => x.WorkflowId);
                    table.CheckConstraint("CK_planning_workflows_status", "\"Status\" IN ('PENDING', 'PROCESSING', 'COMPLETED', 'FAILED')");
                });

            migrationBuilder.CreateTable(
                name: "itinerary_items",
                schema: "coastal_planner",
                columns: table => new
                {
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItineraryId = table.Column<Guid>(type: "uuid", nullable: false),
                    DestinationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActivityId = table.Column<Guid>(type: "uuid", nullable: false),
                    OfferingId = table.Column<Guid>(type: "uuid", nullable: true),
                    Title = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    OrderIndex = table.Column<int>(type: "integer", nullable: false),
                    ScheduledStartUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ScheduledEndUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastSuitabilityStatus = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    LastAvailabilityStatus = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    LastOperationalStatus = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    AdvisoryNote = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_itinerary_items", x => x.ItemId);
                    table.CheckConstraint("CK_itinerary_items_date_range", "\"ScheduledEndUtc\" > \"ScheduledStartUtc\"");
                    table.CheckConstraint("CK_itinerary_items_order_non_negative", "\"OrderIndex\" >= 0");
                    table.ForeignKey(
                        name: "FK_itinerary_items_itineraries_ItineraryId",
                        column: x => x.ItineraryId,
                        principalSchema: "coastal_planner",
                        principalTable: "itineraries",
                        principalColumn: "ItineraryId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "recommendations",
                schema: "coastal_planner",
                columns: table => new
                {
                    RecommendationId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkflowId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetDestinationId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartsAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndsAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DurationHours = table.Column<int>(type: "integer", nullable: false),
                    ExperienceLevel = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IncludeBiodiversityContext = table.Column<bool>(type: "boolean", nullable: false),
                    CandidatesJson = table.Column<string>(type: "text", nullable: true),
                    UncertaintyNotesJson = table.Column<string>(type: "text", nullable: true),
                    ExcludedCandidatesCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recommendations", x => x.RecommendationId);
                    table.CheckConstraint("CK_recommendations_date_range", "\"EndsAtUtc\" > \"StartsAtUtc\"");
                    table.CheckConstraint("CK_recommendations_duration_positive", "\"DurationHours\" > 0");
                    table.ForeignKey(
                        name: "FK_recommendations_planning_workflows_WorkflowId",
                        column: x => x.WorkflowId,
                        principalSchema: "coastal_planner",
                        principalTable: "planning_workflows",
                        principalColumn: "WorkflowId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_biodiversity_predictions_cache_DestinationId_ActivityId",
                schema: "coastal_planner",
                table: "biodiversity_predictions_cache",
                columns: new[] { "DestinationId", "ActivityId" });

            migrationBuilder.CreateIndex(
                name: "IX_biodiversity_predictions_cache_ExpiresAtUtc",
                schema: "coastal_planner",
                table: "biodiversity_predictions_cache",
                column: "ExpiresAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_itineraries_CreatedAtUtc",
                schema: "coastal_planner",
                table: "itineraries",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_itineraries_OwnerUserId",
                schema: "coastal_planner",
                table: "itineraries",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_itinerary_items_ItineraryId_OrderIndex",
                schema: "coastal_planner",
                table: "itinerary_items",
                columns: new[] { "ItineraryId", "OrderIndex" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_planning_workflows_CreatedAtUtc",
                schema: "coastal_planner",
                table: "planning_workflows",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_planning_workflows_InitiatorUserId",
                schema: "coastal_planner",
                table: "planning_workflows",
                column: "InitiatorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_planning_workflows_Status",
                schema: "coastal_planner",
                table: "planning_workflows",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_recommendations_UserId",
                schema: "coastal_planner",
                table: "recommendations",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_recommendations_WorkflowId",
                schema: "coastal_planner",
                table: "recommendations",
                column: "WorkflowId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "biodiversity_predictions_cache",
                schema: "coastal_planner");

            migrationBuilder.DropTable(
                name: "itinerary_items",
                schema: "coastal_planner");

            migrationBuilder.DropTable(
                name: "recommendations",
                schema: "coastal_planner");

            migrationBuilder.DropTable(
                name: "itineraries",
                schema: "coastal_planner");

            migrationBuilder.DropTable(
                name: "planning_workflows",
                schema: "coastal_planner");
        }
    }
}

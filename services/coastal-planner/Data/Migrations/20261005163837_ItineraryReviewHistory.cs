using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Blueverse.CoastalPlanner.Data.Migrations
{
    /// <inheritdoc />
    public partial class ItineraryReviewHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Outcome",
                schema: "coastal_planner",
                table: "recommendations",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "LEGACY_RESULT");

            migrationBuilder.AddColumn<string>(
                name: "TimeZone",
                schema: "coastal_planner",
                table: "itinerary_items",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "Asia/Colombo");

            migrationBuilder.AddColumn<string>(
                name: "TimeZone",
                schema: "coastal_planner",
                table: "itineraries",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "Asia/Colombo");

            migrationBuilder.CreateTable(
                name: "itinerary_evaluations",
                schema: "coastal_planner",
                columns: table => new
                {
                    EvaluationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItineraryId = table.Column<Guid>(type: "uuid", nullable: false),
                    EvaluatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ResultJson = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_itinerary_evaluations", x => x.EvaluationId);
                    table.ForeignKey(
                        name: "FK_itinerary_evaluations_itineraries_ItineraryId",
                        column: x => x.ItineraryId,
                        principalSchema: "coastal_planner",
                        principalTable: "itineraries",
                        principalColumn: "ItineraryId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_itinerary_evaluations_ItineraryId_EvaluatedAtUtc",
                schema: "coastal_planner",
                table: "itinerary_evaluations",
                columns: new[] { "ItineraryId", "EvaluatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "itinerary_evaluations",
                schema: "coastal_planner");

            migrationBuilder.DropColumn(
                name: "Outcome",
                schema: "coastal_planner",
                table: "recommendations");

            migrationBuilder.DropColumn(
                name: "TimeZone",
                schema: "coastal_planner",
                table: "itinerary_items");

            migrationBuilder.DropColumn(
                name: "TimeZone",
                schema: "coastal_planner",
                table: "itineraries");
        }
    }
}

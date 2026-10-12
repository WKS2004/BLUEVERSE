using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Blueverse.CoastalPlanner.Data.Migrations
{
    /// <inheritdoc />
    public partial class BiodiversityCacheModelVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_biodiversity_predictions_cache_DestinationId_ActivityId",
                schema: "coastal_planner",
                table: "biodiversity_predictions_cache");

            migrationBuilder.AddColumn<double>(
                name: "FitScore",
                schema: "coastal_planner",
                table: "itinerary_items",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                schema: "coastal_planner",
                table: "itineraries",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Draft");

            migrationBuilder.AddColumn<string>(
                name: "UncertaintyText",
                schema: "coastal_planner",
                table: "biodiversity_predictions_cache",
                type: "text",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_itineraries_status",
                schema: "coastal_planner",
                table: "itineraries",
                sql: "\"Status\" IN ('Draft','Confirmed')");

            migrationBuilder.CreateIndex(
                name: "IX_biodiversity_predictions_cache_DestinationId_ActivityId_Mod~",
                schema: "coastal_planner",
                table: "biodiversity_predictions_cache",
                columns: new[] { "DestinationId", "ActivityId", "ModelVersion" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_itineraries_status",
                schema: "coastal_planner",
                table: "itineraries");

            migrationBuilder.DropIndex(
                name: "IX_biodiversity_predictions_cache_DestinationId_ActivityId_Mod~",
                schema: "coastal_planner",
                table: "biodiversity_predictions_cache");

            migrationBuilder.DropColumn(
                name: "FitScore",
                schema: "coastal_planner",
                table: "itinerary_items");

            migrationBuilder.DropColumn(
                name: "Status",
                schema: "coastal_planner",
                table: "itineraries");

            migrationBuilder.DropColumn(
                name: "UncertaintyText",
                schema: "coastal_planner",
                table: "biodiversity_predictions_cache");

            migrationBuilder.CreateIndex(
                name: "IX_biodiversity_predictions_cache_DestinationId_ActivityId",
                schema: "coastal_planner",
                table: "biodiversity_predictions_cache",
                columns: new[] { "DestinationId", "ActivityId" });
        }
    }
}

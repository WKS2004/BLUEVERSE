using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Blueverse.CoastalPlanner.Data.Migrations
{
    /// <inheritdoc />
    public partial class BiodiversityCacheProviderProvenance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Provider",
                schema: "coastal_planner",
                table: "biodiversity_predictions_cache",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Provider",
                schema: "coastal_planner",
                table: "biodiversity_predictions_cache");
        }
    }
}

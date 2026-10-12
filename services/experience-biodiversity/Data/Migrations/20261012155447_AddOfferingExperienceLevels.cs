using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Blueverse.ExperienceBiodiversity.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOfferingExperienceLevels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string[]>(
                name: "ExperienceLevels",
                table: "offerings",
                type: "text[]",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExperienceLevels",
                table: "offerings");
        }
    }
}

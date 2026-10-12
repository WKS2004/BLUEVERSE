using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Blueverse.ExperienceBiodiversity.Data.Migrations
{
    /// <inheritdoc />
    public partial class MoveExperienceBiodiversityIntoOwnedSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "experience_biodiversity");

            migrationBuilder.RenameTable(
                name: "schedules",
                newName: "schedules",
                newSchema: "experience_biodiversity");

            migrationBuilder.RenameTable(
                name: "offerings",
                newName: "offerings",
                newSchema: "experience_biodiversity");

            migrationBuilder.RenameTable(
                name: "favourites",
                newName: "favourites",
                newSchema: "experience_biodiversity");

            migrationBuilder.RenameTable(
                name: "destinations",
                newName: "destinations",
                newSchema: "experience_biodiversity");

            migrationBuilder.RenameTable(
                name: "activities",
                newName: "activities",
                newSchema: "experience_biodiversity");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(
                name: "schedules",
                schema: "experience_biodiversity",
                newName: "schedules");

            migrationBuilder.RenameTable(
                name: "offerings",
                schema: "experience_biodiversity",
                newName: "offerings");

            migrationBuilder.RenameTable(
                name: "favourites",
                schema: "experience_biodiversity",
                newName: "favourites");

            migrationBuilder.RenameTable(
                name: "destinations",
                schema: "experience_biodiversity",
                newName: "destinations");

            migrationBuilder.RenameTable(
                name: "activities",
                schema: "experience_biodiversity",
                newName: "activities");
        }
    }
}

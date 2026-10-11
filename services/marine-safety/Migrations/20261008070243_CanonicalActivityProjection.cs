using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Blueverse.MarineSafety.Migrations
{
    /// <inheritdoc />
    public partial class CanonicalActivityProjection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MarineActivities_Name",
                table: "MarineActivities");

            migrationBuilder.AlterColumn<string>(
                name: "ActivityType",
                table: "MarineActivities",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64);

            migrationBuilder.CreateIndex(
                name: "IX_MarineActivities_Name",
                table: "MarineActivities",
                column: "Name");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MarineActivities_Name",
                table: "MarineActivities");

            migrationBuilder.AlterColumn<string>(
                name: "ActivityType",
                table: "MarineActivities",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.CreateIndex(
                name: "IX_MarineActivities_Name",
                table: "MarineActivities",
                column: "Name",
                unique: true);
        }
    }
}

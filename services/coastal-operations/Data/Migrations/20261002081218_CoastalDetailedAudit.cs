using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Blueverse.CoastalOperations.Data.Migrations
{
    /// <inheritdoc />
    public partial class CoastalDetailedAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ActorName",
                schema: "coastal_operations",
                table: "OperationsAudit",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ActorRolesJson",
                schema: "coastal_operations",
                table: "OperationsAudit",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "ChangesJson",
                schema: "coastal_operations",
                table: "OperationsAudit",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "RecordTitle",
                schema: "coastal_operations",
                table: "OperationsAudit",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Summary",
                schema: "coastal_operations",
                table: "OperationsAudit",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_OperationsAudit_Snapshots",
                schema: "coastal_operations",
                table: "OperationsAudit",
                sql: "jsonb_typeof(\"ActorRolesJson\") = 'array' AND jsonb_typeof(\"ChangesJson\") = 'array'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_OperationsAudit_Snapshots",
                schema: "coastal_operations",
                table: "OperationsAudit");

            migrationBuilder.DropColumn(
                name: "ActorName",
                schema: "coastal_operations",
                table: "OperationsAudit");

            migrationBuilder.DropColumn(
                name: "ActorRolesJson",
                schema: "coastal_operations",
                table: "OperationsAudit");

            migrationBuilder.DropColumn(
                name: "ChangesJson",
                schema: "coastal_operations",
                table: "OperationsAudit");

            migrationBuilder.DropColumn(
                name: "RecordTitle",
                schema: "coastal_operations",
                table: "OperationsAudit");

            migrationBuilder.DropColumn(
                name: "Summary",
                schema: "coastal_operations",
                table: "OperationsAudit");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Blueverse.CoastalOperations.Data.Migrations
{
    /// <inheritdoc />
    public partial class PersistComponentDependencyResults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ComponentDependenciesJson",
                schema: "coastal_operations",
                table: "Assessments",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'[]'::jsonb");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Assessments_ComponentDependencies",
                schema: "coastal_operations",
                table: "Assessments",
                sql: "jsonb_typeof(\"ComponentDependenciesJson\") = 'array'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Assessments_ComponentDependencies",
                schema: "coastal_operations",
                table: "Assessments");

            migrationBuilder.DropColumn(
                name: "ComponentDependenciesJson",
                schema: "coastal_operations",
                table: "Assessments");
        }
    }
}

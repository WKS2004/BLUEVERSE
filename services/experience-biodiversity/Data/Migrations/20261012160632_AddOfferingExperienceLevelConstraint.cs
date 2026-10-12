using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Blueverse.ExperienceBiodiversity.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOfferingExperienceLevelConstraint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "CK_offerings_experience_levels",
                table: "offerings",
                sql: "\"ExperienceLevels\" IS NULL OR (cardinality(\"ExperienceLevels\") <= 3 AND \"ExperienceLevels\" <@ ARRAY['BEGINNER','INTERMEDIATE','ADVANCED']::text[] AND cardinality(\"ExperienceLevels\") = (CASE WHEN 'BEGINNER' = ANY(\"ExperienceLevels\") THEN 1 ELSE 0 END + CASE WHEN 'INTERMEDIATE' = ANY(\"ExperienceLevels\") THEN 1 ELSE 0 END + CASE WHEN 'ADVANCED' = ANY(\"ExperienceLevels\") THEN 1 ELSE 0 END))");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_offerings_experience_levels",
                table: "offerings");
        }
    }
}

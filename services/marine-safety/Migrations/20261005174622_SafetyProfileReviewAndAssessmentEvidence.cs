using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Blueverse.MarineSafety.Migrations
{
    /// <inheritdoc />
    public partial class SafetyProfileReviewAndAssessmentEvidence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ActivityName",
                table: "SuitabilityAssessments",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CautionSwellHeight",
                table: "SuitabilityAssessments",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CautionWaveHeight",
                table: "SuitabilityAssessments",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CautionWindSpeed",
                table: "SuitabilityAssessments",
                type: "numeric(6,2)",
                precision: 6,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string[]>(
                name: "ConditionMissingFields",
                table: "SuitabilityAssessments",
                type: "text[]",
                nullable: false,
                defaultValue: new string[0]);

            migrationBuilder.AddColumn<DateTime>(
                name: "ConditionRetrievedAt",
                table: "SuitabilityAssessments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EvidenceCompleteness",
                table: "SuitabilityAssessments",
                type: "character varying(24)",
                maxLength: 24,
                nullable: false,
                defaultValue: "LEGACY_INCOMPLETE");

            migrationBuilder.AddColumn<DateTime>(
                name: "ForecastTime",
                table: "SuitabilityAssessments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MaxSwellHeight",
                table: "SuitabilityAssessments",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MaxWaveHeight",
                table: "SuitabilityAssessments",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MaxWindSpeed",
                table: "SuitabilityAssessments",
                type: "numeric(6,2)",
                precision: 6,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Rain",
                table: "SuitabilityAssessments",
                type: "numeric(6,2)",
                precision: 6,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SwellCriteriaRationale",
                table: "SuitabilityAssessments",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SwellCriteriaSource",
                table: "SuitabilityAssessments",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SwellHeight",
                table: "SuitabilityAssessments",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WaveCriteriaRationale",
                table: "SuitabilityAssessments",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WaveCriteriaSource",
                table: "SuitabilityAssessments",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "WaveHeight",
                table: "SuitabilityAssessments",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WeatherCode",
                table: "SuitabilityAssessments",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WindCriteriaRationale",
                table: "SuitabilityAssessments",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WindCriteriaSource",
                table: "SuitabilityAssessments",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "WindSpeed",
                table: "SuitabilityAssessments",
                type: "numeric(6,2)",
                precision: 6,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedByUserId",
                table: "SafetyProfiles",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EffectiveFrom",
                table: "SafetyProfiles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EffectiveTo",
                table: "SafetyProfiles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReviewedAt",
                table: "SafetyProfiles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReviewedByUserId",
                table: "SafetyProfiles",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SwellCriteriaRationale",
                table: "SafetyProfiles",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SwellCriteriaSource",
                table: "SafetyProfiles",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WaveCriteriaRationale",
                table: "SafetyProfiles",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WaveCriteriaSource",
                table: "SafetyProfiles",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WindCriteriaRationale",
                table: "SafetyProfiles",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WindCriteriaSource",
                table: "SafetyProfiles",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SafetyProfiles_ActivityId_Version",
                table: "SafetyProfiles",
                columns: new[] { "ActivityId", "Version" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SafetyProfiles_ActivityId_Version",
                table: "SafetyProfiles");

            migrationBuilder.DropColumn(
                name: "ActivityName",
                table: "SuitabilityAssessments");

            migrationBuilder.DropColumn(
                name: "CautionSwellHeight",
                table: "SuitabilityAssessments");

            migrationBuilder.DropColumn(
                name: "CautionWaveHeight",
                table: "SuitabilityAssessments");

            migrationBuilder.DropColumn(
                name: "CautionWindSpeed",
                table: "SuitabilityAssessments");

            migrationBuilder.DropColumn(
                name: "ConditionMissingFields",
                table: "SuitabilityAssessments");

            migrationBuilder.DropColumn(
                name: "ConditionRetrievedAt",
                table: "SuitabilityAssessments");

            migrationBuilder.DropColumn(
                name: "EvidenceCompleteness",
                table: "SuitabilityAssessments");

            migrationBuilder.DropColumn(
                name: "ForecastTime",
                table: "SuitabilityAssessments");

            migrationBuilder.DropColumn(
                name: "MaxSwellHeight",
                table: "SuitabilityAssessments");

            migrationBuilder.DropColumn(
                name: "MaxWaveHeight",
                table: "SuitabilityAssessments");

            migrationBuilder.DropColumn(
                name: "MaxWindSpeed",
                table: "SuitabilityAssessments");

            migrationBuilder.DropColumn(
                name: "Rain",
                table: "SuitabilityAssessments");

            migrationBuilder.DropColumn(
                name: "SwellCriteriaRationale",
                table: "SuitabilityAssessments");

            migrationBuilder.DropColumn(
                name: "SwellCriteriaSource",
                table: "SuitabilityAssessments");

            migrationBuilder.DropColumn(
                name: "SwellHeight",
                table: "SuitabilityAssessments");

            migrationBuilder.DropColumn(
                name: "WaveCriteriaRationale",
                table: "SuitabilityAssessments");

            migrationBuilder.DropColumn(
                name: "WaveCriteriaSource",
                table: "SuitabilityAssessments");

            migrationBuilder.DropColumn(
                name: "WaveHeight",
                table: "SuitabilityAssessments");

            migrationBuilder.DropColumn(
                name: "WeatherCode",
                table: "SuitabilityAssessments");

            migrationBuilder.DropColumn(
                name: "WindCriteriaRationale",
                table: "SuitabilityAssessments");

            migrationBuilder.DropColumn(
                name: "WindCriteriaSource",
                table: "SuitabilityAssessments");

            migrationBuilder.DropColumn(
                name: "WindSpeed",
                table: "SuitabilityAssessments");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "SafetyProfiles");

            migrationBuilder.DropColumn(
                name: "EffectiveFrom",
                table: "SafetyProfiles");

            migrationBuilder.DropColumn(
                name: "EffectiveTo",
                table: "SafetyProfiles");

            migrationBuilder.DropColumn(
                name: "ReviewedAt",
                table: "SafetyProfiles");

            migrationBuilder.DropColumn(
                name: "ReviewedByUserId",
                table: "SafetyProfiles");

            migrationBuilder.DropColumn(
                name: "SwellCriteriaRationale",
                table: "SafetyProfiles");

            migrationBuilder.DropColumn(
                name: "SwellCriteriaSource",
                table: "SafetyProfiles");

            migrationBuilder.DropColumn(
                name: "WaveCriteriaRationale",
                table: "SafetyProfiles");

            migrationBuilder.DropColumn(
                name: "WaveCriteriaSource",
                table: "SafetyProfiles");

            migrationBuilder.DropColumn(
                name: "WindCriteriaRationale",
                table: "SafetyProfiles");

            migrationBuilder.DropColumn(
                name: "WindCriteriaSource",
                table: "SafetyProfiles");
        }
    }
}

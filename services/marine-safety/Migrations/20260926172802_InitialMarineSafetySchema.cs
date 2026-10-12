using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Blueverse.MarineSafety.Migrations
{
    /// <inheritdoc />
    public partial class InitialMarineSafetySchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConditionSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Latitude = table.Column<decimal>(type: "numeric(8,5)", precision: 8, scale: 5, nullable: false),
                    Longitude = table.Column<decimal>(type: "numeric(9,5)", precision: 9, scale: 5, nullable: false),
                    ForecastTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RetrievedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    WindSpeed = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: true),
                    WaveHeight = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    SwellHeight = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    Rain = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: true),
                    WeatherCode = table.Column<int>(type: "integer", nullable: true),
                    Source = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    FreshnessStatus = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    MissingFields = table.Column<string[]>(type: "text[]", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConditionSnapshots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MarineActivities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ActivityType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarineActivities", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SafetyProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ActivityId = table.Column<Guid>(type: "uuid", nullable: false),
                    MaxWindSpeed = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: false),
                    MaxWaveHeight = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    MaxSwellHeight = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    CautionWindSpeed = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: true),
                    CautionWaveHeight = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    CautionSwellHeight = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SafetyProfiles", x => x.Id);
                    table.CheckConstraint("CK_SafetyProfiles_CautionSwellHeight_BelowMax", "\"CautionSwellHeight\" IS NULL OR \"CautionSwellHeight\" < \"MaxSwellHeight\"");
                    table.CheckConstraint("CK_SafetyProfiles_CautionWaveHeight_BelowMax", "\"CautionWaveHeight\" IS NULL OR \"CautionWaveHeight\" < \"MaxWaveHeight\"");
                    table.CheckConstraint("CK_SafetyProfiles_CautionWindSpeed_BelowMax", "\"CautionWindSpeed\" IS NULL OR \"CautionWindSpeed\" < \"MaxWindSpeed\"");
                    table.CheckConstraint("CK_SafetyProfiles_MaxSwellHeight_Positive", "\"MaxSwellHeight\" > 0");
                    table.CheckConstraint("CK_SafetyProfiles_MaxWaveHeight_Positive", "\"MaxWaveHeight\" > 0");
                    table.CheckConstraint("CK_SafetyProfiles_MaxWindSpeed_Positive", "\"MaxWindSpeed\" > 0");
                    table.ForeignKey(
                        name: "FK_SafetyProfiles_MarineActivities_ActivityId",
                        column: x => x.ActivityId,
                        principalTable: "MarineActivities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SuitabilityAssessments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ActivityId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProfileVersion = table.Column<int>(type: "integer", nullable: false),
                    Latitude = table.Column<decimal>(type: "numeric(8,5)", precision: 8, scale: 5, nullable: false),
                    Longitude = table.Column<decimal>(type: "numeric(9,5)", precision: 9, scale: 5, nullable: false),
                    RequestedTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EvaluatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Result = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Violations = table.Column<string[]>(type: "text[]", nullable: false),
                    CautionFactors = table.Column<string[]>(type: "text[]", nullable: false),
                    MissingFields = table.Column<string[]>(type: "text[]", nullable: false),
                    Source = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    FreshnessStatus = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ConditionSnapshotId = table.Column<Guid>(type: "uuid", nullable: false),
                    SafetyProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SuitabilityAssessments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SuitabilityAssessments_MarineActivities_ActivityId",
                        column: x => x.ActivityId,
                        principalTable: "MarineActivities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "MarineActivities",
                columns: new[] { "Id", "ActivityType", "CreatedAt", "IsActive", "Name", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid("33333333-3333-3333-3333-333333333301"), "Surfing", new DateTime(2026, 9, 26, 0, 0, 0, 0, DateTimeKind.Utc), true, "Surfing", null },
                    { new Guid("33333333-3333-3333-3333-333333333302"), "Snorkeling", new DateTime(2026, 9, 26, 0, 0, 0, 0, DateTimeKind.Utc), true, "Snorkeling", null },
                    { new Guid("33333333-3333-3333-3333-333333333303"), "Diving", new DateTime(2026, 9, 26, 0, 0, 0, 0, DateTimeKind.Utc), true, "Scuba Diving", null },
                    { new Guid("33333333-3333-3333-3333-333333333304"), "BoatTour", new DateTime(2026, 9, 26, 0, 0, 0, 0, DateTimeKind.Utc), true, "Whale and Dolphin Watching", null },
                    { new Guid("33333333-3333-3333-3333-333333333305"), "BoatTour", new DateTime(2026, 9, 26, 0, 0, 0, 0, DateTimeKind.Utc), true, "Coastal Boat Tour", null }
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConditionSnapshots_Latitude_Longitude_ForecastTime",
                table: "ConditionSnapshots",
                columns: new[] { "Latitude", "Longitude", "ForecastTime" });

            migrationBuilder.CreateIndex(
                name: "IX_ConditionSnapshots_Latitude_Longitude_RetrievedAt",
                table: "ConditionSnapshots",
                columns: new[] { "Latitude", "Longitude", "RetrievedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ConditionSnapshots_RetrievedAt",
                table: "ConditionSnapshots",
                column: "RetrievedAt");

            migrationBuilder.CreateIndex(
                name: "IX_MarineActivities_Name",
                table: "MarineActivities",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SafetyProfiles_ActivityId_IsActive",
                table: "SafetyProfiles",
                columns: new[] { "ActivityId", "IsActive" },
                unique: true,
                filter: "\"IsActive\"");

            migrationBuilder.CreateIndex(
                name: "IX_SuitabilityAssessments_ActivityId_RequestedTime",
                table: "SuitabilityAssessments",
                columns: new[] { "ActivityId", "RequestedTime" });

            migrationBuilder.CreateIndex(
                name: "IX_SuitabilityAssessments_EvaluatedAt",
                table: "SuitabilityAssessments",
                column: "EvaluatedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConditionSnapshots");

            migrationBuilder.DropTable(
                name: "SafetyProfiles");

            migrationBuilder.DropTable(
                name: "SuitabilityAssessments");

            migrationBuilder.DropTable(
                name: "MarineActivities");
        }
    }
}

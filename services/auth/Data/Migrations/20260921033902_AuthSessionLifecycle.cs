using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Blueverse.Auth.Data.Migrations
{
    /// <inheritdoc />
    public partial class AuthSessionLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ExpiresAt",
                table: "UserSessions",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<bool>(
                name: "RememberMe",
                table: "UserSessions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "RevocationReason",
                table: "UserSessions",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DeviceInstallations",
                columns: table => new
                {
                    DeviceId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    DeviceKeyHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    IsLegacy = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastSeenAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RevokedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceInstallations", x => x.DeviceId);
                });

            migrationBuilder.CreateTable(
                name: "RefreshTokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserSessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    FamilyId = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    IssuedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ConsumedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RevokedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReplacedByTokenId = table.Column<Guid>(type: "uuid", nullable: true),
                    RevocationReason = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefreshTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RefreshTokens_UserSessions_UserSessionId",
                        column: x => x.UserSessionId,
                        principalTable: "UserSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Existing installations used DeviceId directly on UserSessions. Seed
            // a legacy installation row for each value before adding the FK.
            migrationBuilder.Sql("""
                INSERT INTO "DeviceInstallations" ("DeviceId", "DeviceKeyHash", "IsLegacy", "CreatedAt", "LastSeenAt", "RevokedAt")
                SELECT "DeviceId", NULL, TRUE, MIN("CreatedAt"), MAX("LastSeenAt"), NULL
                FROM "UserSessions"
                GROUP BY "DeviceId";
                """);

            // The new non-null column receives a meaningful expiry for sessions
            // created by the previous schema instead of the CLR minimum date.
            migrationBuilder.Sql("""
                UPDATE "UserSessions"
                SET "ExpiresAt" = "CreatedAt" + INTERVAL '1 day'
                WHERE "ExpiresAt" = TIMESTAMPTZ '0001-01-01 00:00:00+00';
                """);

            migrationBuilder.CreateIndex(
                name: "IX_UserSessions_UserId_RevokedAt_ExpiresAt",
                table: "UserSessions",
                columns: new[] { "UserId", "RevokedAt", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_DeviceInstallations_DeviceKeyHash",
                table: "DeviceInstallations",
                column: "DeviceKeyHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_FamilyId",
                table: "RefreshTokens",
                column: "FamilyId");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_TokenHash",
                table: "RefreshTokens",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_UserSessionId_ExpiresAt",
                table: "RefreshTokens",
                columns: new[] { "UserSessionId", "ExpiresAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_UserSessions_DeviceInstallations_DeviceId",
                table: "UserSessions",
                column: "DeviceId",
                principalTable: "DeviceInstallations",
                principalColumn: "DeviceId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserSessions_DeviceInstallations_DeviceId",
                table: "UserSessions");

            migrationBuilder.DropTable(
                name: "DeviceInstallations");

            migrationBuilder.DropTable(
                name: "RefreshTokens");

            migrationBuilder.DropIndex(
                name: "IX_UserSessions_UserId_RevokedAt_ExpiresAt",
                table: "UserSessions");

            migrationBuilder.DropColumn(
                name: "ExpiresAt",
                table: "UserSessions");

            migrationBuilder.DropColumn(
                name: "RememberMe",
                table: "UserSessions");

            migrationBuilder.DropColumn(
                name: "RevocationReason",
                table: "UserSessions");
        }
    }
}

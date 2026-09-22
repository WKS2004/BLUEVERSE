using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Blueverse.Auth.Data.Migrations
{
    /// <inheritdoc />
    public partial class ActiveSessionsAndSessionLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RefreshTokens_UserSessions_UserSessionId",
                table: "RefreshTokens");

            migrationBuilder.DropForeignKey(
                name: "FK_UserSessions_DeviceInstallations_DeviceId",
                table: "UserSessions");

            migrationBuilder.DropForeignKey(
                name: "FK_UserSessions_Users_UserId",
                table: "UserSessions");

            migrationBuilder.DropPrimaryKey(
                name: "PK_UserSessions",
                table: "UserSessions");

            migrationBuilder.DropIndex(
                name: "IX_UserSessions_DeviceId_RevokedAt",
                table: "UserSessions");

            migrationBuilder.DropIndex(
                name: "IX_UserSessions_UserId_RevokedAt_ExpiresAt",
                table: "UserSessions");

            migrationBuilder.CreateTable(
                name: "UserSessionLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    SessionVersion = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastSeenAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RememberMe = table.Column<bool>(type: "boolean", nullable: false),
                    EndedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndReason = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserSessionLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserSessionLogs_DeviceInstallations_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "DeviceInstallations",
                        principalColumn: "DeviceId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserSessionLogs_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AlterColumn<Guid>(
                name: "UserSessionId",
                table: "RefreshTokens",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "UserSessionLogId",
                table: "RefreshTokens",
                type: "uuid",
                nullable: true);

            // Preserve existing lifecycle history before the old status columns
            // are removed. Only rows that are already revoked or expired move
            // to the archive; valid rows remain active.
            migrationBuilder.Sql("""
                INSERT INTO "UserSessionLogs"
                    ("Id", "UserId", "DeviceId", "SessionVersion", "CreatedAt", "LastSeenAt", "ExpiresAt", "RememberMe", "EndedAt", "EndReason")
                SELECT
                    "Id", "UserId", "DeviceId", "SessionVersion", "CreatedAt", "LastSeenAt", "ExpiresAt", "RememberMe",
                    COALESCE("RevokedAt", "ExpiresAt"),
                    COALESCE(NULLIF("RevocationReason", ''), 'session-expired')
                FROM "UserSessions"
                WHERE "RevokedAt" IS NOT NULL OR "ExpiresAt" <= CURRENT_TIMESTAMP;
                """);

            // Refresh-token rows survive session archival. Their association is
            // moved from the active table to the matching log row.
            migrationBuilder.Sql("""
                UPDATE "RefreshTokens" AS refresh_token
                SET "UserSessionLogId" = refresh_token."UserSessionId"
                WHERE EXISTS
                (
                    SELECT 1
                    FROM "UserSessionLogs" AS session_log
                    WHERE session_log."Id" = refresh_token."UserSessionId"
                );

                UPDATE "RefreshTokens"
                SET "UserSessionId" = NULL
                WHERE "UserSessionLogId" IS NOT NULL;
                """);

            migrationBuilder.Sql("""
                DELETE FROM "UserSessions"
                WHERE "RevokedAt" IS NOT NULL OR "ExpiresAt" <= CURRENT_TIMESTAMP;
                """);

            migrationBuilder.DropColumn(
                name: "RevocationReason",
                table: "UserSessions");

            migrationBuilder.DropColumn(
                name: "RevokedAt",
                table: "UserSessions");

            migrationBuilder.RenameTable(
                name: "UserSessions",
                newName: "ActiveSessions");

            migrationBuilder.RenameIndex(
                name: "IX_UserSessions_UserId_DeviceId",
                table: "ActiveSessions",
                newName: "IX_ActiveSessions_UserId_DeviceId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ActiveSessions",
                table: "ActiveSessions",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_ActiveSessions_DeviceId",
                table: "ActiveSessions",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_ActiveSessions_UserId_ExpiresAt",
                table: "ActiveSessions",
                columns: new[] { "UserId", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_UserSessionLogs_DeviceId_EndedAt",
                table: "UserSessionLogs",
                columns: new[] { "DeviceId", "EndedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_UserSessionLogs_UserId_EndedAt",
                table: "UserSessionLogs",
                columns: new[] { "UserId", "EndedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_UserSessionLogId_ExpiresAt",
                table: "RefreshTokens",
                columns: new[] { "UserSessionLogId", "ExpiresAt" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_RefreshTokens_ExactlyOneSession",
                table: "RefreshTokens",
                sql: "(\"UserSessionId\" IS NOT NULL AND \"UserSessionLogId\" IS NULL) OR (\"UserSessionId\" IS NULL AND \"UserSessionLogId\" IS NOT NULL)");

            migrationBuilder.AddForeignKey(
                name: "FK_ActiveSessions_DeviceInstallations_DeviceId",
                table: "ActiveSessions",
                column: "DeviceId",
                principalTable: "DeviceInstallations",
                principalColumn: "DeviceId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ActiveSessions_Users_UserId",
                table: "ActiveSessions",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_RefreshTokens_ActiveSessions_UserSessionId",
                table: "RefreshTokens",
                column: "UserSessionId",
                principalTable: "ActiveSessions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_RefreshTokens_UserSessionLogs_UserSessionLogId",
                table: "RefreshTokens",
                column: "UserSessionLogId",
                principalTable: "UserSessionLogs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RefreshTokens_ActiveSessions_UserSessionId",
                table: "RefreshTokens");

            migrationBuilder.DropForeignKey(
                name: "FK_RefreshTokens_UserSessionLogs_UserSessionLogId",
                table: "RefreshTokens");

            migrationBuilder.DropForeignKey(
                name: "FK_ActiveSessions_DeviceInstallations_DeviceId",
                table: "ActiveSessions");

            migrationBuilder.DropForeignKey(
                name: "FK_ActiveSessions_Users_UserId",
                table: "ActiveSessions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_RefreshTokens_ExactlyOneSession",
                table: "RefreshTokens");

            migrationBuilder.DropIndex(
                name: "IX_RefreshTokens_UserSessionLogId_ExpiresAt",
                table: "RefreshTokens");

            migrationBuilder.DropIndex(
                name: "IX_ActiveSessions_DeviceId",
                table: "ActiveSessions");

            migrationBuilder.DropIndex(
                name: "IX_ActiveSessions_UserId_ExpiresAt",
                table: "ActiveSessions");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ActiveSessions",
                table: "ActiveSessions");

            migrationBuilder.AddColumn<DateTime>(
                name: "RevokedAt",
                table: "ActiveSessions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RevocationReason",
                table: "ActiveSessions",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.RenameTable(
                name: "ActiveSessions",
                newName: "UserSessions");

            migrationBuilder.RenameIndex(
                name: "IX_ActiveSessions_UserId_DeviceId",
                table: "UserSessions",
                newName: "IX_UserSessions_UserId_DeviceId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_UserSessions",
                table: "UserSessions",
                column: "Id");

            migrationBuilder.Sql("""
                INSERT INTO "UserSessions"
                    ("Id", "UserId", "DeviceId", "SessionVersion", "CreatedAt", "LastSeenAt", "ExpiresAt", "RememberMe", "RevokedAt", "RevocationReason")
                SELECT
                    "Id", "UserId", "DeviceId", "SessionVersion", "CreatedAt", "LastSeenAt", "ExpiresAt", "RememberMe", "EndedAt", "EndReason"
                FROM "UserSessionLogs";

                UPDATE "RefreshTokens"
                SET "UserSessionId" = "UserSessionLogId",
                    "UserSessionLogId" = NULL
                WHERE "UserSessionLogId" IS NOT NULL;
                """);

            migrationBuilder.DropTable(
                name: "UserSessionLogs");

            migrationBuilder.DropColumn(
                name: "UserSessionLogId",
                table: "RefreshTokens");

            migrationBuilder.AlterColumn<Guid>(
                name: "UserSessionId",
                table: "RefreshTokens",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserSessions_DeviceId_RevokedAt",
                table: "UserSessions",
                columns: new[] { "DeviceId", "RevokedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_UserSessions_UserId_RevokedAt_ExpiresAt",
                table: "UserSessions",
                columns: new[] { "UserId", "RevokedAt", "ExpiresAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_UserSessions_DeviceInstallations_DeviceId",
                table: "UserSessions",
                column: "DeviceId",
                principalTable: "DeviceInstallations",
                principalColumn: "DeviceId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_UserSessions_Users_UserId",
                table: "UserSessions",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_RefreshTokens_UserSessions_UserSessionId",
                table: "RefreshTokens",
                column: "UserSessionId",
                principalTable: "UserSessions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}

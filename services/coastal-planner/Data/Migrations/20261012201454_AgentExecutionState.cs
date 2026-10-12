using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Blueverse.CoastalPlanner.Data.Migrations
{
    /// <inheritdoc />
    public partial class AgentExecutionState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "agent_execution_states",
                schema: "coastal_planner",
                columns: table => new
                {
                    WorkflowId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CorrelationId = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    StateJson = table.Column<string>(type: "jsonb", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agent_execution_states", x => x.WorkflowId);
                    table.CheckConstraint("CK_agent_execution_states_status", "\"Status\" IN ('PLANNING', 'DELEGATING', 'BLOCKED', 'COMPLETED', 'UNAVAILABLE', 'SAFE_FAILURE')");
                    table.ForeignKey(
                        name: "FK_agent_execution_states_planning_workflows_WorkflowId",
                        column: x => x.WorkflowId,
                        principalSchema: "coastal_planner",
                        principalTable: "planning_workflows",
                        principalColumn: "WorkflowId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_agent_execution_states_UpdatedAtUtc",
                schema: "coastal_planner",
                table: "agent_execution_states",
                column: "UpdatedAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "agent_execution_states",
                schema: "coastal_planner");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pentra.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddToolRuns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ToolRuns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProjectId = table.Column<int>(type: "INTEGER", nullable: false),
                    PhaseId = table.Column<int>(type: "INTEGER", nullable: false),
                    SecurityToolId = table.Column<int>(type: "INTEGER", nullable: false),
                    ScopeTargetId = table.Column<int>(type: "INTEGER", nullable: true),
                    ToolSlug = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ImageRef = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    TargetValue = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    ParametersJson = table.Column<string>(type: "TEXT", maxLength: 8000, nullable: false),
                    ApprovedArgumentsJson = table.Column<string>(type: "TEXT", maxLength: 8000, nullable: false),
                    IsActiveScan = table.Column<bool>(type: "INTEGER", nullable: false),
                    ConfirmedActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    AuthorizationAllowed = table.Column<bool>(type: "INTEGER", nullable: false),
                    AuthorizationReason = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    CancelRequested = table.Column<bool>(type: "INTEGER", nullable: false),
                    TimeoutSeconds = table.Column<int>(type: "INTEGER", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DurationMs = table.Column<long>(type: "INTEGER", nullable: true),
                    ExitCode = table.Column<int>(type: "INTEGER", nullable: true),
                    FailureReason = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    RawOutput = table.Column<string>(type: "TEXT", nullable: false),
                    ErrorOutput = table.Column<string>(type: "TEXT", nullable: false),
                    ResultJson = table.Column<string>(type: "TEXT", nullable: false),
                    ArtifactSha256 = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ClaimedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    RunnerId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ToolRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ToolRuns_Phases_PhaseId",
                        column: x => x.PhaseId,
                        principalTable: "Phases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ToolRuns_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ToolRuns_Targets_ScopeTargetId",
                        column: x => x.ScopeTargetId,
                        principalTable: "Targets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ToolRuns_Tools_SecurityToolId",
                        column: x => x.SecurityToolId,
                        principalTable: "Tools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ToolRunLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ToolRunId = table.Column<int>(type: "INTEGER", nullable: false),
                    Seq = table.Column<int>(type: "INTEGER", nullable: false),
                    Stream = table.Column<int>(type: "INTEGER", nullable: false),
                    Text = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ToolRunLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ToolRunLogs_ToolRuns_ToolRunId",
                        column: x => x.ToolRunId,
                        principalTable: "ToolRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ToolRunLogs_ToolRunId_Seq",
                table: "ToolRunLogs",
                columns: new[] { "ToolRunId", "Seq" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ToolRuns_PhaseId",
                table: "ToolRuns",
                column: "PhaseId");

            migrationBuilder.CreateIndex(
                name: "IX_ToolRuns_ProjectId_CreatedAt",
                table: "ToolRuns",
                columns: new[] { "ProjectId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ToolRuns_ScopeTargetId",
                table: "ToolRuns",
                column: "ScopeTargetId");

            migrationBuilder.CreateIndex(
                name: "IX_ToolRuns_SecurityToolId",
                table: "ToolRuns",
                column: "SecurityToolId");

            migrationBuilder.CreateIndex(
                name: "IX_ToolRuns_Status_CreatedAt",
                table: "ToolRuns",
                columns: new[] { "Status", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ToolRunLogs");

            migrationBuilder.DropTable(
                name: "ToolRuns");
        }
    }
}

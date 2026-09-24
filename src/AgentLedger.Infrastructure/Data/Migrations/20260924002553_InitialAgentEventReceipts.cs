using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentLedger.Infrastructure.Data.Migrations;

/// <inheritdoc />
public partial class InitialAgentEventReceipts : Migration
{
  /// <inheritdoc />
  protected override void Up(MigrationBuilder migrationBuilder)
  {
    migrationBuilder.CreateTable(
        name: "agent_event_receipts",
        columns: table => new
        {
          id = table.Column<Guid>(type: "uuid", nullable: false),
          event_id = table.Column<Guid>(type: "uuid", nullable: false),
          agent = table.Column<string>(type: "text", nullable: false),
          event_type = table.Column<string>(type: "text", nullable: false),
          native_session_id = table.Column<string>(type: "text", nullable: true),
          captured_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
          received_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
          tags = table.Column<string>(type: "jsonb", nullable: false),
          payload = table.Column<string>(type: "json", nullable: false),
          created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
          updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
          context_git_branch = table.Column<string>(type: "text", nullable: true),
          context_git_repo = table.Column<string>(type: "text", nullable: true),
          context_git_worktree = table.Column<string>(type: "text", nullable: true),
          context_host = table.Column<string>(type: "text", nullable: false),
          context_project_dir = table.Column<string>(type: "text", nullable: false),
          context_user = table.Column<string>(type: "text", nullable: false)
        },
        constraints: table =>
        {
          table.PrimaryKey("pk_agent_event_receipts", x => x.id);
        });

    migrationBuilder.CreateIndex(
        name: "ix_agent_event_receipts_agent_native_session_id",
        table: "agent_event_receipts",
        columns: new[] { "agent", "native_session_id" });

    migrationBuilder.CreateIndex(
        name: "ix_agent_event_receipts_captured_at",
        table: "agent_event_receipts",
        column: "captured_at");

    migrationBuilder.CreateIndex(
        name: "ix_agent_event_receipts_event_id",
        table: "agent_event_receipts",
        column: "event_id");

    migrationBuilder.CreateIndex(
        name: "ix_agent_event_receipts_tags",
        table: "agent_event_receipts",
        column: "tags")
        .Annotation("Npgsql:IndexMethod", "gin");

    migrationBuilder.CreateIndex(
        name: "ix_agent_event_receipts_updated_at",
        table: "agent_event_receipts",
        column: "updated_at");
  }

  /// <inheritdoc />
  protected override void Down(MigrationBuilder migrationBuilder)
  {
    migrationBuilder.DropTable(
        name: "agent_event_receipts");
  }
}

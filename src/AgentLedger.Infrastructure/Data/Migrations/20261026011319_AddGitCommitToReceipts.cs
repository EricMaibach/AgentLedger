using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentLedger.Infrastructure.Data.Migrations;

/// <inheritdoc />
public partial class AddGitCommitToReceipts : Migration
{
  /// <inheritdoc />
  protected override void Up(MigrationBuilder migrationBuilder)
  {
    migrationBuilder.AddColumn<string>(
        name: "context_git_commit",
        table: "agent_event_receipts",
        type: "text",
        nullable: true);
  }

  /// <inheritdoc />
  protected override void Down(MigrationBuilder migrationBuilder)
  {
    migrationBuilder.DropColumn(
        name: "context_git_commit",
        table: "agent_event_receipts");
  }
}

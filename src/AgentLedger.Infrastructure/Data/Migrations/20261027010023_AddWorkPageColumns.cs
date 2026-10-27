using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentLedger.Infrastructure.Data.Migrations;

/// <inheritdoc />
public partial class AddWorkPageColumns : Migration
{
  /// <inheritdoc />
  protected override void Up(MigrationBuilder migrationBuilder)
  {
    migrationBuilder.AddColumn<string>(
        name: "summary",
        table: "steps",
        type: "text",
        nullable: true);

    migrationBuilder.AddColumn<string>(
        name: "git_branch",
        table: "sessions",
        type: "text",
        nullable: true);

    migrationBuilder.AddColumn<string>(
        name: "git_repo",
        table: "sessions",
        type: "text",
        nullable: true);

    migrationBuilder.AddColumn<string>(
        name: "host",
        table: "sessions",
        type: "text",
        nullable: true);

    migrationBuilder.AddColumn<string>(
        name: "title",
        table: "sessions",
        type: "text",
        nullable: true);

    migrationBuilder.AddColumn<string>(
        name: "user",
        table: "sessions",
        type: "text",
        nullable: true);

    migrationBuilder.CreateIndex(
        name: "ix_sessions_last_activity_at_id",
        table: "sessions",
        columns: new[] { "last_activity_at", "id" });
  }

  /// <inheritdoc />
  protected override void Down(MigrationBuilder migrationBuilder)
  {
    migrationBuilder.DropIndex(
        name: "ix_sessions_last_activity_at_id",
        table: "sessions");

    migrationBuilder.DropColumn(
        name: "summary",
        table: "steps");

    migrationBuilder.DropColumn(
        name: "git_branch",
        table: "sessions");

    migrationBuilder.DropColumn(
        name: "git_repo",
        table: "sessions");

    migrationBuilder.DropColumn(
        name: "host",
        table: "sessions");

    migrationBuilder.DropColumn(
        name: "title",
        table: "sessions");

    migrationBuilder.DropColumn(
        name: "user",
        table: "sessions");
  }
}

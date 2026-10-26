using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentLedger.Infrastructure.Data.Migrations;

/// <inheritdoc />
public partial class AddGitCommits : Migration
{
  /// <inheritdoc />
  protected override void Up(MigrationBuilder migrationBuilder)
  {
    migrationBuilder.CreateTable(
        name: "git_commits",
        columns: table => new
        {
          id = table.Column<Guid>(type: "uuid", nullable: false),
          repository = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
          hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
          parent_hashes = table.Column<string[]>(type: "text[]", nullable: false),
          authored_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
          committed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
          message = table.Column<string>(type: "text", nullable: false),
          files = table.Column<string>(type: "jsonb", nullable: false),
          files_truncated = table.Column<bool>(type: "boolean", nullable: false),
          api_key_id = table.Column<Guid>(type: "uuid", nullable: false),
          received_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
          created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
          updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
        },
        constraints: table =>
        {
          table.PrimaryKey("pk_git_commits", x => x.id);
          table.ForeignKey(
                    name: "fk_git_commits_api_keys_api_key_id",
                    column: x => x.api_key_id,
                    principalTable: "api_keys",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
        });

    migrationBuilder.CreateIndex(
        name: "ix_git_commits_api_key_id",
        table: "git_commits",
        column: "api_key_id");

    migrationBuilder.CreateIndex(
        name: "ix_git_commits_repository_committed_at",
        table: "git_commits",
        columns: new[] { "repository", "committed_at" });
  }

  /// <inheritdoc />
  protected override void Down(MigrationBuilder migrationBuilder)
  {
    migrationBuilder.DropTable(
        name: "git_commits");
  }
}

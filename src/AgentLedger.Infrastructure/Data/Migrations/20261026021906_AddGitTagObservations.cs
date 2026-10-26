using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentLedger.Infrastructure.Data.Migrations;

/// <inheritdoc />
public partial class AddGitTagObservations : Migration
{
  /// <inheritdoc />
  protected override void Up(MigrationBuilder migrationBuilder)
  {
    migrationBuilder.CreateTable(
        name: "git_tag_observations",
        columns: table => new
        {
          id = table.Column<Guid>(type: "uuid", nullable: false),
          repository = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
          tag_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
          change = table.Column<string>(type: "text", nullable: false),
          commit = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
          previous_commit = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
          observed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
          api_key_id = table.Column<Guid>(type: "uuid", nullable: false),
          received_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
          created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
          updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
        },
        constraints: table =>
        {
          table.PrimaryKey("pk_git_tag_observations", x => x.id);
          table.ForeignKey(
                    name: "fk_git_tag_observations_api_keys_api_key_id",
                    column: x => x.api_key_id,
                    principalTable: "api_keys",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
        });

    migrationBuilder.CreateIndex(
        name: "ix_git_tag_observations_api_key_id",
        table: "git_tag_observations",
        column: "api_key_id");

    migrationBuilder.CreateIndex(
        name: "ix_git_tag_observations_repository_tag_name_observed_at",
        table: "git_tag_observations",
        columns: new[] { "repository", "tag_name", "observed_at" });
  }

  /// <inheritdoc />
  protected override void Down(MigrationBuilder migrationBuilder)
  {
    migrationBuilder.DropTable(
        name: "git_tag_observations");
  }
}

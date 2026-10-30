using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentLedger.Infrastructure.Data.Migrations;

/// <inheritdoc />
public partial class AddApiKeyClientVersion : Migration
{
  /// <inheritdoc />
  protected override void Up(MigrationBuilder migrationBuilder)
  {
    migrationBuilder.AddColumn<string>(
        name: "last_client_version",
        table: "api_keys",
        type: "character varying(64)",
        maxLength: 64,
        nullable: true);
  }

  /// <inheritdoc />
  protected override void Down(MigrationBuilder migrationBuilder)
  {
    migrationBuilder.DropColumn(
        name: "last_client_version",
        table: "api_keys");
  }
}

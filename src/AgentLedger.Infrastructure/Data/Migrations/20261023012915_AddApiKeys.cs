using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentLedger.Infrastructure.Data.Migrations;

/// <inheritdoc />
public partial class AddApiKeys : Migration
{
  /// <inheritdoc />
  protected override void Up(MigrationBuilder migrationBuilder)
  {
    migrationBuilder.AddColumn<Guid>(
        name: "api_key_id",
        table: "agent_event_receipts",
        type: "uuid",
        nullable: true);

    migrationBuilder.CreateTable(
        name: "api_keys",
        columns: table => new
        {
          id = table.Column<Guid>(type: "uuid", nullable: false),
          owner_id = table.Column<Guid>(type: "uuid", nullable: false),
          name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
          scope = table.Column<string>(type: "text", nullable: false),
          secret_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
          issued_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
          expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
          last_used_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
          revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
          revoked_by = table.Column<Guid>(type: "uuid", nullable: true),
          created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
          updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
        },
        constraints: table =>
        {
          table.PrimaryKey("pk_api_keys", x => x.id);
          table.ForeignKey(
                    name: "fk_api_keys_users_owner_id",
                    column: x => x.owner_id,
                    principalTable: "users",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
          table.ForeignKey(
                    name: "fk_api_keys_users_revoked_by",
                    column: x => x.revoked_by,
                    principalTable: "users",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
        });

    migrationBuilder.CreateIndex(
        name: "ix_agent_event_receipts_api_key_id",
        table: "agent_event_receipts",
        column: "api_key_id");

    migrationBuilder.CreateIndex(
        name: "ix_api_keys_owner_id",
        table: "api_keys",
        column: "owner_id");

    migrationBuilder.CreateIndex(
        name: "ix_api_keys_revoked_by",
        table: "api_keys",
        column: "revoked_by");

    migrationBuilder.AddForeignKey(
        name: "fk_agent_event_receipts_api_keys_api_key_id",
        table: "agent_event_receipts",
        column: "api_key_id",
        principalTable: "api_keys",
        principalColumn: "id",
        onDelete: ReferentialAction.Restrict);
  }

  /// <inheritdoc />
  protected override void Down(MigrationBuilder migrationBuilder)
  {
    migrationBuilder.DropForeignKey(
        name: "fk_agent_event_receipts_api_keys_api_key_id",
        table: "agent_event_receipts");

    migrationBuilder.DropTable(
        name: "api_keys");

    migrationBuilder.DropIndex(
        name: "ix_agent_event_receipts_api_key_id",
        table: "agent_event_receipts");

    migrationBuilder.DropColumn(
        name: "api_key_id",
        table: "agent_event_receipts");
  }
}

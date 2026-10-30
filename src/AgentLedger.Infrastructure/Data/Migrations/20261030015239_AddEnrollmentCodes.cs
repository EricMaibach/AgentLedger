using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentLedger.Infrastructure.Data.Migrations;

/// <inheritdoc />
public partial class AddEnrollmentCodes : Migration
{
  /// <inheritdoc />
  protected override void Up(MigrationBuilder migrationBuilder)
  {
    migrationBuilder.CreateTable(
        name: "enrollment_codes",
        columns: table => new
        {
          id = table.Column<Guid>(type: "uuid", nullable: false),
          owner_id = table.Column<Guid>(type: "uuid", nullable: false),
          code_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
          created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
          expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
          used_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
          used_by_host = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
          api_key_id = table.Column<Guid>(type: "uuid", nullable: true),
          updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
        },
        constraints: table =>
        {
          table.PrimaryKey("pk_enrollment_codes", x => x.id);
          table.ForeignKey(
                    name: "fk_enrollment_codes_api_keys_api_key_id",
                    column: x => x.api_key_id,
                    principalTable: "api_keys",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
          table.ForeignKey(
                    name: "fk_enrollment_codes_users_owner_id",
                    column: x => x.owner_id,
                    principalTable: "users",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
        });

    migrationBuilder.CreateIndex(
        name: "ix_enrollment_codes_api_key_id",
        table: "enrollment_codes",
        column: "api_key_id");

    migrationBuilder.CreateIndex(
        name: "ix_enrollment_codes_code_hash",
        table: "enrollment_codes",
        column: "code_hash",
        unique: true);

    migrationBuilder.CreateIndex(
        name: "ix_enrollment_codes_owner_id",
        table: "enrollment_codes",
        column: "owner_id");
  }

  /// <inheritdoc />
  protected override void Down(MigrationBuilder migrationBuilder)
  {
    migrationBuilder.DropTable(
        name: "enrollment_codes");
  }
}

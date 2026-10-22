using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentLedger.Infrastructure.Data.Migrations;

/// <inheritdoc />
public partial class AddUsers : Migration
{
  /// <inheritdoc />
  protected override void Up(MigrationBuilder migrationBuilder)
  {
    migrationBuilder.CreateTable(
        name: "users",
        columns: table => new
        {
          id = table.Column<Guid>(type: "uuid", nullable: false),
          username = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
          password_hash = table.Column<string>(type: "text", nullable: false),
          security_stamp = table.Column<Guid>(type: "uuid", nullable: false),
          is_admin = table.Column<bool>(type: "boolean", nullable: false),
          failed_sign_in_count = table.Column<int>(type: "integer", nullable: false),
          locked_until = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
          created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
          updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
        },
        constraints: table =>
        {
          table.PrimaryKey("pk_users", x => x.id);
        });

    migrationBuilder.CreateIndex(
        name: "ix_users_username",
        table: "users",
        column: "username",
        unique: true);
  }

  /// <inheritdoc />
  protected override void Down(MigrationBuilder migrationBuilder)
  {
    migrationBuilder.DropTable(
        name: "users");
  }
}

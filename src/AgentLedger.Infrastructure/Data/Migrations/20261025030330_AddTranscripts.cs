using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentLedger.Infrastructure.Data.Migrations;

/// <inheritdoc />
public partial class AddTranscripts : Migration
{
  /// <inheritdoc />
  protected override void Up(MigrationBuilder migrationBuilder)
  {
    migrationBuilder.CreateTable(
        name: "transcript_files",
        columns: table => new
        {
          id = table.Column<Guid>(type: "uuid", nullable: false),
          agent = table.Column<string>(type: "text", nullable: false),
          native_session_id = table.Column<string>(type: "text", nullable: false),
          file = table.Column<string>(type: "text", nullable: false),
          subagent_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
          meta = table.Column<string>(type: "text", nullable: true),
          api_key_id = table.Column<Guid>(type: "uuid", nullable: false),
          first_seen_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
          created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
          updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
        },
        constraints: table =>
        {
          table.PrimaryKey("pk_transcript_files", x => x.id);
          table.ForeignKey(
                    name: "fk_transcript_files_api_keys_api_key_id",
                    column: x => x.api_key_id,
                    principalTable: "api_keys",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
        });

    migrationBuilder.CreateTable(
        name: "transcript_lines",
        columns: table => new
        {
          id = table.Column<Guid>(type: "uuid", nullable: false),
          agent = table.Column<string>(type: "text", nullable: false),
          native_session_id = table.Column<string>(type: "text", nullable: false),
          file = table.Column<string>(type: "text", nullable: false),
          subagent_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
          byte_offset = table.Column<long>(type: "bigint", nullable: false),
          text = table.Column<string>(type: "text", nullable: false),
          api_key_id = table.Column<Guid>(type: "uuid", nullable: false),
          received_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
          created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
          updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
        },
        constraints: table =>
        {
          table.PrimaryKey("pk_transcript_lines", x => x.id);
          table.ForeignKey(
                    name: "fk_transcript_lines_api_keys_api_key_id",
                    column: x => x.api_key_id,
                    principalTable: "api_keys",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
        });

    migrationBuilder.CreateIndex(
        name: "ix_transcript_files_agent_native_session_id",
        table: "transcript_files",
        columns: new[] { "agent", "native_session_id" });

    migrationBuilder.CreateIndex(
        name: "ix_transcript_files_api_key_id",
        table: "transcript_files",
        column: "api_key_id");

    migrationBuilder.CreateIndex(
        name: "ix_transcript_lines_agent_native_session_id_file_byte_offset",
        table: "transcript_lines",
        columns: new[] { "agent", "native_session_id", "file", "byte_offset" });

    migrationBuilder.CreateIndex(
        name: "ix_transcript_lines_api_key_id",
        table: "transcript_lines",
        column: "api_key_id");
  }

  /// <inheritdoc />
  protected override void Down(MigrationBuilder migrationBuilder)
  {
    migrationBuilder.DropTable(
        name: "transcript_files");

    migrationBuilder.DropTable(
        name: "transcript_lines");
  }
}

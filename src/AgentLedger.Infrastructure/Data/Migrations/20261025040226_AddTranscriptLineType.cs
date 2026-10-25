using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentLedger.Infrastructure.Data.Migrations;

/// <inheritdoc />
public partial class AddTranscriptLineType : Migration
{
  /// <inheritdoc />
  protected override void Up(MigrationBuilder migrationBuilder)
  {
    migrationBuilder.AddColumn<string>(
        name: "line_type",
        table: "transcript_lines",
        type: "text",
        nullable: true);

    // Lines stored before this column: the same rule as TranscriptLine (the top-level "type" field, only if the
    // line is a JSON object and the field a string). pg_input_is_valid (Postgres 16+) skips lines that aren't JSON.
    migrationBuilder.Sql("""
              UPDATE transcript_lines
              SET line_type = text::json ->> 'type'
              WHERE pg_input_is_valid(text, 'json')
                AND json_typeof(text::json) = 'object'
                AND json_typeof(text::json -> 'type') = 'string'
              """);

    migrationBuilder.CreateIndex(
        name: "ix_transcript_lines_received_at_byte_offset_id",
        table: "transcript_lines",
        columns: new[] { "received_at", "byte_offset", "id" });
  }

  /// <inheritdoc />
  protected override void Down(MigrationBuilder migrationBuilder)
  {
    migrationBuilder.DropIndex(
        name: "ix_transcript_lines_received_at_byte_offset_id",
        table: "transcript_lines");

    migrationBuilder.DropColumn(
        name: "line_type",
        table: "transcript_lines");
  }
}

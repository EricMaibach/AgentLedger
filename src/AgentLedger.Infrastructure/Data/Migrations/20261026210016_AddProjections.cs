using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentLedger.Infrastructure.Data.Migrations;

/// <inheritdoc />
public partial class AddProjections : Migration
{
  /// <inheritdoc />
  protected override void Up(MigrationBuilder migrationBuilder)
  {
    migrationBuilder.CreateTable(
        name: "projection_state",
        columns: table => new
        {
          name = table.Column<string>(type: "text", nullable: false),
          watermark = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
          applied_version = table.Column<int>(type: "integer", nullable: false),
          created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
          updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
        },
        constraints: table =>
        {
          table.PrimaryKey("pk_projection_state", x => x.name);
        });

    migrationBuilder.CreateTable(
        name: "sessions",
        columns: table => new
        {
          id = table.Column<Guid>(type: "uuid", nullable: false),
          agent = table.Column<string>(type: "text", nullable: false),
          native_session_id = table.Column<string>(type: "text", nullable: false),
          started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
          last_activity_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
          turn_count = table.Column<int>(type: "integer", nullable: false),
          projection_version = table.Column<int>(type: "integer", nullable: false),
          created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
          updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
        },
        constraints: table =>
        {
          table.PrimaryKey("pk_sessions", x => x.id);
        });

    migrationBuilder.CreateTable(
        name: "turns",
        columns: table => new
        {
          id = table.Column<Guid>(type: "uuid", nullable: false),
          session_id = table.Column<Guid>(type: "uuid", nullable: false),
          sequence = table.Column<int>(type: "integer", nullable: false),
          prompt_id = table.Column<string>(type: "text", nullable: false),
          status = table.Column<string>(type: "text", nullable: false),
          source = table.Column<string>(type: "text", nullable: false),
          started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
          ended_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
          git_repo = table.Column<string>(type: "text", nullable: true),
          git_branch = table.Column<string>(type: "text", nullable: true),
          git_commit = table.Column<string>(type: "text", nullable: true),
          git_worktree = table.Column<string>(type: "text", nullable: true),
          directory = table.Column<string>(type: "text", nullable: true),
          host = table.Column<string>(type: "text", nullable: true),
          user = table.Column<string>(type: "text", nullable: true),
          source_event_ids = table.Column<Guid[]>(type: "uuid[]", nullable: false),
          source_line_ids = table.Column<Guid[]>(type: "uuid[]", nullable: false),
          created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
          updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
        },
        constraints: table =>
        {
          table.PrimaryKey("pk_turns", x => x.id);
          table.ForeignKey(
                    name: "fk_turns_sessions_session_id",
                    column: x => x.session_id,
                    principalTable: "sessions",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
        });

    migrationBuilder.CreateTable(
        name: "model_responses",
        columns: table => new
        {
          id = table.Column<Guid>(type: "uuid", nullable: false),
          turn_id = table.Column<Guid>(type: "uuid", nullable: false),
          session_id = table.Column<Guid>(type: "uuid", nullable: false),
          message_id = table.Column<string>(type: "text", nullable: false),
          model = table.Column<string>(type: "text", nullable: false),
          responded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
          input_tokens = table.Column<long>(type: "bigint", nullable: false),
          output_tokens = table.Column<long>(type: "bigint", nullable: false),
          cache_write5m_tokens = table.Column<long>(type: "bigint", nullable: false),
          cache_write1h_tokens = table.Column<long>(type: "bigint", nullable: false),
          cache_read_tokens = table.Column<long>(type: "bigint", nullable: false),
          thinking_tokens = table.Column<long>(type: "bigint", nullable: false),
          web_searches = table.Column<long>(type: "bigint", nullable: false),
          web_fetches = table.Column<long>(type: "bigint", nullable: false),
          source_event_ids = table.Column<Guid[]>(type: "uuid[]", nullable: false),
          source_line_ids = table.Column<Guid[]>(type: "uuid[]", nullable: false),
          created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
          updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
        },
        constraints: table =>
        {
          table.PrimaryKey("pk_model_responses", x => x.id);
          table.ForeignKey(
                    name: "fk_model_responses_turns_turn_id",
                    column: x => x.turn_id,
                    principalTable: "turns",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
        });

    migrationBuilder.CreateTable(
        name: "steps",
        columns: table => new
        {
          id = table.Column<Guid>(type: "uuid", nullable: false),
          turn_id = table.Column<Guid>(type: "uuid", nullable: false),
          session_id = table.Column<Guid>(type: "uuid", nullable: false),
          sequence = table.Column<int>(type: "integer", nullable: false),
          kind = table.Column<string>(type: "text", nullable: false),
          started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
          ended_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
          text = table.Column<string>(type: "text", nullable: true),
          tool_name = table.Column<string>(type: "text", nullable: true),
          tool_use_id = table.Column<string>(type: "text", nullable: true),
          tool_input = table.Column<string>(type: "text", nullable: true),
          tool_output = table.Column<string>(type: "text", nullable: true),
          succeeded = table.Column<bool>(type: "boolean", nullable: true),
          response_id = table.Column<Guid>(type: "uuid", nullable: true),
          source_event_ids = table.Column<Guid[]>(type: "uuid[]", nullable: false),
          source_line_ids = table.Column<Guid[]>(type: "uuid[]", nullable: false),
          created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
          updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
        },
        constraints: table =>
        {
          table.PrimaryKey("pk_steps", x => x.id);
          table.ForeignKey(
                    name: "fk_steps_turns_turn_id",
                    column: x => x.turn_id,
                    principalTable: "turns",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
        });

    migrationBuilder.CreateIndex(
        name: "ix_transcript_lines_updated_at",
        table: "transcript_lines",
        column: "updated_at");

    migrationBuilder.CreateIndex(
        name: "ix_model_responses_session_id",
        table: "model_responses",
        column: "session_id");

    migrationBuilder.CreateIndex(
        name: "ix_model_responses_source_line_ids",
        table: "model_responses",
        column: "source_line_ids")
        .Annotation("Npgsql:IndexMethod", "gin");

    migrationBuilder.CreateIndex(
        name: "ix_model_responses_turn_id",
        table: "model_responses",
        column: "turn_id");

    migrationBuilder.CreateIndex(
        name: "ix_sessions_agent_native_session_id",
        table: "sessions",
        columns: new[] { "agent", "native_session_id" },
        unique: true);

    migrationBuilder.CreateIndex(
        name: "ix_sessions_projection_version_last_activity_at",
        table: "sessions",
        columns: new[] { "projection_version", "last_activity_at" });

    migrationBuilder.CreateIndex(
        name: "ix_steps_session_id",
        table: "steps",
        column: "session_id");

    migrationBuilder.CreateIndex(
        name: "ix_steps_source_event_ids",
        table: "steps",
        column: "source_event_ids")
        .Annotation("Npgsql:IndexMethod", "gin");

    migrationBuilder.CreateIndex(
        name: "ix_steps_source_line_ids",
        table: "steps",
        column: "source_line_ids")
        .Annotation("Npgsql:IndexMethod", "gin");

    migrationBuilder.CreateIndex(
        name: "ix_steps_turn_id_sequence",
        table: "steps",
        columns: new[] { "turn_id", "sequence" });

    migrationBuilder.CreateIndex(
        name: "ix_turns_session_id_sequence",
        table: "turns",
        columns: new[] { "session_id", "sequence" });

    migrationBuilder.CreateIndex(
        name: "ix_turns_source_event_ids",
        table: "turns",
        column: "source_event_ids")
        .Annotation("Npgsql:IndexMethod", "gin");

    migrationBuilder.CreateIndex(
        name: "ix_turns_source_line_ids",
        table: "turns",
        column: "source_line_ids")
        .Annotation("Npgsql:IndexMethod", "gin");
  }

  /// <inheritdoc />
  protected override void Down(MigrationBuilder migrationBuilder)
  {
    migrationBuilder.DropTable(
        name: "model_responses");

    migrationBuilder.DropTable(
        name: "projection_state");

    migrationBuilder.DropTable(
        name: "steps");

    migrationBuilder.DropTable(
        name: "turns");

    migrationBuilder.DropTable(
        name: "sessions");

    migrationBuilder.DropIndex(
        name: "ix_transcript_lines_updated_at",
        table: "transcript_lines");
  }
}

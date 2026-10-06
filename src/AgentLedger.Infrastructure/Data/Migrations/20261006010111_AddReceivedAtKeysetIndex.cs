using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentLedger.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddReceivedAtKeysetIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_agent_event_receipts_received_at_id",
                table: "agent_event_receipts",
                columns: new[] { "received_at", "id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_agent_event_receipts_received_at_id",
                table: "agent_event_receipts");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MailEngine.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Tables_Again : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "processed_messages",
                schema: "public",
                columns: table => new
                {
                    processed_message_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    message_id = table.Column<string>(type: "text", nullable: false),
                    idempotency_key = table.Column<string>(type: "text", nullable: true),
                    event_type = table.Column<string>(type: "text", nullable: false),
                    processed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_processed_messages", x => x.processed_message_id);
                });

            migrationBuilder.CreateIndex(
                name: "idx_processed_messages_event_type",
                schema: "public",
                table: "processed_messages",
                column: "event_type");

            migrationBuilder.CreateIndex(
                name: "idx_processed_messages_idempotency_key",
                schema: "public",
                table: "processed_messages",
                column: "idempotency_key");

            migrationBuilder.CreateIndex(
                name: "idx_processed_messages_message_id",
                schema: "public",
                table: "processed_messages",
                column: "message_id");

            migrationBuilder.CreateIndex(
                name: "idx_processed_messages_processed_at",
                schema: "public",
                table: "processed_messages",
                column: "processed_at");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "processed_messages",
                schema: "public");
        }
    }
}

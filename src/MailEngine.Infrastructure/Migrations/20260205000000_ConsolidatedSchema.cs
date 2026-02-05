using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MailEngine.Infrastructure.Migrations
{
    /// <summary>
    /// Consolidated schema migration that combines all necessary table definitions
    /// This migration addresses the issue of multiple conflicting initial migrations
    /// </summary>
    public partial class ConsolidatedSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Ensure the public schema exists
            migrationBuilder.EnsureSchema(
                name: "public");

            // Create failed_messages table (originally in InitialCreateWithNamingConventions)
            migrationBuilder.CreateTable(
                name: "failed_messages",
                schema: "public",
                columns: table => new
                {
                    failed_message_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    message_id_txt = table.Column<string>(type: "text", nullable: false),
                    topic_cd = table.Column<string>(type: "text", nullable: false),
                    subscription_txt = table.Column<string>(type: "text", nullable: false),
                    error_message_txt = table.Column<string>(type: "text", nullable: false),
                    error_stack_trace_txt = table.Column<string>(type: "text", nullable: false),
                    failed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    status_cd = table.Column<string>(type: "text", nullable: false),
                    retry_count_no = table.Column<int>(type: "integer", nullable: false),
                    resolved_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    message_content_txt = table.Column<string>(type: "text", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_failed_messages", x => x.failed_message_id);
                });

            // Create indexes for failed_messages
            migrationBuilder.CreateIndex(
                name: "idx_failed_messages_created_at_utc",
                schema: "public",
                table: "failed_messages",
                column: "created_at_utc");

            migrationBuilder.CreateIndex(
                name: "idx_failed_messages_status_cd",
                schema: "public",
                table: "failed_messages",
                column: "status_cd");

            migrationBuilder.CreateIndex(
                name: "idx_failed_messages_topic_cd",
                schema: "public",
                table: "failed_messages",
                column: "topic_cd");

            // Create oauth_tokens table (originally in InitialCreateWithNamingConventions)
            migrationBuilder.CreateTable(
                name: "oauth_tokens",
                schema: "public",
                columns: table => new
                {
                    oauth_token_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    user_mail_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    access_token_txt = table.Column<string>(type: "text", nullable: false),
                    refresh_token_txt = table.Column<string>(type: "text", nullable: false),
                    expires_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_oauth_tokens", x => x.oauth_token_id);
                });

            // Create user_mail_accounts table (originally in InitialCreateWithNamingConventions)
            migrationBuilder.CreateTable(
                name: "user_mail_accounts",
                schema: "public",
                columns: table => new
                {
                    user_mail_account_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    email_address_txt = table.Column<string>(type: "text", nullable: false),
                    provider_cd = table.Column<int>(type: "integer", nullable: false),
                    is_active_flg = table.Column<bool>(type: "boolean", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_mail_accounts", x => x.user_mail_account_id);
                });

            // Create processed_messages table (from Tables_Again migration)
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

            // Create indexes for processed_messages
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
            // Drop tables in reverse dependency order
            migrationBuilder.DropTable(
                name: "processed_messages",
                schema: "public");

            migrationBuilder.DropTable(
                name: "failed_messages",
                schema: "public");

            migrationBuilder.DropTable(
                name: "oauth_tokens",
                schema: "public");

            migrationBuilder.DropTable(
                name: "user_mail_accounts",
                schema: "public");
        }
    }
}
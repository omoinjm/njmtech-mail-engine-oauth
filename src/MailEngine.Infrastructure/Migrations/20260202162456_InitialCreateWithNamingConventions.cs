using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MailEngine.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreateWithNamingConventions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "public");

            migrationBuilder.CreateTable(
                name: "failed_messages",
                schema: "public",
                columns: table => new
                {
                    failed_message_id = table.Column<Guid>(type: "uuid", nullable: false),
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

            migrationBuilder.CreateTable(
                name: "oauth_tokens",
                schema: "public",
                columns: table => new
                {
                    oauth_token_id = table.Column<Guid>(type: "uuid", nullable: false),
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

            migrationBuilder.CreateTable(
                name: "user_mail_accounts",
                schema: "public",
                columns: table => new
                {
                    user_mail_account_id = table.Column<Guid>(type: "uuid", nullable: false),
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
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

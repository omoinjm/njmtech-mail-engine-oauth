using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MailEngine.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CreateMissingFailedMessagesTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Create the failed_messages table if it doesn't exist
            var createTableSql = @"
                CREATE TABLE IF NOT EXISTS public.failed_messages (
                    failed_message_id UUID NOT NULL DEFAULT gen_random_uuid(),
                    message_id_txt TEXT NOT NULL,
                    topic_cd TEXT NOT NULL,
                    subscription_txt TEXT NOT NULL,
                    error_message_txt TEXT NOT NULL,
                    error_stack_trace_txt TEXT NOT NULL,
                    failed_at_utc TIMESTAMP WITH TIME ZONE NOT NULL,
                    status_cd TEXT NOT NULL,
                    retry_count_no INTEGER NOT NULL DEFAULT 0,
                    resolved_at_utc TIMESTAMP WITH TIME ZONE,
                    message_content_txt TEXT NOT NULL,
                    created_at_utc TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
                    modified_at_utc TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
                    CONSTRAINT PK_failed_messages PRIMARY KEY (failed_message_id)
                );

                CREATE INDEX IF NOT EXISTS idx_failed_messages_created_at_utc ON public.failed_messages (created_at_utc);
                CREATE INDEX IF NOT EXISTS idx_failed_messages_status_cd ON public.failed_messages (status_cd);
                CREATE INDEX IF NOT EXISTS idx_failed_messages_topic_cd ON public.failed_messages (topic_cd);
            ";

            migrationBuilder.Sql(createTableSql);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop the failed_messages table and its indexes
            migrationBuilder.Sql(@"
                DROP INDEX IF EXISTS idx_failed_messages_topic_cd;
                DROP INDEX IF EXISTS idx_failed_messages_status_cd;
                DROP INDEX IF EXISTS idx_failed_messages_created_at_utc;
                DROP TABLE IF EXISTS public.failed_messages;
            ");
        }
    }
}

using MailEngine.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace MailEngine.Infrastructure.Data;

public class MailEngineDbContext : DbContext
{
    public MailEngineDbContext(DbContextOptions<MailEngineDbContext> options)
        : base(options)
    {
    }

    public DbSet<OAuthToken> OAuthTokens { get; set; }
    public DbSet<UserMailAccount> UserMailAccounts { get; set; }
    public DbSet<FailedMessage> FailedMessages { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Configure schema domains
        modelBuilder.HasDefaultSchema("public");

        // OAuthTokens configuration
        modelBuilder.Entity<OAuthToken>()
            .ToTable("oauth_tokens")
            .HasKey(t => t.OAuthTokenId);

        modelBuilder.Entity<OAuthToken>()
            .Property(t => t.OAuthTokenId)
            .HasColumnName("oauth_token_id");

        modelBuilder.Entity<OAuthToken>()
            .Property(t => t.UserMailAccountId)
            .HasColumnName("user_mail_account_id");

        modelBuilder.Entity<OAuthToken>()
            .Property(t => t.AccessTokenTxt)
            .HasColumnName("access_token_txt");

        modelBuilder.Entity<OAuthToken>()
            .Property(t => t.RefreshTokenTxt)
            .HasColumnName("refresh_token_txt");

        modelBuilder.Entity<OAuthToken>()
            .Property(t => t.ExpiresAtUtc)
            .HasColumnName("expires_at_utc");

        modelBuilder.Entity<OAuthToken>()
            .Property(t => t.CreatedAtUtc)
            .HasColumnName("created_at_utc");

        modelBuilder.Entity<OAuthToken>()
            .Property(t => t.ModifiedAtUtc)
            .HasColumnName("modified_at_utc");

        // UserMailAccounts configuration
        modelBuilder.Entity<UserMailAccount>()
            .ToTable("user_mail_accounts")
            .HasKey(u => u.UserMailAccountId);

        modelBuilder.Entity<UserMailAccount>()
            .Property(u => u.UserMailAccountId)
            .HasColumnName("user_mail_account_id");

        modelBuilder.Entity<UserMailAccount>()
            .Property(u => u.EmailAddressTxt)
            .HasColumnName("email_address_txt");

        modelBuilder.Entity<UserMailAccount>()
            .Property(u => u.ProviderCd)
            .HasColumnName("provider_cd");

        modelBuilder.Entity<UserMailAccount>()
            .Property(u => u.IsActiveFalg)
            .HasColumnName("is_active_flg");

        modelBuilder.Entity<UserMailAccount>()
            .Property(u => u.CreatedAtUtc)
            .HasColumnName("created_at_utc");

        modelBuilder.Entity<UserMailAccount>()
            .Property(u => u.ModifiedAtUtc)
            .HasColumnName("modified_at_utc");

        // FailedMessages configuration
        modelBuilder.Entity<FailedMessage>()
            .ToTable("failed_messages")
            .HasKey(f => f.FailedMessageId);

        modelBuilder.Entity<FailedMessage>()
            .Property(f => f.FailedMessageId)
            .HasColumnName("failed_message_id");

        modelBuilder.Entity<FailedMessage>()
            .Property(f => f.MessageIdTxt)
            .HasColumnName("message_id_txt");

        modelBuilder.Entity<FailedMessage>()
            .Property(f => f.TopicCd)
            .HasColumnName("topic_cd");

        modelBuilder.Entity<FailedMessage>()
            .Property(f => f.SubscriptionTxt)
            .HasColumnName("subscription_txt");

        modelBuilder.Entity<FailedMessage>()
            .Property(f => f.ErrorMessageTxt)
            .HasColumnName("error_message_txt");

        modelBuilder.Entity<FailedMessage>()
            .Property(f => f.ErrorStackTraceTxt)
            .HasColumnName("error_stack_trace_txt");

        modelBuilder.Entity<FailedMessage>()
            .Property(f => f.FailedAtUtc)
            .HasColumnName("failed_at_utc");

        modelBuilder.Entity<FailedMessage>()
            .Property(f => f.StatusCd)
            .HasColumnName("status_cd");

        modelBuilder.Entity<FailedMessage>()
            .Property(f => f.RetryCountNo)
            .HasColumnName("retry_count_no");

        modelBuilder.Entity<FailedMessage>()
            .Property(f => f.ResolvedAtUtc)
            .HasColumnName("resolved_at_utc");

        modelBuilder.Entity<FailedMessage>()
            .Property(f => f.MessageContentTxt)
            .HasColumnName("message_content_txt");

        modelBuilder.Entity<FailedMessage>()
            .Property(f => f.CreatedAtUtc)
            .HasColumnName("created_at_utc");

        modelBuilder.Entity<FailedMessage>()
            .Property(f => f.ModifiedAtUtc)
            .HasColumnName("modified_at_utc");

        // Add indexes
        modelBuilder.Entity<FailedMessage>()
            .HasIndex(f => f.StatusCd)
            .HasName("idx_failed_messages_status_cd");

        modelBuilder.Entity<FailedMessage>()
            .HasIndex(f => f.TopicCd)
            .HasName("idx_failed_messages_topic_cd");

        modelBuilder.Entity<FailedMessage>()
            .HasIndex(f => f.CreatedAtUtc)
            .HasName("idx_failed_messages_created_at_utc");
    }
}

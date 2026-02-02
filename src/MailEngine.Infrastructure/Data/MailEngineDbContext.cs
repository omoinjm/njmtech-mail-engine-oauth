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
        modelBuilder.Entity<OAuthToken>().HasKey(t => t.Id);
        modelBuilder.Entity<UserMailAccount>().HasKey(u => u.Id);
        modelBuilder.Entity<FailedMessage>().HasKey(f => f.Id);
        
        // Add index on Status for efficient querying of failed messages
        modelBuilder.Entity<FailedMessage>()
            .HasIndex(f => f.Status);
        
        // Add index on Topic for filtering by topic
        modelBuilder.Entity<FailedMessage>()
            .HasIndex(f => f.Topic);
    }
}

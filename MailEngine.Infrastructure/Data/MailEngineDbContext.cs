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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OAuthToken>().HasKey(t => t.Id);
        modelBuilder.Entity<UserMailAccount>().HasKey(u => u.Id);
    }
}

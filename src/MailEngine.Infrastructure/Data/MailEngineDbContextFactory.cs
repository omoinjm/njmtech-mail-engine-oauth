using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using System.Text.Json;

namespace MailEngine.Infrastructure.Data;

public class MailEngineDbContextFactory : IDesignTimeDbContextFactory<MailEngineDbContext>
{
    public MailEngineDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<MailEngineDbContext>();

        // Try to read from environment variables or local.settings.json
        var connectionString = GetConnectionString();

        optionsBuilder.UseNpgsql(connectionString);

        return new MailEngineDbContext(optionsBuilder.Options);
    }

    private static string GetConnectionString()
    {
        // Try multiple sources for connection string
        
        // 1. Check environment variables first
        var envConnectionString = 
            Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection") ??
            Environment.GetEnvironmentVariable("ConnectionStrings__DATABASE_CONNECTION_STRING") ??
            Environment.GetEnvironmentVariable("DATABASE_CONNECTION_STRING");
        
        if (!string.IsNullOrEmpty(envConnectionString))
        {
            Console.WriteLine($"✅ Using connection string from environment variable");
            return envConnectionString;
        }
        
        // 2. Try to read from local.settings.json
        var settingsPath = Path.Combine(
            Directory.GetCurrentDirectory(),
            "../MailEngine.Functions",
            "local.settings.json"
        );
        
        if (File.Exists(settingsPath))
        {
            try
            {
                var json = File.ReadAllText(settingsPath);
                using (var doc = JsonDocument.Parse(json))
                {
                    if (doc.RootElement.TryGetProperty("ConnectionStrings", out var connectionStrings))
                    {
                        if (connectionStrings.TryGetProperty("DefaultConnection", out var defaultConnection))
                        {
                            var connString = defaultConnection.GetString();
                            if (!string.IsNullOrEmpty(connString))
                            {
                                Console.WriteLine($"✅ Using connection string from local.settings.json");
                                return connString;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️  Failed to read local.settings.json: {ex.Message}");
            }
        }
        
        // 3. Fallback to default localhost
        var defaultConnectionString = "Host=localhost;Port=5432;Database=mail_engine_dev;Username=postgres;Password=postgres";
        Console.WriteLine($"⚠️  Using fallback connection string (localhost)");
        return defaultConnectionString;
    }
}

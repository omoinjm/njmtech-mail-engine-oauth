using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace MailEngine.Infrastructure.Data;

// This class is used by EF Core tools during design time operations (migrations)
public class MailEngineDesignTimeServices : IServiceProvider
{
    private readonly ServiceProvider _serviceProvider;

    public MailEngineDesignTimeServices()
    {
        var services = new ServiceCollection();
        
        // Add only the minimum required services for migrations
        var connectionString = GetConnectionString();
        services.AddDbContext<MailEngineDbContext>(options =>
            options.UseNpgsql(connectionString));
        
        _serviceProvider = services.BuildServiceProvider();
    }

    public object GetService(Type serviceType)
    {
        return _serviceProvider.GetService(serviceType);
    }

    private static string GetConnectionString()
    {
        // Try multiple sources for connection string
        var envConnectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection") ??
            Environment.GetEnvironmentVariable("ConnectionStrings__DATABASE_CONNECTION_STRING") ??
            Environment.GetEnvironmentVariable("ConnectionStrings:DATABASE_CONNECTION_STRING") ??
            Environment.GetEnvironmentVariable("ConnectionStrings:DefaultConnection");

        if (!string.IsNullOrEmpty(envConnectionString))
        {
            Console.WriteLine($"✅ Using connection string from environment variable");
            return envConnectionString;
        }

        // Try to read from local.settings.json
        var settingsPath = Path.Combine(
            Directory.GetCurrentDirectory(),
            "..", "MailEngine.Functions",
            "local.settings.json"
        );

        if (File.Exists(settingsPath))
        {
            try
            {
                using var file = File.OpenRead(settingsPath);
                using var jsonDoc = System.Text.Json.JsonDocument.Parse(file);
                if (jsonDoc.RootElement.TryGetProperty("ConnectionStrings", out var connectionStrings))
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
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️  Failed to read local.settings.json: {ex.Message}");
            }
        }

        // Fallback to default localhost
        var defaultConnectionString = "Host=localhost;Port=5432;Database=mail_engine_dev;Username=postgres;Password=postgres";
        Console.WriteLine($"⚠️  Using fallback connection string (localhost)");
        return defaultConnectionString;
    }
}
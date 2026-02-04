using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using MailEngine.Infrastructure.Data;
using MailEngine.Infrastructure.Factories;
using MailEngine.Infrastructure.TokenStore;
using MailEngine.Core.Interfaces;
using MailEngine.Functions.Dispatching;
using MailEngine.Functions.Services;
using MailEngine.Functions.Webhooks;
using MailEngine.Providers.Gmail;
using MailEngine.Providers.Outlook;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

// Load configuration
var config = builder.Configuration;

// Logging
builder.Services.AddLogging();

// Database context
var connectionString = config.GetConnectionString("DefaultConnection");
var dbProvider = config.GetValue<string>("DatabaseProvider") ?? "PostgreSQL";

if (!string.IsNullOrEmpty(connectionString))
{
    builder.Services.AddDbContext<MailEngineDbContext>(options =>
    {
        if (dbProvider.Equals("PostgreSQL", StringComparison.OrdinalIgnoreCase))
        {
            options.UseNpgsql(connectionString);
        }
        else
        {
            options.UseSqlServer(connectionString);
        }
    });
}

// Token Repository (for OAuth token storage)
builder.Services.AddScoped<ITokenRepository, TokenRepository>();

// Mail Provider Factory (creates Gmail/Outlook instances)
builder.Services.AddScoped<IMailProviderFactory, MailProviderFactory>();

// Individual Mail Providers
builder.Services.AddScoped<GmailMailProvider>();

// OutlookMailProvider with optional IKeyVaultSecretProvider
// Use factory since IKeyVaultSecretProvider is optional (nullable)
builder.Services.AddScoped<OutlookMailProvider>(provider =>
{
    var tokenRepository = provider.GetRequiredService<ITokenRepository>();
    var logger = provider.GetRequiredService<ILogger<OutlookMailProvider>>();
    
    // IKeyVaultSecretProvider is optional, so we pass null if not configured
    return new OutlookMailProvider(tokenRepository, null, logger);
});

// Concurrency limiter
builder.Services.AddSingleton<ProviderConcurrencyLimiter>(
    new ProviderConcurrencyLimiter(maxConcurrencyPerProvider: 10));

// Core Services
builder.Services.AddScoped<IMailEventHandler, MailEventDispatcher>();
builder.Services.AddScoped<IFailedMessageLogger, FailedMessageLogger>();
builder.Services.AddScoped<IWebhookValidator, WebhookValidator>();

builder.Build().Run();


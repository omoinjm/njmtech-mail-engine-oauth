using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using MailEngine.Infrastructure.Data;
using MailEngine.Core.Interfaces;
using MailEngine.Functions.Dispatching;
using MailEngine.Functions.Services;
using MailEngine.Functions.Webhooks;

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

// Concurrency limiter
builder.Services.AddSingleton<ProviderConcurrencyLimiter>(
    new ProviderConcurrencyLimiter(maxConcurrencyPerProvider: 10));

// Services
builder.Services.AddScoped<IMailEventHandler, MailEventDispatcher>();
builder.Services.AddScoped<IFailedMessageLogger, FailedMessageLogger>();
builder.Services.AddScoped<IWebhookValidator, WebhookValidator>();

builder.Build().Run();



using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using MailEngine.Core.Interfaces;
using MailEngine.Infrastructure.Data;
using MailEngine.Infrastructure.ServiceBus;
using MailEngine.Infrastructure.KeyVault;
using MailEngine.Infrastructure.Factories;
using MailEngine.Functions.Dispatching;
using MailEngine.Providers.Gmail;
using MailEngine.Providers.Outlook;
using MailEngine.Functions.Webhooks;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

// Add configuration
builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
builder.Configuration.AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true);
builder.Configuration.AddEnvironmentVariables();

var config = builder.Configuration;

// Add logging with Application Insights
builder.Services
    .AddApplicationInsightsTelemetryWorkerService()
    .ConfigureFunctionsApplicationInsights();

// Add DbContext
var dbConnectionString = config["Database:ConnectionString"] ?? "Server=.;Database=MailEngine;Integrated Security=true;";
builder.Services.AddDbContext<MailEngineDbContext>(options =>
{
    options.UseSqlServer(dbConnectionString);
});

// Add Key Vault
var keyVaultUri = config["KeyVault:Uri"];
if (!string.IsNullOrEmpty(keyVaultUri))
{
    builder.Services.AddSingleton<IKeyVaultSecretProvider>(sp =>
        new KeyVaultSecretProvider(keyVaultUri, sp.GetRequiredService<ILogger<KeyVaultSecretProvider>>()));
}

// Add Service Bus
var serviceBusConnectionString = config["AzureServiceBus:ConnectionString"] ?? "Endpoint=sb://localhost/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_HERE";
builder.Services.AddSingleton(new ServiceBusPublisher(serviceBusConnectionString));

// Add concurrency limiter
var maxConcurrency = config.GetValue<int>("Providers:Concurrency:MaxPerProvider", 10);
builder.Services.AddSingleton(new ProviderConcurrencyLimiter(maxConcurrency));

// Add mail providers
builder.Services.AddScoped<GmailMailProvider>();
builder.Services.AddScoped<OutlookMailProvider>();

// Add factory
builder.Services.AddScoped<IMailProviderFactory, MailProviderFactory>();

// Add event handler
builder.Services.AddScoped<IMailEventHandler, MailEventDispatcher>();

// Add webhook validator
builder.Services.AddScoped<IWebhookValidator, WebhookValidator>();

builder.Build().Run();

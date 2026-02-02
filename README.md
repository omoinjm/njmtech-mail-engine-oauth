# Mail Engine - Azure-Native Event-Driven Email Service

A production-grade, event-driven mail engine built on Azure Functions (.NET 8) and Azure Service Bus. Supports Gmail and Outlook/Microsoft Graph with pluggable provider architecture.

![Build Status](https://img.shields.io/badge/Build-Passing-brightgreen)
![License](https://img.shields.io/badge/License-MIT-blue)
![.NET](https://img.shields.io/badge/.NET-8.0-512BD4)

## 🎯 Overview

Mail Engine is a headless backend system for sending and receiving emails at scale. It uses Azure's event-driven architecture to handle email operations asynchronously with built-in concurrency control and resilience patterns.

### Key Features

✅ **Event-Driven Architecture**
- Azure Service Bus Topics & Subscriptions
- Asynchronous message processing
- Dead-letter queue handling
- Message deduplication

✅ **Multi-Provider Support**
- Gmail API (Google Workspace)
- Microsoft Graph (Office 365/Outlook)
- Plugin-ready architecture for additional providers

✅ **Production-Ready**
- Concurrency limiting (10 concurrent per provider)
- Token lifecycle management
- Key Vault integration for secrets
- Application Insights monitoring
- Structured logging with correlation IDs

✅ **Webhook Integration**
- Google Pub/Sub push notifications
- Microsoft Graph change notifications
- Validation token handling

## 🏗️ Architecture

### High-Level Flow

```
Email Request
    ↓
Service Bus Topic
    ↓
Azure Function (SendMailFunction or ReadInboxFunction)
    ↓
Provider (Gmail or Outlook)
    ↓
Email API (Gmail API or Microsoft Graph)
    ↓
User's Mailbox
```

### System Components

```
MailEngine.Core
├── Interfaces (IMailProvider, ITokenRepository, IMailEventHandler, IKeyVaultSecretProvider)
├── Models (MailEvent, SendMailEvent, ReadInboxEvent, OAuthToken, UserMailAccount)
└── Enums (ProviderType)

MailEngine.Functions
├── Functions
│   ├── SendMailFunction.cs          (ServiceBusTrigger)
│   ├── ReadInboxFunction.cs         (ServiceBusTrigger)
│   ├── GmailPushNotificationFunction.cs  (HttpTrigger)
│   └── GraphWebhookFunction.cs      (HttpTrigger)
├── Dispatching
│   ├── MailEventDispatcher.cs
│   └── ProviderConcurrencyLimiter.cs
└── Webhooks
    └── WebhookValidator.cs

MailEngine.Infrastructure
├── Data (MailEngineDbContext.cs)
├── KeyVault (KeyVaultSecretProvider.cs)
├── ServiceBus (ServiceBusPublisher.cs)
├── Factories (MailProviderFactory.cs)
├── TokenStore (TokenRepository.cs)
└── Resilience (RetryPolicy.cs)

MailEngine.Providers.Gmail
└── GmailMailProvider.cs

MailEngine.Providers.Outlook
└── OutlookMailProvider.cs
```

## 🚀 Getting Started

### Prerequisites

- .NET 8.0 SDK or later
- Azure Subscription
- Azure SQL Database (or local SQL Server for development)
- Azure Service Bus namespace
- Azure Key Vault instance
- Gmail API credentials (for Gmail support)
- Microsoft Graph credentials (for Outlook support)

### Local Development Setup

1. **Clone the repository**
```bash
git clone https://github.com/yourusername/mail-engine.git
cd mail-engine
```

2. **Install dependencies**
```bash
dotnet restore
```

3. **Configure local settings**
Create `src/MailEngine.Functions/local.settings.json`:
```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    "AzureServiceBus:ConnectionString": "Endpoint=sb://your-namespace.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=YOUR_KEY",
    "Database:ConnectionString": "Server=(local);Database=MailEngine;Integrated Security=true;",
    "KeyVault:Uri": "https://your-vault.vault.azure.net/"
  }
}
```

4. **Build the solution**
```bash
dotnet build
```

5. **Run tests**
```bash
dotnet test
```

6. **Start Azure Functions locally**
```bash
cd src/MailEngine.Functions
func start
```

### Production Deployment

1. **Build for release**
```bash
dotnet build --configuration Release
```

2. **Publish to Azure**
```bash
dotnet publish -c Release
```

3. **Deploy to Azure Functions**
```bash
# Using Azure Functions Core Tools
func azure functionapp publish your-function-app-name

# Or using Azure CLI
az functionapp deployment source config-zip -g your-resource-group -n your-function-app-name --src "publish.zip"
```

## 📝 Usage Examples

### Sending an Email via Service Bus

```csharp
var serviceBusClient = new ServiceBusClient(connectionString);
var sender = serviceBusClient.CreateSender("mail-send");

var sendMailEvent = new SendMailEvent
{
    MessageId = Guid.NewGuid(),
    CorrelationId = Guid.NewGuid(),
    ProviderType = ProviderType.Gmail,
    UserMailAccountId = new Guid("user-id"),
    To = "recipient@example.com",
    Subject = "Hello World",
    Body = "<h1>Hello</h1><p>This is an email.</p>"
};

var message = new ServiceBusMessage(JsonSerializer.Serialize(sendMailEvent))
{
    MessageId = sendMailEvent.MessageId.ToString(),
    CorrelationId = sendMailEvent.CorrelationId.ToString(),
    ApplicationProperties =
    {
        { "ProviderType", sendMailEvent.ProviderType.ToString() },
        { "UserMailAccountId", sendMailEvent.UserMailAccountId.ToString() }
    }
};

await sender.SendMessageAsync(message);
```

### Reading User's Inbox

```csharp
var readInboxEvent = new ReadInboxEvent
{
    MessageId = Guid.NewGuid(),
    CorrelationId = Guid.NewGuid(),
    ProviderType = ProviderType.Outlook,
    UserMailAccountId = new Guid("user-id")
};

// Publish to Service Bus
var publisher = new ServiceBusPublisher(connectionString);
await publisher.PublishMessageAsync("mail-inbox-read", readInboxEvent);
```

### Webhook Endpoint (Google Pub/Sub)

```
POST https://your-function-app.azurewebsites.net/api/webhooks/gmail

{
  "message": {
    "data": "base64-encoded-data",
    "messageId": "123456789"
  }
}
```

### Webhook Endpoint (Microsoft Graph)

```
POST https://your-function-app.azurewebsites.net/api/webhooks/graph

{
  "value": [
    {
      "changeType": "created",
      "clientState": "your-state",
      "resource": "/me/mailFolders('Inbox')/messages/123"
    }
  ]
}
```

## ⚙️ Configuration

### Application Settings

Configure via `appsettings.json` or environment variables:

```json
{
  "AzureServiceBus": {
    "ConnectionString": "Endpoint=sb://..."
  },
  "Database": {
    "ConnectionString": "Server=...;Database=MailEngine;..."
  },
  "KeyVault": {
    "Uri": "https://your-vault.vault.azure.net/"
  },
  "Providers": {
    "Concurrency": {
      "MaxPerProvider": 10
    }
  }
}
```

### Required Secrets in Key Vault

```
gmail-api-key                    (Gmail API key)
outlook-tenant-id               (Azure AD Tenant ID)
outlook-client-id               (Azure AD Client ID)
outlook-client-secret           (Azure AD Client Secret)
```

### Service Bus Configuration

**Topics to Create:**
- `mail-send` - Email sending requests
- `mail-inbox-read` - Inbox reading requests

**Subscriptions:**
- `mail-send` → `gmail`, `outlook`
- `mail-inbox-read` → `gmail`, `outlook`

**Settings:**
- Enable duplicate detection (1 hour window)
- Configure dead-letter queues
- Set max delivery count to 3

## 🧪 Testing

### Run All Tests
```bash
dotnet test
```

### Run Specific Test Project
```bash
dotnet test tests/MailEngine.Tests.Unit
dotnet test tests/MailEngine.Tests.Integration
```

### Run with Code Coverage
```bash
dotnet test /p:CollectCoverage=true /p:CoverageFormat=opencover
```

## 📊 Monitoring

### Application Insights

Mail Engine logs to Application Insights with:
- **Structured logging** - JSON format with correlation IDs
- **Custom metrics** - Provider performance, message count
- **Distributed tracing** - End-to-end request tracking
- **Alerts** - Failures, high latency, quota limits

### View Logs

```bash
# Using Azure CLI
az monitor app-insights metrics show -g your-resource-group --app your-app-insights-name

# Or use Azure Portal → Application Insights → Logs
```

## 🔐 Security

See [SECURITY.md](./SECURITY.md) for:
- Authentication and authorization
- Token management
- Secret rotation
- Webhook validation
- Network security
- Incident response

**Quick Security Checklist:**
- ✅ Secrets in Key Vault (never hardcoded)
- ✅ Managed Identity for Azure services
- ✅ HTTPS for all endpoints
- ✅ Correlation IDs for audit logging
- ✅ Rate limiting per provider
- ✅ Input validation on webhooks

## 📦 Project Status

| Component | Status | Notes |
|-----------|--------|-------|
| Core Infrastructure | ✅ Complete | Full DI, config, logging |
| Email Sending | ✅ Complete | Gmail & Outlook |
| Inbox Reading | ✅ Complete | Gmail & Outlook |
| Webhook Support | ✅ Complete | Google & Microsoft |
| Token Management | ⚠️ Partial | Refresh logic needs OAuth flow |
| Integration Tests | ⚠️ Partial | Test containers needed |
| CI/CD Pipeline | ❌ Not Started | GitHub Actions ready |
| Documentation | ✅ Complete | README, SECURITY, API docs |

## 🤝 Contributing

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

### Development Guidelines
- Follow C# coding standards (StyleCop)
- Use async/await everywhere
- Write unit tests for new logic
- Update documentation
- Keep commits atomic

## 📚 Documentation

- [Architecture Guide](./docs/ARCHITECTURE.md) - Detailed system design
- [Security Guide](./SECURITY.md) - Security considerations
- [API Reference](./docs/API.md) - Function signatures and models
- [Setup Guide](./docs/SETUP.md) - Detailed deployment instructions

## 🐛 Known Issues

1. **Token Refresh** - Currently uses stored tokens; needs OAuth refresh flow
2. **Integration Tests** - Require test containers for Service Bus
3. **Webhook Validation** - HMAC validation not yet implemented
4. **Rate Limiting** - Per-user rate limits not yet enforced

See [Issues](https://github.com/yourusername/mail-engine/issues) for more.

## 📋 Roadmap

### Q1 2024
- [ ] Complete OAuth token refresh flow
- [ ] Implement webhook signature validation
- [ ] Add integration tests with test containers
- [ ] Create Bicep templates for infrastructure

### Q2 2024
- [ ] CI/CD pipeline (GitHub Actions)
- [ ] Performance optimization
- [ ] Additional email providers (ProtonMail, etc.)
- [ ] Admin dashboard

### Q3 2024
- [ ] Multi-tenant support
- [ ] Advanced filtering and search
- [ ] Batch operations
- [ ] Custom provider SDK

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](./LICENSE) file for details.

## 📞 Support

For issues, questions, or suggestions:
1. Check [existing issues](https://github.com/yourusername/mail-engine/issues)
2. Open a [new issue](https://github.com/yourusername/mail-engine/issues/new)
3. Contact the maintainers

## 🙏 Acknowledgments

- Built with [Azure Functions](https://docs.microsoft.com/en-us/azure/azure-functions/)
- Email APIs: [Gmail API](https://developers.google.com/gmail/api) and [Microsoft Graph](https://docs.microsoft.com/en-us/graph/)
- Architecture inspired by event-driven systems best practices

---

**Made with ❤️ for email automation**

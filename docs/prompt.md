You are a senior Azure cloud architect and .NET engineer.

Design and implement a production-grade, Azure-native, event-driven Mail Engine
using Azure Functions (.NET 8 isolated) and Azure Service Bus.

This is a headless backend system:

- Users are already authenticated
- OAuth tokens already exist in the database
- No UI
- No OAuth login flows
- No SMTP or IMAP

---

1. AZURE-NATIVE ARCHITECTURE

---

Core technologies:

- Azure Functions (.NET 8 isolated worker)
- Azure Service Bus (Topics + Subscriptions)
- Azure SQL / Cosmos DB (for tokens & accounts)
- Azure Key Vault (secrets)
- Application Insights (logging & metrics)

Design goals:

- Fully event-driven
- Serverless, auto-scaling
- High throughput
- Resilient and idempotent

---

2. SOLUTION STRUCTURE

---

MailEngine.sln
│
├── MailEngine.Functions
│ ├── Functions
│ │ ├── SendMailFunction.cs
│ │ ├── ReadInboxFunction.cs
│ │ ├── GmailPushNotificationFunction.cs
│ │ └── GraphWebhookFunction.cs
│ ├── Dispatching
│ │ ├── MailEventDispatcher.cs
│ │ └── ProviderConcurrencyLimiter.cs
│ ├── Program.cs
│ └── host.json
│
├── MailEngine.Core
│ ├── Interfaces
│ │ ├── IMailProvider.cs
│ │ ├── IMailProviderFactory.cs
│ │ ├── ITokenRepository.cs
│ │ └── IMailEventHandler.cs
│ ├── Models
│ │ ├── MailEvent.cs
│ │ ├── SendMailEvent.cs
│ │ ├── ReadInboxEvent.cs
│ │ ├── OAuthToken.cs
│ │ └── UserMailAccount.cs
│
├── MailEngine.Providers.Gmail
│ ├── GmailMailProvider.cs
│ └── GmailPushProcessor.cs
│
├── MailEngine.Providers.Outlook
│ ├── OutlookMailProvider.cs
│ └── GraphWebhookProcessor.cs
│
├── MailEngine.Infrastructure
│ ├── Data
│ │ └── MailEngineDbContext.cs
│ ├── TokenStore
│ │ └── TokenRepository.cs
│ ├── ServiceBus
│ │ └── ServiceBusPublisher.cs
│ ├── Resilience
│ │ └── RetryPolicy.cs
│ └── Security
│ └── TokenEncryption.cs
│
├── MailEngine.Tests.Unit
└── MailEngine.Tests.Integration

---

3. AZURE SERVICE BUS DESIGN

---

Use Azure Service Bus Topics.

Topics:

- mail-send
- mail-inbox-read

Subscriptions:

- gmail
- outlook

Message requirements:

- MessageId (for deduplication)
- CorrelationId
- ProviderType
- UserMailAccountId
- Payload (JSON)

Enable:

- Duplicate detection
- Dead-letter queues
- Max delivery count

---

4. AZURE FUNCTION TRIGGERS

---

Functions:

SendMailFunction:

- ServiceBusTrigger (mail-send topic)
- Resolves provider
- Sends email asynchronously

ReadInboxFunction:

- ServiceBusTrigger (mail-inbox-read topic)
- Reads inbox changes
- Maps messages to domain models

GmailPushNotificationFunction:

- HttpTrigger
- Receives Google Pub/Sub push messages
- Validates request
- Publishes ReadInboxEvent to Service Bus

GraphWebhookFunction:

- HttpTrigger
- Handles validation tokens
- Processes Graph notifications
- Publishes ReadInboxEvent to Service Bus

---

5. CONCURRENCY & PARALLELISM

---

Implement provider-level concurrency limits:

- Max 10 concurrent operations per provider
- Use ProviderConcurrencyLimiter:
  - ConcurrentDictionary<ProviderType, SemaphoreSlim>
  - SemaphoreSlim initialized to 10 per provider

Requirements:

- Async/await everywhere
- No thread blocking
- Safe under Azure Functions scaling
- Graceful shutdown handling

---

6. PROVIDER ABSTRACTION (PLUG-IN READY)

---

IMailProvider:

- SendEmailAsync
- ReadInboxAsync
- ProviderType

Providers:

- Gmail provider using Gmail API
- Outlook provider using Microsoft Graph API
- Providers are stateless
- Resolved via factory

Adding a provider requires:

- New provider project
- No changes to Functions

---

7. TOKEN MANAGEMENT

---

- Tokens stored in database (already authenticated)
- Validate token before each call
- Refresh expired tokens
- Handle revoked refresh tokens gracefully

Optional:

- Timer-triggered function for proactive token refresh

---

8. RELIABILITY & SCALE

---

- Functions must be idempotent
- Safe message retries
- Dead-letter handling
- Correlation-based logging
- Application Insights telemetry

Scale characteristics:

- Azure Functions auto-scale
- Service Bus controls load
- Provider concurrency limits protect APIs

---

9. TESTING REQUIREMENTS

---

Unit Tests:

- Provider concurrency limiter
- Token refresh logic
- Event dispatching
- Provider factory

Integration Tests:

- Azure Service Bus emulator or test containers
- HTTP trigger tests for webhooks
- Concurrent message processing scenarios

No real Google or Microsoft API calls.

---

10. SECURITY

---

- Secrets in Azure Key Vault
- Managed Identity for Service Bus
- Token encryption at rest
- Validate webhook signatures

---

11. OUTPUT EXPECTATIONS

---

Generate:

- Azure Functions (isolated .NET 8)
- Service Bus triggers & publishers
- Gmail push & Graph webhook handlers
- Provider implementations
- Concurrency-limited async processing
- Unit & integration tests
- host.json configuration

Do NOT:

- Implement UI or OAuth login
- Use SMTP or IMAP
- Hardcode secrets

Assume production-scale Azure deployment.

Proceed step by step, generating files in the correct projects and namespaces.

# Copilot Instructions - Mail Engine

## Build, Test & Lint Commands

### Building
```bash
# Full solution build
dotnet build

# Release build
dotnet build --configuration Release
```

### Testing
```bash
# Run all tests
dotnet test

# Run unit tests only
dotnet test tests/MailEngine.Tests.Unit

# Run integration tests only
dotnet test tests/MailEngine.Tests.Integration

# Run specific test class
dotnet test tests/MailEngine.Tests.Unit --filter MailEventDispatcherTests

# Run with code coverage
dotnet test /p:CollectCoverage=true /p:CoverageFormat=opencover
```

### Database Migrations
```bash
# Generate a migration (after schema changes)
./scripts/generate-migration.sh "YourMigrationName"

# Apply migrations locally
cd src/MailEngine.Infrastructure
dotnet ef database update --startup-project ../MailEngine.Functions

# Rollback to previous migration
dotnet ef database update "PreviousMigrationName" --startup-project ../MailEngine.Functions

# View pending migrations
dotnet ef migrations list --startup-project ../MailEngine.Functions
```

### Running Locally
```bash
# Start Azure Functions locally
cd src/MailEngine.Functions
func start
```

## Architecture Overview

### Core Design Pattern: Event-Driven Pipeline

The system follows a **Service Bus → Azure Functions → Provider** pipeline:

1. **Service Bus Topics** (`mail-send`, `mail-inbox-read`) receive email events
2. **Azure Functions** (triggered by subscriptions) process events asynchronously  
3. **MailProviderFactory** instantiates the correct provider (Gmail/Outlook)
4. **IMailProvider** implementations (GmailMailProvider, OutlookMailProvider) execute API calls
5. **ProviderConcurrencyLimiter** enforces 10 concurrent operations per provider
6. **Failed messages** are captured in database for monitoring

### Project Structure

```
MailEngine.Core
├── Interfaces (IMailProvider, IMailProviderFactory, IMailEventHandler, IFailedMessageLogger, IWebhookValidator)
├── Models (SendMailEvent, ReadInboxEvent, OAuthToken, UserMailAccount, FailedMessage)
└── Enums (ProviderType: Gmail, Outlook)

MailEngine.Functions (Azure Functions Worker)
├── Functions
│   ├── SendMailFunction (ServiceBusTrigger) - Sends email via providers
│   ├── ReadInboxFunction (ServiceBusTrigger) - Reads inbox messages
│   ├── GmailPushNotificationFunction (HttpTrigger) - Google Pub/Sub webhook
│   └── GraphWebhookFunction (HttpTrigger) - Microsoft Graph webhook
├── Dispatching
│   ├── MailEventDispatcher (IMailEventHandler) - Routes events to providers
│   └── ProviderConcurrencyLimiter - Limits concurrent calls (max 10 per provider)
├── Services
│   ├── FailedMessageLogger - Captures and logs failed messages to database
│   └── WebhookValidator - Validates incoming webhook payloads
└── Program.cs (Dependency Injection setup)

MailEngine.Infrastructure (Data & Services)
├── Data
│   ├── MailEngineDbContext (EF Core context)
│   └── Migrations (EF Core-managed, manual application)
├── KeyVault (KeyVaultSecretProvider)
├── ServiceBus (ServiceBusPublisher)
├── Factories (MailProviderFactory - creates Gmail/Outlook providers based on ProviderType)
├── TokenStore (TokenRepository - retrieves OAuth tokens from database)
└── Resilience (RetryPolicy)

MailEngine.Providers.Gmail
└── GmailMailProvider (implements IMailProvider for Gmail API)

MailEngine.Providers.Outlook
└── OutlookMailProvider (implements IMailProvider for Microsoft Graph)
```

### Dependency Injection Setup

In `Program.cs`:
- **ProviderConcurrencyLimiter** registered as Singleton (shared across all invocations)
- **IMailEventHandler** → MailEventDispatcher (Scoped)
- **IFailedMessageLogger** → FailedMessageLogger (Scoped)
- **IWebhookValidator** → WebhookValidator (Scoped)
- **MailEngineDbContext** supports both PostgreSQL and SQL Server via configuration

### Database Schema

Three core tables (all use UUIDs as primary keys):

1. **oauth_tokens** - OAuth credentials (AccessToken, RefreshToken, ExpiresAtUtc)
2. **user_mail_accounts** - User email address & provider type mapping
3. **failed_messages** - Dead-letter queue tracking with error details, retry count, and resolution status

Migrations are **manual** (not automatic on startup) for safety. See `docs/MIGRATIONS.md` for details.

## Key Conventions

### Event Model Pattern
- Events inherit from `MailEvent` base class with `MessageId`, `CorrelationId`, `ProviderType`
- Specific event types: `SendMailEvent`, `ReadInboxEvent` (in `MailEngine.Core.Models`)
- Events are published to Service Bus Topics via `IServiceBusPublisher`

### Factory Pattern
- **MailProviderFactory** creates provider instances based on `ProviderType` enum
- All providers implement `IMailProvider` interface (enforces `SendEmailAsync`, `ReadInboxAsync` signatures)
- New providers are added by implementing `IMailProvider` + registering in factory

### Concurrency Control
- **ProviderConcurrencyLimiter** uses SemaphoreSlim per provider (10 concurrent limit)
- Call `await _concurrencyLimiter.WaitForSlotAsync(providerType)` before provider calls
- Call `_concurrencyLimiter.ReleaseSlot(providerType)` in finally block

### Naming Conventions (PostgreSQL)
- Table names: `snake_case` (e.g., `oauth_tokens`, `user_mail_accounts`)
- Column names: `snake_case` (e.g., `oauth_token_id`, `user_mail_account_id`)
- EF Core mappings use `ToTable("table_name")` and `HasColumnName("column_name")`
- All primary key GUIDs auto-generated via `HasDefaultValueSql("gen_random_uuid()")`

### Error Handling
- Failed messages captured via `IFailedMessageLogger.LogFailedMessageAsync()`
- Stores to `FailedMessages` table for async processing
- `MonitorDLQFunction` (runs every 5 minutes) processes dead-letter queue
- Error classification: **Permanent** (401, malformed) vs **Transient** (timeout, rate limit)

### Testing
- **MSTest** framework with **Moq** for mocking
- Test classes follow `{ComponentName}Tests` naming (e.g., `MailEventDispatcherTests`)
- Initialize mocks in `[TestInitialize]` method
- Use `[TestMethod]` for test cases

### Service Bus Configuration
- Topics: `mail-send`, `mail-inbox-read`
- Subscriptions per topic: `gmail`, `outlook`
- Deduplication enabled (1-hour window)
- Dead-letter queues configured on subscriptions
- Max delivery count: 3

### Configuration Sources (Priority Order)
1. **local.settings.json** (development)
2. **Environment variables** (Azure Functions configuration)
3. **Azure Key Vault** (production secrets via `KeyVaultSecretProvider`)

## Provider Implementation Pattern

When adding a new email provider:

1. Create new project: `MailEngine.Providers.{ProviderName}`
2. Implement `IMailProvider` interface:
   ```csharp
   public class {ProviderName}MailProvider : IMailProvider
   {
       public ProviderType ProviderType => ProviderType.{ProviderName};
       
       public async Task SendEmailAsync(SendMailEvent mailEvent, CancellationToken ct)
       {
           // Get token from ITokenRepository
           // Call provider API
           // Handle errors via IFailedMessageLogger
       }
       
       public async Task ReadInboxAsync(ReadInboxEvent inboxEvent, CancellationToken ct)
       {
           // Fetch inbox messages from provider API
       }
   }
   ```
3. Add new `ProviderType.{ProviderName}` to enum in `MailEngine.Core`
4. Register in `MailProviderFactory` switch statement

## Webhook Validation

Both Gmail (Pub/Sub) and Graph (Microsoft) webhooks validate via `IWebhookValidator`:
- Google Pub/Sub: Validates JWT signature
- Microsoft Graph: Validates POST body signature with shared secret (clientState)

Webhook endpoints automatically handle validation token exchanges.

---

# Expanded Reference Guide

## Detailed Architecture & Data Flow

### Email Sending Flow

```
1. Client publishes SendMailEvent to Service Bus topic "mail-send"
   ↓
2. SendMailFunction triggered by subscription (gmail or outlook)
   ↓
3. Function extracts UserMailAccountId from message ApplicationProperties
   ↓
4. MailEventDispatcher.HandleEventAsync() called with SendMailEvent
   ↓
5. MailProviderFactory creates correct provider (Gmail or Outlook)
   ↓
6. ProviderConcurrencyLimiter waits for available slot (max 10 concurrent)
   ↓
7. Provider retrieves OAuth token from TokenRepository
   ↓
8. Provider calls Gmail API or Microsoft Graph
   ↓
9. On success: Message processed, function completes
   ↓
10. On failure:
    - Permanent errors (401, 403, bad format) → FailedMessageLogger marks as permanent
    - Transient errors (timeout, 429) → Azure Service Bus retries (max 3 times)
    - If max retries exceeded → Message moved to dead-letter queue
    - MonitorDLQFunction detects and logs for manual review
```

### Inbox Reading Flow

Similar to sending, but reads messages from user's mailbox:

```
ReadInboxEvent → Service Bus → ReadInboxFunction → Provider.ReadInboxAsync()
→ Returns email list (currently logged/stored via custom logic)
```

### Webhook Processing Flow

```
Gmail Push Notification (Google Pub/Sub):
  1. HttpTrigger receives POST with base64-encoded message
  2. WebhookValidator verifies JWT signature using Google's public key
  3. Decodes message to extract notification
  4. Publishes ReadInboxEvent to "mail-inbox-read" topic
  5. ReadInboxFunction processes asynchronously

Microsoft Graph Webhook:
  1. HttpTrigger receives POST with subscription notification
  2. WebhookValidator checks clientState matches configured secret
  3. Validates POST signature using HMAC
  4. Publishes ReadInboxEvent for affected mailbox
  5. ReadInboxFunction processes asynchronously
```

## Entity Framework Core Setup

### DbContext Configuration (MailEngineDbContext)

```csharp
// src/MailEngine.Infrastructure/Data/MailEngineDbContext.cs

public DbSet<OAuthToken> OAuthTokens { get; set; }
public DbSet<UserMailAccount> UserMailAccounts { get; set; }
public DbSet<FailedMessage> FailedMessages { get; set; }

// OnModelCreating configures:
// - Table naming: snake_case (oauth_tokens, user_mail_accounts)
// - Primary key generation: UUID via gen_random_uuid()
// - Foreign key relationships
// - Indexes on frequently queried columns (Status, Topic in FailedMessages)
```

### Database Switching (PostgreSQL vs SQL Server)

In `Program.cs`:

```csharp
var dbProvider = config.GetValue<string>("DatabaseProvider") ?? "PostgreSQL";

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
```

Set `DatabaseProvider` in `local.settings.json` or environment:
```json
"Values": {
  "DatabaseProvider": "PostgreSQL"  // or "SQLServer"
}
```

### Migration Workflow

**Local Development:**
```bash
# 1. Make schema changes in model classes
# 2. Generate migration
./scripts/generate-migration.sh "AddNewColumn"

# 3. Review generated migration file
cat src/MailEngine.Infrastructure/Migrations/[timestamp]_AddNewColumn.cs

# 4. Apply to local database
cd src/MailEngine.Infrastructure
dotnet ef database update --startup-project ../MailEngine.Functions

# 5. Commit migration to git
git add src/MailEngine.Infrastructure/Migrations/
```

**Staging Deployment (GitHub Actions - Automatic):**
- On push to staging branch, workflow generates and auto-applies migration
- Connection string: `${{ secrets.STAGING_DATABASE_CONNECTION_STRING }}`

**Production Deployment (GitHub Actions - Manual):**
- On push to main branch, workflow generates migration script
- Script uploaded as artifact for review
- Manual approval required before applying to production

## Service Bus Integration

### Topic & Subscription Setup

**Create Topics** (in Azure Portal or IaC):

```
Topic: mail-send
  ├─ Subscription: gmail
  ├─ Subscription: outlook
  └─ Settings:
     - Duplicate detection: 1 hour window
     - Max delivery count: 3
     - Dead-letter on max delivery: enabled

Topic: mail-inbox-read
  ├─ Subscription: gmail
  ├─ Subscription: outlook
  └─ Settings: (same as above)
```

### Message Publishing

```csharp
var serviceBusClient = new ServiceBusClient(connectionString);
var sender = serviceBusClient.CreateSender("mail-send");

var sendMailEvent = new SendMailEvent
{
    MessageId = Guid.NewGuid(),
    CorrelationId = Guid.NewGuid(),  // For tracking across distributed calls
    ProviderType = ProviderType.Gmail,
    UserMailAccountId = userId,
    To = "recipient@example.com",
    Subject = "Hello",
    Body = "<p>Content</p>"
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

### Message Consumption

In Azure Functions (Service Bus Trigger):

```csharp
[Function("SendMailFunction")]
public async Task Run(
    [ServiceBusTrigger("mail-send", "gmail", Connection = "AzureServiceBus:ConnectionString")]
    ServiceBusReceivedMessage message,
    FunctionContext context)
{
    var sendMailEvent = JsonSerializer.Deserialize<SendMailEvent>(message.Body.ToString());
    var userMailAccountId = message.ApplicationProperties["UserMailAccountId"].ToString();
    
    await _mailEventHandler.HandleEventAsync(sendMailEvent, context.CancellationToken);
}
```

### Dead-Letter Queue Monitoring

Service Bus automatically moves messages to DLQ when:
- Max delivery count exceeded (default: 3)
- Message fails deserialization
- Subscription filter evaluation throws exception

**MonitorDLQFunction** runs on timer trigger (every 5 minutes):

```csharp
[Function("MonitorDLQFunction")]
public async Task Run(
    [TimerTrigger("0 */5 * * * *")] TimerInfo timer,
    FunctionContext context)
{
    var serviceBusClient = new ServiceBusClient(connectionString);
    
    // Check DLQ for mail-send topic
    var receiver = serviceBusClient.CreateReceiver("mail-send/$DeadLetterQueue");
    var messages = await receiver.ReceiveMessagesAsync(maxMessages: 100);
    
    foreach (var msg in messages)
    {
        await _failedMessageLogger.LogFailedMessageAsync(
            messageId: msg.MessageId,
            topic: "mail-send",
            errorMessage: msg.DeadLetterReason,
            errorStackTrace: msg.DeadLetterErrorDescription,
            cancellationToken: context.CancellationToken
        );
        
        await receiver.CompleteMessageAsync(msg);
    }
}
```

## OAuth Token Management

### Token Storage & Retrieval

Tokens stored in `OAuthTokens` table:

```csharp
public class OAuthToken
{
    public Guid OAuthTokenId { get; set; }
    public Guid UserMailAccountId { get; set; }
    public string AccessToken { get; set; }        // Current access token
    public string RefreshToken { get; set; }       // Long-lived refresh token
    public DateTime ExpiresAtUtc { get; set; }     // Access token expiration
}
```

### Token Retrieval in Providers

```csharp
// In Gmail/Outlook provider implementation
var token = await _tokenRepository.GetTokenAsync(userMailAccountId);

if (token.ExpiresAtUtc < DateTime.UtcNow)
{
    // Token expired - need refresh (currently handled by separate OAuth service)
    throw new ExpiredTokenException("Token requires refresh");
}

// Use token in API call
var client = new HttpClient();
client.DefaultRequestHeaders.Authorization = 
    new AuthenticationHeaderValue("Bearer", token.AccessToken);

var response = await client.SendAsync(request);
```

### Token Refresh (Current Architecture)

⚠️ **Important**: Mail Engine does NOT handle token refresh. A separate OAuth service:
1. Stores initial tokens in database
2. Monitors expiration
3. Calls Google/Microsoft OAuth endpoints to refresh
4. Updates database with new tokens
5. Mail Engine reads fresh tokens from database

This separation ensures:
- Mail Engine remains stateless
- Token refresh can be scaled independently
- Credentials are managed centrally

## Provider Implementation Details

### Gmail Provider

**File**: `src/MailEngine.Providers.Gmail/GmailMailProvider.cs`

```csharp
public class GmailMailProvider : IMailProvider
{
    public ProviderType ProviderType => ProviderType.Gmail;
    
    public async Task SendEmailAsync(SendMailEvent mailEvent, CancellationToken cancellationToken)
    {
        // 1. Get OAuth token
        var token = await _tokenRepository.GetTokenAsync(mailEvent.UserMailAccountId);
        
        // 2. Create Gmail API client with token
        var service = new GmailService(new BaseClientService.Initializer()
        {
            HttpClientInitializer = new FirebaseCredential(token.AccessToken),
            ApplicationName = "MailEngine"
        });
        
        // 3. Build email message
        var message = new Message();
        message.Raw = Base64UrlEncode(BuildMimeMessage(mailEvent));
        
        // 4. Send via API
        var request = service.Users.Messages.Send(message, "me");
        var result = await request.ExecuteAsync(cancellationToken);
        
        // On success: returns message object with id
        // On error: throws GoogleApiException (catch and log)
    }
    
    public async Task ReadInboxAsync(ReadInboxEvent inboxEvent, CancellationToken cancellationToken)
    {
        // Similar flow: get token → create client → call API
        var service = new GmailService(...);
        var request = service.Users.Messages.List("me");
        var result = await request.ExecuteAsync(cancellationToken);
        
        // result.Messages contains list of message metadata
        // Call .Get() on each to fetch full message details if needed
    }
}
```

**Gmail API Endpoints Used:**
- `users.messages.send` - Send email
- `users.messages.list` - List messages in mailbox
- `users.messages.get` - Fetch specific message
- `users.watch` - Setup webhook notifications

### Outlook/Microsoft Graph Provider

**File**: `src/MailEngine.Providers.Outlook/OutlookMailProvider.cs`

```csharp
public class OutlookMailProvider : IMailProvider
{
    public ProviderType ProviderType => ProviderType.Outlook;
    
    public async Task SendEmailAsync(SendMailEvent mailEvent, CancellationToken cancellationToken)
    {
        // 1. Get OAuth token
        var token = await _tokenRepository.GetTokenAsync(mailEvent.UserMailAccountId);
        
        // 2. Create Graph client
        var client = new HttpClient();
        client.DefaultRequestHeaders.Authorization = 
            new AuthenticationHeaderValue("Bearer", token.AccessToken);
        
        // 3. Build email payload
        var sendMailBody = new
        {
            message = new
            {
                subject = mailEvent.Subject,
                body = new { contentType = "HTML", content = mailEvent.Body },
                toRecipients = new[] { new { emailAddress = new { address = mailEvent.To } } }
            }
        };
        
        // 4. Send via Graph
        var response = await client.PostAsJsonAsync(
            "https://graph.microsoft.com/v1.0/me/sendMail",
            sendMailBody,
            cancellationToken
        );
        
        response.EnsureSuccessStatusCode();
    }
    
    public async Task ReadInboxAsync(ReadInboxEvent inboxEvent, CancellationToken cancellationToken)
    {
        // Similar flow with different endpoints
        var response = await client.GetAsync(
            "https://graph.microsoft.com/v1.0/me/mailFolders/inbox/messages",
            cancellationToken
        );
    }
}
```

**Microsoft Graph Endpoints Used:**
- `POST /me/sendMail` - Send email
- `GET /me/mailFolders/inbox/messages` - List inbox messages
- `GET /me/messages/{id}` - Fetch specific message
- `POST /subscriptions` - Setup webhook notifications

## Error Classification & Handling

### Error Categories

**Permanent Errors** (should not retry):
- `401 Unauthorized` - OAuth token invalid/expired
- `403 Forbidden` - User doesn't have permission
- `400 Bad Request` - Malformed email data
- `422 Unprocessable Entity` - Invalid email address format

**Transient Errors** (should retry):
- `429 Too Many Requests` - Rate limit (retry with backoff)
- `502 Bad Gateway` - Temporary service issue
- `503 Service Unavailable` - Provider maintenance
- `504 Gateway Timeout` - Slow response
- `TimeoutException` - Network timeout

### Error Handling in Providers

```csharp
try
{
    await provider.SendEmailAsync(sendMailEvent, cancellationToken);
}
catch (GoogleApiException ex) when (ex.HttpStatusCode == System.Net.HttpStatusCode.Unauthorized)
{
    // Permanent: Token expired
    await _failedMessageLogger.LogFailedMessageAsync(
        messageId: sendMailEvent.MessageId.ToString(),
        topic: "mail-send",
        errorMessage: "Unauthorized - Token expired",
        isPermanent: true
    );
    // Do not retry - Service Bus will discard after max retries
}
catch (GoogleApiException ex) when (ex.HttpStatusCode == System.Net.HttpStatusCode.TooManyRequests)
{
    // Transient: Rate limit
    await _failedMessageLogger.LogFailedMessageAsync(
        messageId: sendMailEvent.MessageId.ToString(),
        topic: "mail-send",
        errorMessage: "Too Many Requests - Rate limited",
        isPermanent: false
    );
    // Throw to let Service Bus retry
    throw;
}
catch (HttpRequestException ex) when (ex.InnerException is TimeoutException)
{
    // Transient: Timeout
    throw;  // Retry
}
```

### FailedMessage Table Schema

```sql
CREATE TABLE failed_messages (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    message_id UUID NOT NULL,
    topic VARCHAR(255) NOT NULL,
    subscription VARCHAR(255),
    error_message TEXT,
    error_stack_trace TEXT,
    failed_at_utc TIMESTAMP DEFAULT NOW(),
    resolved_at_utc TIMESTAMP,
    status VARCHAR(50),  -- 'pending', 'permanent', 'transient', 'resolved'
    retry_count INT DEFAULT 0,
    last_retry_at_utc TIMESTAMP
);

CREATE INDEX idx_failed_messages_status ON failed_messages(status);
CREATE INDEX idx_failed_messages_topic ON failed_messages(topic);
```

## Concurrency Control Details

### ProviderConcurrencyLimiter Implementation

```csharp
public class ProviderConcurrencyLimiter
{
    private readonly Dictionary<ProviderType, SemaphoreSlim> _semaphores;
    
    public ProviderConcurrencyLimiter(int maxConcurrencyPerProvider)
    {
        // Create semaphore for each provider type
        _semaphores = new Dictionary<ProviderType, SemaphoreSlim>
        {
            { ProviderType.Gmail, new SemaphoreSlim(maxConcurrencyPerProvider) },
            { ProviderType.Outlook, new SemaphoreSlim(maxConcurrencyPerProvider) }
        };
    }
    
    public async Task WaitForSlotAsync(ProviderType providerType)
    {
        // Waits until a slot is available
        await _semaphores[providerType].WaitAsync();
    }
    
    public void ReleaseSlot(ProviderType providerType)
    {
        _semaphores[providerType].Release();
    }
}
```

### Usage Pattern

```csharp
try
{
    await _concurrencyLimiter.WaitForSlotAsync(mailEvent.ProviderType);
    
    var provider = _factory.CreateProvider(mailEvent.ProviderType);
    await provider.SendEmailAsync(mailEvent, cancellationToken);
}
finally
{
    _concurrencyLimiter.ReleaseSlot(mailEvent.ProviderType);
}
```

**Why This Approach:**
- Prevents provider API rate limits (both providers limit concurrent connections)
- Simple, reliable semaphore-based approach
- Works across multiple Azure Function instances (semaphore is per-instance)
- For true cross-instance limiting, consider Azure Service Bus rate limiting or dedicated throttling service

## Webhook Validation Implementation

### Google Pub/Sub Validation

```csharp
[Function("GmailPushNotificationFunction")]
public async Task Run(
    [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "webhooks/gmail")]
    HttpRequest req,
    FunctionContext context)
{
    var body = await new StreamReader(req.Body).ReadToEndAsync();
    var pubsubMessage = JsonSerializer.Deserialize<GmailPubSubMessage>(body);
    
    // Validate JWT signature
    if (!_webhookValidator.ValidateGmailSignature(pubsubMessage.Message.Attributes))
    {
        context.GetLogger("Gmail").LogError("Invalid Gmail signature");
        return new UnauthorizedResult();
    }
    
    // Decode message
    var decodedData = Convert.FromBase64String(pubsubMessage.Message.Data);
    var notification = JsonSerializer.Deserialize<GmailNotification>(
        Encoding.UTF8.GetString(decodedData)
    );
    
    // Publish ReadInboxEvent
    await _serviceBusPublisher.PublishMessageAsync(
        "mail-inbox-read",
        new ReadInboxEvent
        {
            MessageId = Guid.NewGuid(),
            CorrelationId = Guid.Parse(notification.HistoryId),
            ProviderType = ProviderType.Gmail,
            UserMailAccountId = notification.EmailAddress  // Or lookup from notification
        }
    );
    
    return new OkResult();
}
```

### Microsoft Graph Webhook Validation

```csharp
[Function("GraphWebhookFunction")]
public async Task Run(
    [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "webhooks/graph")]
    HttpRequest req,
    FunctionContext context)
{
    // Step 1: Handle validation token request
    if (req.Query.ContainsKey("validationToken"))
    {
        var validationToken = req.Query["validationToken"].ToString();
        // Return validation token as response
        await req.HttpContext.Response.WriteAsync(validationToken);
        return;
    }
    
    // Step 2: Validate webhook signature
    var body = await new StreamReader(req.Body).ReadToEndAsync();
    var clientState = req.Headers["client-state"].ToString();
    
    if (!_webhookValidator.ValidateGraphSignature(body, clientState))
    {
        context.GetLogger("Graph").LogError("Invalid Graph signature");
        return new UnauthorizedResult();
    }
    
    // Step 3: Process notifications
    var notification = JsonSerializer.Deserialize<GraphNotification>(body);
    
    foreach (var change in notification.Value)
    {
        // change.Resource contains mailbox path like /me/mailFolders('Inbox')/messages
        
        await _serviceBusPublisher.PublishMessageAsync(
            "mail-inbox-read",
            new ReadInboxEvent
            {
                MessageId = Guid.NewGuid(),
                CorrelationId = Guid.NewGuid(),
                ProviderType = ProviderType.Outlook,
                UserMailAccountId = ExtractUserIdFromResource(change.Resource)
            }
        );
    }
    
    return new OkResult();
}
```

## Configuration Management

### Configuration Hierarchy

```
1. local.settings.json (Development)
   └─ Overrides everything locally

2. Environment Variables (Azure Functions)
   └─ Set via function app settings in Azure Portal or IaC

3. Azure Key Vault (Production Secrets)
   └─ Referenced via managed identity
   └─ Prefix with "vault://" in settings

4. Application defaults (Program.cs)
   └─ Fallback values
```

### Example local.settings.json

```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    "AzureServiceBus:ConnectionString": "Endpoint=sb://your-namespace.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=YOUR_KEY",
    "DatabaseProvider": "PostgreSQL",
    "ProviderConcurrencyLimit": "10",
    "LogLevel:Default": "Information",
    "LogLevel:MailEngine": "Debug"
  },
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=mail_engine_dev;Username=postgres;Password=postgres"
  }
}
```

### Example Azure Key Vault References

In Azure Portal function app settings:

```
Key: DatabaseConnectionString
Value: @Microsoft.KeyVault(SecretUri=https://your-vault.vault.azure.net/secrets/mail-engine-db-connection/version)

Key: GmailClientSecret
Value: @Microsoft.KeyVault(SecretUri=https://your-vault.vault.azure.net/secrets/gmail-client-secret/version)

Key: GraphClientSecret
Value: @Microsoft.KeyVault(SecretUri=https://your-vault.vault.azure.net/secrets/graph-client-secret/version)
```

## Application Insights Integration

### Logging Setup

```csharp
// In Program.cs
builder.Services.AddApplicationInsightsTelemetry();

// Configure log levels
builder.Services.Configure<LoggerFilterOptions>(options =>
{
    options.MinimumLevel = LogLevel.Information;
});
```

### Structured Logging Pattern

```csharp
public async Task HandleEventAsync(MailEvent mailEvent, CancellationToken cancellationToken)
{
    _logger.LogInformation(
        "Processing mail event: {MessageId} {EventType} for provider {Provider}",
        mailEvent.MessageId,
        mailEvent.GetType().Name,
        mailEvent.ProviderType
    );
    
    try
    {
        // Process event
        await provider.SendEmailAsync(sendMailEvent, cancellationToken);
        
        _logger.LogInformation(
            "Mail event completed successfully: {MessageId}",
            mailEvent.MessageId
        );
    }
    catch (Exception ex)
    {
        _logger.LogError(
            ex,
            "Mail event failed: {MessageId} {CorrelationId}",
            mailEvent.MessageId,
            mailEvent.CorrelationId
        );
        throw;
    }
}
```

### Querying Application Insights

```kusto
// Find all failed messages for Gmail provider in last 24 hours
traces
| where timestamp > ago(24h)
| where message contains "Mail event failed"
| where customDimensions.Provider == "Gmail"
| summarize count() by tostring(customDimensions.MessageId)

// Check average processing time by provider
customMetrics
| where name == "ProcessingTime"
| summarize avg(value) by tostring(customDimensions.Provider)
```

## Deployment Workflow

### Local Development Deployment

```bash
# 1. Prepare local environment
createdb mail_engine_dev
export AzureServiceBus:ConnectionString="your-connection-string"

# 2. Apply migrations
cd src/MailEngine.Infrastructure
dotnet ef database update --startup-project ../MailEngine.Functions

# 3. Start functions locally
cd ../MailEngine.Functions
func start
```

### Staging Deployment (GitHub Actions)

1. Push to `staging` branch
2. GitHub Actions workflow triggered automatically:
   - Builds solution
   - Runs all tests
   - Generates migration script
   - Auto-applies migration to staging database
   - Publishes to Azure Staging function app

### Production Deployment (GitHub Actions + Manual)

1. Push to `main` branch
2. GitHub Actions workflow triggered:
   - Builds and tests
   - Generates migration script
   - Uploads as artifact
   - **Waits for manual approval** (via PR comment or artifact download)
   - After approval: publishes to production

## Testing Strategy

### Unit Testing Pattern

```csharp
[TestClass]
public class GmailMailProviderTests
{
    private Mock<ITokenRepository> _mockTokenRepository;
    private Mock<IGmailService> _mockGmailService;
    private GmailMailProvider _provider;
    
    [TestInitialize]
    public void Setup()
    {
        _mockTokenRepository = new Mock<ITokenRepository>();
        _mockGmailService = new Mock<IGmailService>();
        _provider = new GmailMailProvider(_mockTokenRepository.Object, _mockGmailService.Object);
    }
    
    [TestMethod]
    public async Task SendEmailAsync_WithValidToken_SendsSuccessfully()
    {
        // Arrange
        var mailEvent = new SendMailEvent
        {
            MessageId = Guid.NewGuid(),
            UserMailAccountId = Guid.NewGuid(),
            To = "test@example.com",
            Subject = "Test",
            Body = "<p>Test</p>"
        };
        
        var token = new OAuthToken
        {
            AccessToken = "valid-token",
            ExpiresAtUtc = DateTime.UtcNow.AddHours(1)
        };
        
        _mockTokenRepository
            .Setup(r => r.GetTokenAsync(mailEvent.UserMailAccountId))
            .ReturnsAsync(token);
        
        // Act
        await _provider.SendEmailAsync(mailEvent);
        
        // Assert
        _mockGmailService.Verify(s => s.SendAsync(It.IsAny<Message>()), Times.Once);
    }
    
    [TestMethod]
    public async Task SendEmailAsync_WithExpiredToken_ThrowsException()
    {
        // Arrange
        var mailEvent = new SendMailEvent { UserMailAccountId = Guid.NewGuid() };
        
        var token = new OAuthToken
        {
            AccessToken = "expired-token",
            ExpiresAtUtc = DateTime.UtcNow.AddHours(-1)  // Expired
        };
        
        _mockTokenRepository
            .Setup(r => r.GetTokenAsync(It.IsAny<Guid>()))
            .ReturnsAsync(token);
        
        // Act & Assert
        await Assert.ThrowsExceptionAsync<ExpiredTokenException>(
            () => _provider.SendEmailAsync(mailEvent)
        );
    }
}
```

### Integration Testing Pattern

Integration tests (when run) use:
- TestContainers for PostgreSQL
- Azure Storage Emulator for Service Bus simulation
- Mock HTTP responses for provider APIs

```csharp
[TestClass]
public class MailEventDispatcherIntegrationTests
{
    private PostgreSqlContainer _dbContainer;
    private MailEngineDbContext _dbContext;
    private MailEventDispatcher _dispatcher;
    
    [ClassInitialize]
    public static void ClassSetup()
    {
        // This is where test containers would be initialized
    }
    
    [TestInitialize]
    public void Setup()
    {
        // Setup test database
    }
    
    [TestMethod]
    public async Task HandleSendMailEvent_WithDatabaseToken_SuccessfullyProcesses()
    {
        // Create test data in database
        var userAccount = new UserMailAccount { ... };
        var token = new OAuthToken { ... };
        
        _dbContext.UserMailAccounts.Add(userAccount);
        _dbContext.OAuthTokens.Add(token);
        await _dbContext.SaveChangesAsync();
        
        // Process event
        var sendMailEvent = new SendMailEvent { ... };
        await _dispatcher.HandleEventAsync(sendMailEvent);
        
        // Verify results
        Assert.IsNotNull(/* result from provider call */);
    }
}
```

## Troubleshooting Guide

### Common Issues & Solutions

**Issue: "Token Expired" errors on every send**
- ✅ Verify separate OAuth service is running and refreshing tokens
- ✅ Check `ExpiresAtUtc` in database is being updated
- ✅ Ensure system clocks are synchronized (use UTC everywhere)

**Issue: Service Bus messages not being processed**
- ✅ Verify function app is running: `func start` locally or check Azure Portal
- ✅ Check subscription exists for topic (mail-send → gmail, outlook subscriptions)
- ✅ Verify connection string in `local.settings.json` is correct
- ✅ Check Application Insights logs for function errors

**Issue: "Too Many Requests" (429) errors**
- ✅ Check ProviderConcurrencyLimiter is set to appropriate limit (currently 10)
- ✅ Verify provider rate limits aren't exceeded (Gmail: 10 emails/sec, Outlook varies)
- ✅ Implement exponential backoff in provider calls
- ✅ Monitor Application Insights for rate limit patterns

**Issue: Database migration fails with "column already exists"**
- ✅ Check for duplicate migrations in Migrations folder
- ✅ Verify `HasDefaultValueSql("gen_random_uuid()")` isn't duplicated
- ✅ Review git history for conflicting migration branches

**Issue: Webhook receiving 401 Unauthorized**
- ✅ Verify webhook signature validation logic (check shared secret/JWT key)
- ✅ Test webhook manually with sample payload
- ✅ Check Application Insights for validation errors
- ✅ Verify clientState matches configuration for Microsoft Graph

**Issue: High memory usage in functions**
- ✅ Check SemaphoreSlim isn't holding too many waiters
- ✅ Profile with Application Insights memory metrics
- ✅ Verify large email bodies aren't being buffered entirely in memory

### Local Development Debugging

```bash
# Clear local state
rm -rf src/MailEngine.Functions/bin src/MailEngine.Functions/obj
rm -rf src/MailEngine.Infrastructure/bin src/MailEngine.Infrastructure/obj
dotnet clean

# Rebuild and run
dotnet build
cd src/MailEngine.Functions
func start

# In another terminal, test with curl
curl -X POST http://localhost:7071/api/test-email \
  -H "Content-Type: application/json" \
  -d '{
    "to": "test@example.com",
    "subject": "Test",
    "body": "<p>Test</p>"
  }'
```

## Security Considerations

### Secret Management Best Practices

- ✅ Never commit `local.settings.json` to git (use .gitignore)
- ✅ Use Azure Key Vault for all production secrets
- ✅ Rotate OAuth refresh tokens regularly (handled by separate service)
- ✅ Use managed identity for function → Key Vault access
- ✅ Enable Azure Key Vault audit logging

### Network Security

- ✅ Use HTTPS only for all API calls (enforced by default)
- ✅ Validate webhook signatures from providers (implemented in WebhookValidator)
- ✅ Restrict Service Bus access to specific IP ranges (if needed)
- ✅ Use Azure Private Endpoints for database access (production)

### Input Validation

```csharp
public async Task SendEmailAsync(SendMailEvent mailEvent, CancellationToken ct)
{
    // Validate email format
    if (string.IsNullOrWhiteSpace(mailEvent.To) || !IsValidEmail(mailEvent.To))
        throw new InvalidEmailFormatException();
    
    // Validate message size
    if (Encoding.UTF8.GetByteCount(mailEvent.Body) > 25 * 1024 * 1024)  // 25 MB
        throw new EmailTooLargeException();
    
    // Validate user exists
    var account = await _db.UserMailAccounts.FindAsync(mailEvent.UserMailAccountId);
    if (account == null)
        throw new UserMailAccountNotFoundException();
    
    // Proceed with API call
}
```

## Performance Optimization Tips

1. **Database Queries**
   - Use `.AsNoTracking()` for read-only queries
   - Index foreign keys (UserMailAccountId in OAuthTokens)
   - Consider materialized views for complex reports

2. **Service Bus**
   - Batch publish multiple events if possible
   - Use message deduplication to prevent duplicate processing
   - Monitor dead-letter queue size

3. **Provider Calls**
   - Implement caching for frequently accessed data
   - Use batch operations where provider supports (e.g., Gmail batch send)
   - Consider request coalescing for multiple concurrent requests

4. **Azure Functions**
   - Use consumption plan for variable workloads
   - Consider premium plan if sustained high load
   - Monitor function execution time in Application Insights
   - Enable Application Insights sampling to reduce costs

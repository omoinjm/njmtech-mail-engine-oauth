# Security Policy

## Overview

Mail Engine handles sensitive email operations and OAuth tokens. This document outlines security best practices, requirements, and incident response procedures.

**Last Updated:** February 2024

---

## 🔐 Security Principles

1. **Zero Trust** - Never assume internal network is secure
2. **Defense in Depth** - Multiple layers of security controls
3. **Least Privilege** - Minimal permissions required
4. **Secure by Default** - Security enabled without configuration
5. **Audit Everything** - Complete logging of all operations

---

## Authentication & Authorization

### Managed Identity (Recommended)

Use Azure Managed Identity for all Azure service authentication:

```csharp
// ✅ Recommended: Managed Identity
var credential = new DefaultAzureCredential();
var client = new ServiceBusClient("your-namespace.servicebus.windows.net", credential);

// ❌ Avoid: Connection strings in code
var client = new ServiceBusClient("Endpoint=sb://...;SharedAccessKey=...");
```

### OAuth Token Management

**Token Storage:**
- Store refresh tokens in Azure SQL or Cosmos DB with encryption at rest
- Never store tokens in logs, cache, or temporary files
- Use HTTPS TLS 1.2+ for all token transmission

**Token Expiration:**
```csharp
if (token.ExpiresAtUtc < DateTime.UtcNow.AddMinutes(5))
{
    // Refresh token using OAuth provider
    token = await RefreshTokenAsync(token.RefreshToken);
    await tokenRepository.SaveTokenAsync(token);
}
```

**Token Revocation:**
- Monitor for revocation notices from email providers
- Implement automatic retry with fresh login on 401 responses
- Log all token refresh failures to Application Insights

### Secrets Management

**Azure Key Vault Integration (Required):**

```csharp
// ✅ Retrieve secrets from Key Vault
var clientSecret = await keyVaultProvider.GetSecretAsync("outlook-client-secret");

// ❌ Never hardcode secrets
const string clientSecret = "abc123..."; // NEVER!
```

**Required Secrets:**
- `gmail-api-key` - Gmail API credentials
- `outlook-tenant-id` - Azure AD tenant ID
- `outlook-client-id` - Azure AD client ID
- `outlook-client-secret` - Azure AD client secret
- `db-connection-string` - Database connection string

**Secret Rotation:**
- Rotate OAuth client secrets every 90 days
- Rotate database passwords every 6 months
- Automate rotation where possible
- Test rotation procedures monthly

---

## Data Protection

### Encryption at Rest

**Database:**
```sql
-- Enable Transparent Data Encryption (TDE)
ALTER DATABASE MailEngine
SET ENCRYPTION ON;
```

**Token Fields:**
```csharp
public class OAuthToken
{
    public Guid Id { get; set; }
    [Encrypted]  // Use value converter
    public string AccessToken { get; set; }
    [Encrypted]
    public string RefreshToken { get; set; }
}
```

### Encryption in Transit

**All API calls must use HTTPS:**
```csharp
// ✅ HTTPS
var client = new HttpClient();
var response = await client.GetAsync("https://api.gmail.com/...");

// ❌ HTTP
var response = await client.GetAsync("http://api.example.com/...");
```

**Service Bus:**
- Use AMQP over TLS (default)
- Enable firewall rules
- Use only managed identity authentication

---

## API Security

### Authentication

All HTTP triggers must require authentication:

```csharp
[Function("GmailPushNotificationFunction")]
public async Task<HttpResponseData> Run(
    [HttpTrigger(AuthorizationLevel.Function, "post", Route = "webhooks/gmail")] 
    HttpRequestData req,
    FunctionContext context)
{
    // AuthorizationLevel.Function requires host key
    // AuthorizationLevel.Admin requires master key
    // Never use AuthorizationLevel.Anonymous for production
}
```

### Webhook Validation

**Google Pub/Sub Webhook (HMAC-SHA256):**

```csharp
public bool ValidateGooglePubSubSignature(
    string payload, 
    string signature,
    string secret)
{
    using (var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret)))
    {
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        var expected = Convert.ToBase64String(hash);
        return signature == expected;
    }
}
```

**Microsoft Graph Webhook (JWT Validation):**

```csharp
public bool ValidateGraphWebhookSignature(string validationToken)
{
    // Microsoft sends validationToken in query string during subscription
    // Return 200 OK with validation token in body
    // This confirms webhook endpoint ownership
    return !string.IsNullOrEmpty(validationToken);
}
```

### Rate Limiting

**Provider-Level Concurrency (Implemented):**
- Max 10 concurrent requests per provider
- Enforced via ProviderConcurrencyLimiter
- Protects external APIs from overload

**User-Level Rate Limiting (Future):**
```csharp
// Limit users to 100 emails/hour
if (userEmailCount > 100 && IsWithinLastHour())
{
    return new TooManyRequestsResult();
}
```

---

## Webhook Security

### Validation Checklist

- [ ] Validate HMAC signature or JWT
- [ ] Verify sender is legitimate (IP whitelist if available)
- [ ] Check webhook URL matches registered endpoint
- [ ] Validate request body schema
- [ ] Implement request timeout (30 seconds)
- [ ] Use unique secret per webhook
- [ ] Log all validation failures

### Implementation

```csharp
[Function("GmailPushNotificationFunction")]
public async Task<HttpResponseData> Run(HttpRequestData req)
{
    try
    {
        // 1. Validate signature
        if (!ValidateSignature(req))
        {
            logger.LogWarning("Invalid webhook signature");
            return req.CreateResponse(HttpStatusCode.Unauthorized);
        }

        // 2. Parse payload
        var payload = await ParsePayload(req);
        
        // 3. Validate schema
        if (!IsValidSchema(payload))
        {
            logger.LogWarning("Invalid payload schema");
            return req.CreateResponse(HttpStatusCode.BadRequest);
        }

        // 4. Process with timeout
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await ProcessWebhookAsync(payload, cts.Token);
        
        return req.CreateResponse(HttpStatusCode.OK);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Webhook processing failed");
        return req.CreateResponse(HttpStatusCode.InternalServerError);
    }
}
```

---

## Logging & Monitoring

### What to Log

✅ **DO Log:**
- Authentication attempts (success/failure)
- Token refresh operations
- Email send/read operations
- Webhook validations
- Configuration changes
- Database access patterns
- Error details with context

❌ **DON'T Log:**
- Tokens or credentials
- Email content/body
- Personal identifiable information (PII)
- Passwords or API keys
- Request/response bodies containing secrets

### Secure Logging

```csharp
// ✅ Safe logging
logger.LogInformation(
    "Email sent successfully for user {UserId} via {Provider}",
    userId,
    providerType);

// ❌ Unsafe logging
logger.LogInformation($"Token: {token}"); // Exposes secrets!
logger.LogInformation($"Email body: {emailBody}"); // Exposes content!
```

### Application Insights Configuration

```json
{
  "ApplicationInsights": {
    "SamplingSettings": {
      "IsEnabled": true,
      "MaxTelemetryItemsPerSecond": 20,
      "EvaluationInterval": "01:00:00",
      "InitialSamplingPercentage": 100.0,
      "MinSamplingPercentage": 0.1,
      "MaxSamplingPercentage": 100.0
    }
  }
}
```

---

## Network Security

### Virtual Network Configuration

```bicep
// Deploy Function App in VNet
resource functionApp 'Microsoft.Web/sites@2021-02-01' = {
  properties: {
    virtualNetworkSubnetId: subnet.id
    publicNetworkAccess: 'Disabled'  // Private endpoints only
  }
}
```

### Service Bus Firewall

```bash
# Enable firewall on Service Bus
az servicebus namespace network-rule add \
  --resource-group myRg \
  --namespace-name myNamespace \
  --ip-address 10.0.0.0/8
```

### Private Endpoints (Recommended)

```bicep
// Private endpoint for Service Bus
resource privateEndpoint 'Microsoft.Network/privateEndpoints@2021-02-01' = {
  name: 'sb-private-endpoint'
  location: location
  properties: {
    privateLinkServiceConnections: [
      {
        name: 'sbConnection'
        properties: {
          privateLinkServiceId: serviceBusId
          groupIds: ['namespace']
        }
      }
    ]
    subnet: {
      id: subnetId
    }
  }
}
```

---

## Database Security

### SQL Server Configuration

```sql
-- Enable advanced security
ALTER SERVER CONFIGURATION SET EXTERNAL SCRIPTS ENABLED = 0;

-- Enable Transparent Data Encryption
ALTER DATABASE MailEngine SET ENCRYPTION ON;

-- Configure firewall
ALTER SERVER FIREWALL RULE [Azure Services] 
ADD ALLOW AZURE SERVICES 1;

-- Enable auditing
CREATE SERVER AUDIT [MailEngineAudit]
TO FILE (FILEPATH = '/var/opt/mssql/audit/');

ALTER SERVER AUDIT [MailEngineAudit] WITH (STATE = ON);
```

### Connection String Security

```csharp
// ✅ Use Managed Identity
services.AddDbContext<MailEngineDbContext>(options =>
    options.UseSqlServer(
        new SqlConnection(connectionString)
        {
            AccessToken = await GetManagedIdentityToken()
        }));

// ❌ Avoid hardcoded passwords
// "Server=...;User Id=sa;Password=P@ssw0rd;"
```

---

## Dependency Security

### NuGet Package Management

```bash
# Scan for vulnerabilities
dotnet list package --vulnerable

# Update security patches
dotnet package update --security

# Use package signing verification
```

**Dependency Policy:**
- Only use signed NuGet packages
- Regular security audits (monthly)
- Keep dependencies updated
- Use dependency pinning for stability

---

## Incident Response

### Security Incident Classification

| Severity | Examples | Response Time |
|----------|----------|---|
| Critical | Token compromise, data breach | 1 hour |
| High | Auth bypass, RCE vulnerability | 4 hours |
| Medium | XSS, CSRF, DoS | 1 day |
| Low | Info disclosure, typos | 1 week |

### Response Procedure

1. **Detect** - Monitor logs, alerts, reports
2. **Isolate** - Disable compromised services if needed
3. **Investigate** - Gather evidence, analyze logs
4. **Remediate** - Fix root cause, patch vulnerabilities
5. **Verify** - Confirm issue is resolved
6. **Document** - Write incident report
7. **Prevent** - Implement controls to prevent recurrence

### Incident Reporting

Email security@yourdomain.com with:
- Incident description
- Severity level
- Affected systems
- Timeline of events
- Evidence/logs
- Recommended actions

---

## Compliance

### Standards & Regulations

- **SOC 2 Type II** - Security, availability, processing integrity
- **ISO 27001** - Information security management
- **GDPR** - Personal data protection (if EU customers)
- **HIPAA** - Health information (if applicable)

### Data Retention

```
Logs:                 90 days
Tokens:               Until revoked or expired
Email metadata:       30 days
User data:            Customer defined (default: 1 year)
Backups:              7 years (compliance)
```

---

## Security Checklist

### Pre-Deployment

- [ ] All secrets in Key Vault (none in code)
- [ ] Managed Identity enabled
- [ ] HTTPS enforced
- [ ] Authentication configured
- [ ] Webhook signatures validated
- [ ] Logging enabled
- [ ] CORS configured properly
- [ ] Database encrypted (TDE)
- [ ] Firewall rules configured
- [ ] Backups tested

### Post-Deployment

- [ ] Monitor Application Insights
- [ ] Review logs daily
- [ ] Test incident response
- [ ] Update firewall rules
- [ ] Rotate secrets
- [ ] Patch dependencies
- [ ] Review access logs
- [ ] Verify backups
- [ ] Security audit
- [ ] Penetration test (quarterly)

---

## Security Resources

- [OWASP Top 10](https://owasp.org/www-project-top-ten/)
- [Azure Security Best Practices](https://docs.microsoft.com/en-us/azure/security/)
- [Microsoft Security Development Lifecycle](https://www.microsoft.com/en-us/securityengineering/sdl/)
- [NIST Cybersecurity Framework](https://www.nist.gov/cyberframework/)

---

## Contact

For security issues, email **security@yourdomain.com** with:
- Issue description
- Affected versions
- Proof of concept (if safe to share)
- Suggested fix

**Do not** post security issues on GitHub or public forums.

---

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 1.0.0 | 2024-02-02 | Initial security policy |

---

**Last Reviewed:** February 2024  
**Next Review:** May 2024

For questions, contact the security team.

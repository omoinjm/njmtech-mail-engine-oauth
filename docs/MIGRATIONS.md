# Database Migrations Guide

This document explains how to create and apply database migrations for the Mail Engine project using Entity Framework Core with PostgreSQL.

## Overview

We use **manual migrations with Option B**:
- **Local Development**: Generate and apply migrations to your local PostgreSQL instance
- **GitHub Pipeline**: Automatically generates migration scripts that can be reviewed before deployment
- **Production**: Manual approval required before applying migrations

---

## Prerequisites

### Local Development

**Required:**
- .NET 8 SDK
- PostgreSQL 12+ installed and running
- Entity Framework Core tools: `dotnet tool install --global dotnet-ef`

**Setup PostgreSQL locally (macOS with Homebrew):**
```bash
brew install postgresql
brew services start postgresql

# Create database
createdb mail_engine_dev

# Verify connection
psql mail_engine_dev
```

**Setup PostgreSQL locally (Windows):**
1. Download from https://www.postgresql.org/download/windows/
2. Install PostgreSQL with default username `postgres`
3. Remember your password
4. Update connection string in `appsettings.Development.json`

**Setup PostgreSQL locally (Linux):**
```bash
sudo apt install postgresql postgresql-contrib
sudo systemctl start postgresql

sudo -u postgres psql
CREATE DATABASE mail_engine_dev;
\q
```

---

## Creating Migrations Locally

### Step 1: Update Your Models

Make changes to models in:
- `src/MailEngine.Core/Models/*.cs`
- `src/MailEngine.Infrastructure/Data/MailEngineDbContext.cs`

Example: Adding a new field
```csharp
public class OAuthToken
{
    public Guid Id { get; set; }
    public string AccessToken { get; set; }
    public string RefreshToken { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? LastRefreshedUtc { get; set; }  // NEW FIELD
}
```

### Step 2: Generate Migration (Bash/Linux/macOS)

```bash
# From project root
chmod +x generate-migration.sh
./generate-migration.sh "AddLastRefreshedField"
```

Or manually:
```bash
cd src/MailEngine.Infrastructure

dotnet ef migrations add "AddLastRefreshedField" \
  --startup-project ../MailEngine.Functions \
  --context MailEngineDbContext
```

### Step 2b: Generate Migration (PowerShell/Windows)

```powershell
# From project root
.\generate-migration.ps1 -MigrationName "AddLastRefreshedField"
```

### Step 3: Review Generated Migration

Check the file created in `src/MailEngine.Infrastructure/Migrations/`:
```csharp
public partial class AddLastRefreshedField : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTime>(
            name: "LastRefreshedUtc",
            table: "OAuthTokens",
            type: "timestamp without time zone",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "LastRefreshedUtc",
            table: "OAuthTokens");
    }
}
```

### Step 4: Generate SQL Script

The `generate-migration.sh` script automatically creates an SQL file:
```bash
# Review the SQL
cat src/MailEngine.Infrastructure/migration_AddLastRefreshedField.sql
```

Example output:
```sql
CREATE TABLE IF NOT EXISTS __EFMigrationsHistory (
    MigrationId character varying(150) NOT NULL,
    ProductVersion character varying(32) NOT NULL,
    PRIMARY KEY (MigrationId)
);

-- AddLastRefreshedField
ALTER TABLE "OAuthTokens" ADD COLUMN "LastRefreshedUtc" timestamp without time zone NULL;
```

### Step 5: Apply Locally

```bash
cd src/MailEngine.Infrastructure

dotnet ef database update \
  --startup-project ../MailEngine.Functions \
  --context MailEngineDbContext
```

Or via script:
```bash
# From project root
cd src/MailEngine.Infrastructure
dotnet ef database update --startup-project ../MailEngine.Functions
```

Verify:
```bash
psql mail_engine_dev

\dt  -- List tables
\d "OAuthTokens"  -- Show columns
```

---

## GitHub Pipeline Migrations

### How It Works

1. **You push code** with model changes
2. **GitHub Actions** runs `migrations.yml`
3. **EF Core generates** migration files and SQL scripts
4. **Artifacts uploaded** for download and review
5. **Staging**: Applied automatically
6. **Production**: Requires manual approval

### Setting Up GitHub Secrets

Add these to your repository Settings → Secrets and variables → Actions:

**For Staging:**
```
STAGING_DATABASE_CONNECTION_STRING
  Value: Host=staging.example.com;Port=5432;Database=mail_engine_staging;Username=postgres;Password=xxx
```

**For Production:**
```
PROD_DATABASE_CONNECTION_STRING
  Value: Host=prod.example.com;Port=5432;Database=mail_engine;Username=postgres;Password=xxx
```

### Downloading Migration Scripts

1. Go to **GitHub → Actions → Database Migrations** workflow
2. Find your run
3. Download artifact: `migration-script-XXXX`
4. Extract and review the SQL files

---

## Manual Migration (Staging)

### Step 1: Get the SQL Script

**Option A: From GitHub Actions**
1. Find the migration artifact in GitHub
2. Download and extract

**Option B: Generate locally**
```bash
cd src/MailEngine.Infrastructure
dotnet ef migrations script --output migration.sql --idempotent
```

### Step 2: Review the Script

```bash
# Review carefully
cat migration.sql

# Example output for new field:
# ALTER TABLE "OAuthTokens" ADD COLUMN "LastRefreshedUtc" timestamp without time zone NULL;
```

### Step 3: Connect to Staging Database

```bash
# Via psql
psql -h staging.example.com \
     -U postgres \
     -d mail_engine_staging \
     -f migration.sql

# Or via connection string
psql "postgresql://postgres:password@staging.example.com/mail_engine_staging" < migration.sql
```

### Step 4: Verify Migration

```bash
psql -h staging.example.com -U postgres -d mail_engine_staging

# Check tables
\dt

# Check specific table
\d "OAuthTokens"

# Check migration history
SELECT * FROM "__EFMigrationsHistory";
```

---

## Manual Migration (Production)

### ⚠️ Important: Backup First!

```bash
# Backup production database
pg_dump -h prod.example.com \
        -U postgres \
        -d mail_engine \
        --file=backup_$(date +%Y%m%d_%H%M%S).sql
```

### Step 1: Get the SQL Script

Same as staging—download from GitHub or generate locally.

### Step 2: Test in Staging First

Always test migrations in staging environment first:
1. Apply migration to staging
2. Run full test suite
3. Monitor for 24 hours
4. Verify no performance issues

### Step 3: Create Maintenance Window

```bash
# Schedule for low-traffic time
# Example: Tuesday 2 AM UTC
```

### Step 4: Apply Migration

```bash
# Connect to production
psql "postgresql://postgres:password@prod.example.com/mail_engine" < migration.sql

# Verify
psql "postgresql://postgres:password@prod.example.com/mail_engine" -c \
  "SELECT * FROM \"__EFMigrationsHistory\" ORDER BY \"MigrationId\" DESC LIMIT 5;"
```

### Step 5: Monitor

1. Check application logs
2. Monitor database performance
3. Verify data integrity

---

## Rollback Procedures

### Rollback Locally

```bash
cd src/MailEngine.Infrastructure

# See migration history
dotnet ef migrations list

# Rollback to previous migration
dotnet ef database update "PreviousMigrationName" \
  --startup-project ../MailEngine.Functions
```

### Rollback Staging

**Option 1: Using EF Core Down Migration**
```sql
-- Run the Down() section of the migration manually
ALTER TABLE "OAuthTokens" DROP COLUMN "LastRefreshedUtc";

-- Remove from history
DELETE FROM "__EFMigrationsHistory" 
WHERE "MigrationId" = '20240202143000_AddLastRefreshedField';
```

**Option 2: Restore from Backup**
```bash
# Restore from backup file
psql -h staging.example.com \
     -U postgres \
     -d mail_engine_staging \
     < backup_20240202_120000.sql
```

### Rollback Production

**⚠️ Only for emergencies!**

1. **First choice**: Restore from backup
   ```bash
   pg_restore -h prod.example.com \
              -U postgres \
              -d mail_engine \
              backup_production.sql
   ```

2. **If backup not possible**: Manual SQL rollback (requires DBA approval)

---

## Idempotent Migrations

Scripts generated with `--idempotent` flag can be run multiple times safely:

```sql
-- Safe to run multiple times
CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (...);

ALTER TABLE "OAuthTokens" ADD COLUMN IF NOT EXISTS "LastRefreshedUtc" ...;
```

---

## Troubleshooting

### Error: "No database provider was configured"

**Solution**: Ensure `DatabaseProvider` in `appsettings.Development.json` is set to `PostgreSQL`

```json
{
  "DatabaseProvider": "PostgreSQL",
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;..."
  }
}
```

### Error: "Could not connect to database"

**Solution**: Verify PostgreSQL is running and connection string is correct

```bash
# Test connection
psql "postgresql://postgres:password@localhost/mail_engine_dev"

# Or check connection string
grep "DefaultConnection" appsettings.Development.json
```

### Error: "Migrations folder not found"

**Solution**: Ensure you're in `src/MailEngine.Infrastructure` directory

```bash
cd src/MailEngine.Infrastructure
ls Migrations/  # Should list migration files
```

### Migration hangs or takes too long

**Solution**: Large data migrations may need timeout increase

```bash
# Set timeout (30 minutes)
export EF_TIMEOUT=1800

dotnet ef database update --startup-project ../MailEngine.Functions
```

---

## Best Practices

✅ **DO:**
- Write descriptive migration names: `AddFailedMessagesTable`, not `Update1`
- Review generated migrations before applying
- Test migrations in staging first
- Keep migrations small and focused
- Backup before production migrations
- Document breaking changes

❌ **DON'T:**
- Manually edit generated migrations (regenerate instead)
- Apply untested migrations to production
- Skip the staging environment
- Delete migration files
- Run migrations during peak traffic

---

## Migration Naming Convention

| Change | Migration Name |
|--------|---|
| Initial schema | `InitialCreate` |
| Add table | `Add[TableName]Table` |
| Drop table | `Drop[TableName]Table` |
| Add column | `Add[ColumnName]To[TableName]` |
| Drop column | `Drop[ColumnName]From[TableName]` |
| Change column type | `Change[ColumnName]Type[In][TableName]` |
| Add index | `Add[IndexName]Index` |

---

## Reference

**Migration Files:**
- `src/MailEngine.Infrastructure/Migrations/*.cs` - Generated migration code
- `src/MailEngine.Infrastructure/migration_*.sql` - Generated SQL scripts

**Configuration:**
- `appsettings.json` - Database provider setting
- `appsettings.Development.json` - Local connection string
- `.github/workflows/migrations.yml` - CI/CD pipeline

**Commands:**
```bash
# List migrations
dotnet ef migrations list

# Create migration
dotnet ef migrations add [Name]

# Generate SQL
dotnet ef migrations script

# Update database
dotnet ef database update

# Get help
dotnet ef migrations --help
```

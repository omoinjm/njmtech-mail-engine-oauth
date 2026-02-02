# GitHub Actions: Automated Migration Pipeline

This document explains how to set up the automated database migration pipeline using GitHub Actions.

## Overview

```
You push code
    ↓
GitHub detects schema changes
    ↓
Runs migrations.yml workflow
    ↓
├─ Generates migration files
├─ Creates SQL scripts
├─ Uploads artifacts
├─ For Staging: Apply automatically ✅
└─ For Production: Requires manual approval ⚠️
```

---

## Setup: GitHub Secrets

### Step 1: Get Your Database Connection Strings

**For Staging PostgreSQL:**
```
Host=staging.example.com
Port=5432
Database=mail_engine_staging
Username=postgres
Password=your_password
```

**For Production PostgreSQL:**
```
Host=prod.example.com
Port=5432
Database=mail_engine
Username=postgres
Password=your_password
```

### Step 2: Add Secrets to GitHub

1. Go to your repository → **Settings** → **Secrets and variables** → **Actions**
2. Click **New repository secret**
3. Add each secret:

**Secret 1: Staging Database**
```
Name: STAGING_DATABASE_CONNECTION_STRING
Value: Host=staging.example.com;Port=5432;Database=mail_engine_staging;Username=postgres;Password=xxx
```

**Secret 2: Production Database**
```
Name: PROD_DATABASE_CONNECTION_STRING
Value: Host=prod.example.com;Port=5432;Database=mail_engine;Username=postgres;Password=xxx
```

**Secret 3: Staging Migration Script (optional, if using automated script)**
```
Name: STAGING_MIGRATION_SCRIPT
Value: [Your migration execution script content]
```

---

## How It Works

### Trigger: What Activates the Pipeline

The workflow runs when:

1. **Push to `staging` branch** with changes to:
   - `src/MailEngine.Infrastructure/Data/**` (database context)
   - `src/MailEngine.Infrastructure/Migrations/**` (migration files)
   - `src/MailEngine.Core/Models/**` (data models)
   - `.github/workflows/migrations.yml` (this file)

2. **Push to `main` branch** (production) with same file changes

3. **Manual trigger** via GitHub Actions UI

### Example: Triggering the Workflow

```bash
# 1. Make a model change
vim src/MailEngine.Core/Models/OAuthToken.cs

# 2. Add migration locally (generates files)
./generate-migration.sh "AddNewField"

# 3. Commit and push
git add src/MailEngine.Infrastructure/Migrations/*
git add src/MailEngine.Core/Models/OAuthToken.cs
git commit -m "Add new field to OAuthToken"
git push origin staging

# 4. GitHub Actions automatically runs!
```

---

## Workflow Stages Explained

### Stage 1: Code Checkout & Setup

```yaml
- name: Checkout code
  uses: actions/checkout@v4

- name: Setup .NET
  uses: actions/setup-dotnet@v4
  with:
    dotnet-version: '8.0.x'
```

**What it does**: Downloads your code and sets up .NET 8 runtime

---

### Stage 2: Build Project

```yaml
- name: Restore dependencies
  run: dotnet restore

- name: Build project
  run: dotnet build --configuration Release --no-restore
```

**What it does**: Ensures code compiles without errors

**If fails**: The workflow stops and you get a notification

---

### Stage 3: Generate Migration Script

```yaml
- name: Generate migration script
  run: |
    cd src/MailEngine.Infrastructure
    dotnet ef migrations script \
      --startup-project ../MailEngine.Functions \
      --context MailEngineDbContext \
      --output migration.sql \
      --idempotent
```

**What it does**: 
- Compares current schema to database
- Generates SQL script to bring database up-to-date
- Uses `--idempotent` (safe to run multiple times)

**Output**: `migration.sql` file ready for deployment

---

### Stage 4: Upload Artifact

```yaml
- name: Upload migration script
  uses: actions/upload-artifact@v4
  with:
    name: migration-script-${{ github.run_number }}
    path: src/MailEngine.Infrastructure/migration.sql
    retention-days: 30
```

**What it does**: Saves the migration script for download

**How to access**:
1. Go to GitHub Actions → Your workflow run
2. Scroll to **Artifacts** section
3. Download `migration-script-XXXX`

---

### Stage 5: Apply to Staging (Automatic)

```yaml
- name: Apply migration (Staging)
  if: github.ref == 'refs/heads/staging'
  run: |
    cd src/MailEngine.Infrastructure
    # Execute migration script
```

**What it does**: Automatically applies migration to staging database

**When**: Only when pushing to `staging` branch

**Result**: Staging database is updated immediately

---

### Stage 6: Production Approval (Manual)

```yaml
- name: Apply migration (Production)
  if: github.ref == 'refs/heads/main'
  run: |
    echo "⚠️  PRODUCTION MIGRATION REQUIRES MANUAL APPROVAL"
```

**What it does**: Prepares migration but requires manual approval

**When**: Only when pushing to `main` branch

**Why manual?**: Extra safety to prevent accidental production changes

---

## Usage: Day-to-Day Operations

### Scenario 1: Add a New Column

```bash
# 1. Update model
vim src/MailEngine.Core/Models/OAuthToken.cs
# Add: public DateTime? LastRefreshedUtc { get; set; }

# 2. Generate migration
./generate-migration.sh "AddLastRefreshedField"

# 3. Commit
git add .
git commit -m "Add LastRefreshedUtc to OAuthToken"

# 4. Push to staging
git push origin staging

# 5. GitHub Actions:
#    ✅ Builds code
#    ✅ Generates migration
#    ✅ Uploads artifact
#    ✅ Applies to staging database
#    ✅ Sends notification

# 6. When ready for production
git push origin main

# 7. GitHub Actions:
#    ✅ Builds code
#    ✅ Generates migration
#    ✅ Uploads artifact
#    ⚠️  REQUIRES MANUAL APPROVAL
#    (Review artifact, run SQL manually)
```

### Scenario 2: Download & Review Migration

```bash
# 1. Go to GitHub Actions
# 2. Find your workflow run
# 3. Scroll down to "Artifacts"
# 4. Download "migration-script-XXXX"
# 5. Extract and review migration.sql

# Content looks like:
# ALTER TABLE "OAuthTokens" 
# ADD COLUMN "LastRefreshedUtc" timestamp without time zone NULL;

# 6. Apply to production manually:
psql "postgresql://postgres:password@prod.example.com/mail_engine" < migration.sql
```

---

## Monitoring the Pipeline

### View Workflow Runs

1. Go to **Actions** tab in GitHub
2. Click **Database Migrations** workflow
3. See all recent runs

### Run Status Indicators

| Status | Meaning |
|--------|---------|
| ✅ Success | Migration generated and applied |
| ❌ Failed | Build error or migration generation failed |
| ⏳ In Progress | Currently running |
| ⚠️ Manual Approval | Waiting for approval (production only) |

### View Logs

1. Click on a workflow run
2. Click **Generate migration script** step
3. Expand to see command output
4. Troubleshoot if needed

---

## Advanced: Manual Trigger

You can trigger the workflow manually even without code changes:

1. Go to **Actions** → **Database Migrations**
2. Click **Run workflow**
3. Select branch (staging or main)
4. Click **Run workflow**

**Use cases**:
- Re-run a failed migration
- Generate migration without code changes
- Test the pipeline

---

## Troubleshooting Pipeline Issues

### Error: "Could not find NuGet package"

**Cause**: Missing or outdated NuGet package reference

**Solution**:
1. Check `src/MailEngine.Infrastructure/MailEngine.Infrastructure.csproj`
2. Run `dotnet restore` locally
3. Commit and push again

### Error: "Database connection failed"

**Cause**: Incorrect connection string in secrets

**Solution**:
1. Test connection string locally:
   ```bash
   psql "postgresql://postgres:password@staging.example.com/mail_engine_staging"
   ```
2. Update secret if needed
3. Re-run workflow

### Error: "Migration already exists"

**Cause**: Migration generated locally and also during CI/CD

**Solution**:
```bash
# Remove duplicate migration files
rm src/MailEngine.Infrastructure/Migrations/*LastMigration.cs

# Regenerate properly
./generate-migration.sh "MigrationName"
```

### Error: "EF Core tools not found"

**Cause**: Tool installation failed

**Solution**: Workflow should auto-install, but if not:
```bash
# Run locally and commit migration files
dotnet tool install --global dotnet-ef
./generate-migration.sh
git push
```

---

## Best Practices

✅ **DO:**
- Test migrations in staging first
- Review artifact before production
- Use descriptive migration names
- Keep migration scripts in version control
- Monitor workflow runs regularly
- Set up backup alerts

❌ **DON'T:**
- Push directly to main for small schema changes (use staging first)
- Ignore workflow failures
- Manually modify generated SQL files
- Apply unreviewed migrations to production
- Run migrations during peak hours

---

## Integration with Git Branching

### Recommended Workflow

```
main (production)
  ↑
  └─ (Pull Request)
     ↓
staging (staging environment)
  ↑
  └─ (Feature Branch)
     ↓
feature/my-feature
```

**Process**:
1. Create feature branch from `staging`
2. Make schema changes
3. Generate migration locally
4. Push to feature branch
5. Create PR to `staging`
6. Merge to `staging`
7. **Workflow**: Generates + applies to staging
8. Test in staging environment
9. Create PR from `staging` to `main`
10. Merge to `main`
11. **Workflow**: Generates migration, awaits manual approval
12. Review artifact and apply manually

---

## Environment Configuration

### Staging

- **Branch**: `staging`
- **Database**: PostgreSQL on staging server
- **Migrations**: Applied automatically
- **Approval**: Not required

### Production

- **Branch**: `main`
- **Database**: PostgreSQL on production server
- **Migrations**: Generated, awaiting manual approval
- **Approval**: Required before applying

---

## Monitoring & Alerts

### Set Up GitHub Notifications

1. Go to **Settings** → **Notifications**
2. Enable: "Notify me about"
   - Failed workflow runs
   - Cancelled workflow runs

### Optional: Slack Integration

Add to `.github/workflows/migrations.yml`:

```yaml
- name: Notify Slack on failure
  if: failure()
  uses: slackapi/slack-github-action@v1
  with:
    webhook-url: ${{ secrets.SLACK_WEBHOOK }}
    payload: |
      {
        "text": "❌ Migration pipeline failed",
        "blocks": [{"type": "section", "text": {"type": "mrkdwn", "text": "Check the workflow run: ${{ github.server_url }}/${{ github.repository }}/actions/runs/${{ github.run_id }}"}}]
      }
```

---

## Reference

**Workflow file**: `.github/workflows/migrations.yml`

**Secrets needed**:
- `STAGING_DATABASE_CONNECTION_STRING`
- `PROD_DATABASE_CONNECTION_STRING`

**Triggered by**: Changes to `src/MailEngine.Infrastructure/Data/`, `src/MailEngine.Core/Models/`, or `.github/workflows/migrations.yml`

**Output**: `migration-script-XXXX` artifact with SQL files

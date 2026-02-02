# First-Time Database Setup

This is your step-by-step guide for setting up the database the **first time**.

## 🚀 Quick Start (5 minutes)

### Step 1: Set Up PostgreSQL

**macOS (Homebrew):**
```bash
brew install postgresql
brew services start postgresql
createdb mail_engine_dev
```

**Windows:**
1. Download from https://www.postgresql.org/download/windows/
2. Install with password (remember it!)
3. Test: Open Command Prompt and run `psql -U postgres`

**Linux (Ubuntu/Debian):**
```bash
sudo apt update
sudo apt install postgresql
sudo systemctl start postgresql
sudo -u postgres createdb mail_engine_dev
```

### Step 2: Configure Connection String

Edit `src/MailEngine.Functions/appsettings.Development.json`:

**If you used default PostgreSQL setup:**
```json
{
  "DatabaseProvider": "PostgreSQL",
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=mail_engine_dev;Username=postgres;Password=postgres"
  }
}
```

**Test the connection:**
```bash
psql "Host=localhost;Port=5432;Database=mail_engine_dev;Username=postgres;Password=postgres"
```

### Step 3: Generate Initial Migration

```bash
# macOS/Linux
chmod +x generate-migration.sh
./generate-migration.sh

# Windows (PowerShell)
.\generate-migration.ps1
```

You'll see:
```
🔧 Mail Engine Database Migration
==================================
Migration Name: InitialCreate
Environment: Development

📦 Installing EF Core tools...
🔄 Creating migration...
📝 Generating SQL script...
✅ Migration created successfully!
```

### Step 4: Apply Migration Locally

```bash
cd src/MailEngine.Infrastructure
dotnet ef database update --startup-project ../MailEngine.Functions
```

You'll see:
```
Build started...
Build succeeded.
Applying migration '20240202...InitialCreate'.
Done.
```

### Step 5: Verify Database

```bash
psql mail_engine_dev

# List tables
\dt

# You should see:
#                  List of relations
#  Schema |       Name       | Type  |  Owner
# --------+------------------+-------+----------
#  public | OAuthTokens      | table | postgres
#  public | UserMailAccounts | table | postgres
#  public | FailedMessages   | table | postgres

# Quit
\q
```

✅ **Done!** Your database is ready to use.

---

## 📋 What Gets Created

When you apply the initial migration, three tables are created:

### UserMailAccounts
Stores which email accounts are connected to the system.

```sql
CREATE TABLE "UserMailAccounts" (
    "Id" uuid PRIMARY KEY,
    "EmailAddress" text,
    "ProviderType" integer
);
```

### OAuthTokens
Stores OAuth tokens for each user account.

```sql
CREATE TABLE "OAuthTokens" (
    "Id" uuid PRIMARY KEY,
    "UserMailAccountId" uuid,
    "AccessToken" text,
    "RefreshToken" text,
    "ExpiresAtUtc" timestamp
);
```

### FailedMessages
Tracks messages that fail to send/read.

```sql
CREATE TABLE "FailedMessages" (
    "Id" uuid PRIMARY KEY,
    "MessageId" uuid,
    "Topic" text,
    "Subscription" text,
    "ErrorMessage" text,
    "ErrorStackTrace" text,
    "FailedAtUtc" timestamp,
    "Status" text DEFAULT 'in-dlq',
    "RetryCount" integer DEFAULT 0,
    "ResolvedAtUtc" timestamp,
    "MessageContent" text
);

CREATE INDEX "IX_Status" ON "FailedMessages" ("Status");
CREATE INDEX "IX_Topic" ON "FailedMessages" ("Topic");
```

---

## 🐛 Troubleshooting

### "connection refused" or "could not connect"

**Problem**: PostgreSQL isn't running or wrong credentials

**Solution**:
```bash
# Check if PostgreSQL is running
psql --version  # Shows version, not running status

# macOS
brew services list | grep postgresql  # Should show "started"

# Linux
sudo systemctl status postgresql  # Should show "active (running)"

# Windows: Check Services app (Ctrl+R → services.msc)
```

### "database does not exist"

**Problem**: Didn't create the database

**Solution**:
```bash
# Create database
createdb mail_engine_dev

# Verify
psql -l | grep mail_engine_dev
```

### "role 'postgres' does not exist"

**Problem**: Different username

**Solution**:
```bash
# List users
psql -U postgres -c "\du"

# Use correct username
"DefaultConnection": "Host=localhost;Port=5432;Database=mail_engine_dev;Username=your_username;Password=your_password"
```

### "permission denied"

**Problem**: PostgreSQL permission issues

**Solution** (macOS with Homebrew):
```bash
# Reset password
psql -U postgres
ALTER USER postgres WITH PASSWORD 'postgres';
\q

# Update connection string with new password
```

### Migration "hangs" or takes too long

**Problem**: Large database or slow connection

**Solution**:
```bash
# Check connection
psql mail_engine_dev

# If stuck, press Ctrl+C and try again

# If still failing, check PostgreSQL logs
sudo tail -f /var/log/postgresql/postgresql.log  # Linux
```

---

## 🔄 Next Time: Making Changes

When you modify models (add fields, create tables, etc.):

```bash
# 1. Update your model files
# 2. Generate migration
./generate-migration.sh "YourMigrationName"

# 3. Review the generated files
cat src/MailEngine.Infrastructure/Migrations/*_YourMigrationName.cs
cat src/MailEngine.Infrastructure/migration_YourMigrationName.sql

# 4. Apply locally
cd src/MailEngine.Infrastructure
dotnet ef database update --startup-project ../MailEngine.Functions
```

---

## 📚 More Information

See `MIGRATIONS.md` for:
- Detailed migration guide
- GitHub Actions setup
- Production deployment
- Rollback procedures
- Best practices

---

## ✅ Checklist

When you've completed the setup:

- [ ] PostgreSQL installed and running
- [ ] `mail_engine_dev` database created
- [ ] Connection string updated in `appsettings.Development.json`
- [ ] Initial migration generated
- [ ] Migration applied locally
- [ ] Tables verified in `psql`

Once all checked, your local development environment is ready! 🎉

#!/bin/bash

# Database Migration Application Script for Mail Engine
# Applies pending migrations to the database
# Usage: ./apply-migration.sh [environment] [connection-string]
# Example: ./apply-migration.sh Development

set -e

ENVIRONMENT=${1:-"Development"}
CONNECTION_STRING=${2:-""}

echo "🚀 Mail Engine - Apply Database Migration"
echo "=========================================="
echo "Environment: $ENVIRONMENT"
echo ""

# Validate environment
if [[ ! "$ENVIRONMENT" =~ ^(Development|Staging|Production)$ ]]; then
    echo "❌ Error: Invalid environment. Must be Development, Staging, or Production"
    exit 1
fi

# Navigate to Infrastructure project
cd src/MailEngine.Infrastructure

echo "📦 Installing EF Core tools..."
dotnet tool install --global dotnet-ef --ignore-failed-sources 2>/dev/null || true

echo ""
echo "📋 Checking for pending migrations..."
MIGRATIONS=$(dotnet ef migrations list --startup-project ../MailEngine.Functions --context MailEngineDbContext 2>&1 | grep -c "Pending" || true)

if [ "$MIGRATIONS" -eq 0 ]; then
    echo "✅ No pending migrations found. Database is up to date!"
    exit 0
fi

echo ""
echo "📝 Pending migrations detected. Preparing to apply..."
echo ""

# For different environments
if [ "$ENVIRONMENT" = "Development" ]; then
    echo "💻 Development Environment"
    echo "Applying migrations to local database..."
    echo ""
    
    dotnet ef database update \
        --startup-project ../MailEngine.Functions \
        --context MailEngineDbContext
    
    echo ""
    echo "✅ Migrations applied successfully!"
    echo ""
    echo "📊 Verify tables were created:"
    echo "  psql mail_engine_dev -c \"\\dt\""
    echo ""
    
elif [ "$ENVIRONMENT" = "Staging" ]; then
    echo "🟡 Staging Environment"
    echo ""
    echo "⚠️  Manual verification required for staging"
    echo ""
    echo "Steps:"
    echo "  1. Review generated SQL script: migration_*.sql"
    echo "  2. Test against staging database backup first"
    echo "  3. Then apply with connection string:"
    echo "     dotnet ef database update --connection=\"<connection-string>\" \\"
    echo "       --startup-project ../MailEngine.Functions"
    echo ""
    
elif [ "$ENVIRONMENT" = "Production" ]; then
    echo "🔴 Production Environment"
    echo ""
    echo "⚠️  CRITICAL: Production migration requires approval"
    echo ""
    echo "Steps:"
    echo "  1. Download migration artifact from GitHub Actions"
    echo "  2. Have DBA review the SQL script"
    echo "  3. Create backup of production database"
    echo "  4. Test rollback procedure"
    echo "  5. Apply migration during maintenance window:"
    echo "     dotnet ef database update --connection=\"<connection-string>\" \\"
    echo "       --startup-project ../MailEngine.Functions"
    echo ""
    read -p "Continue with production migration? (yes/no): " CONFIRM
    if [ "$CONFIRM" != "yes" ]; then
        echo "❌ Migration cancelled"
        exit 1
    fi
    
    dotnet ef database update \
        --startup-project ../MailEngine.Functions \
        --context MailEngineDbContext
    
    echo ""
    echo "✅ Production migrations applied successfully!"
    echo ""
fi

echo "📚 For more information, see docs/MIGRATIONS.md"

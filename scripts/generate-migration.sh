#!/bin/bash

# Database Migration Script for Mail Engine
# Usage: ./generate-migration.sh [migration-name]
# Example: ./generate-migration.sh AddFailedMessagesTable

set -e

MIGRATION_NAME=${1:-"InitialCreate"}
ENVIRONMENT=${2:-"Development"}

echo "🔧 Mail Engine Database Migration"
echo "=================================="
echo "Migration Name: $MIGRATION_NAME"
echo "Environment: $ENVIRONMENT"
echo ""

# Navigate to Infrastructure project
cd src/MailEngine.Infrastructure

echo "📦 Installing EF Core tools..."
dotnet tool install --global dotnet-ef --ignore-failed-sources 2>/dev/null || true

echo "🔄 Creating migration..."
# Suppress non-critical DI errors that don't affect migration creation
dotnet ef migrations add "$MIGRATION_NAME" \
  --startup-project ../MailEngine.Functions \
  --context MailEngineDbContext \
  --no-build # 2>&1 | grep -v "Error while accessing the Microsoft.Extensions.Hosting\|Some services are not able to be constructed\|Unable to resolve service for type" || true

echo "✅ Migration created successfully! (Note: Non-critical DI warnings suppressed)"

echo ""
echo "📝 Generating SQL script..."
# Suppress non-critical DI errors that don't affect script generation
dotnet ef migrations script \
  --startup-project ../MailEngine.Functions \
  --context MailEngineDbContext \
  --output migration_${MIGRATION_NAME}.sql \
  --idempotent \
  --no-build # 2>&1 | grep -v "Error while accessing the Microsoft.Extensions.Hosting\|Some services are not able to be constructed\|Unable to resolve service for type" || true

echo ""
echo "✅ Migration and SQL script generated successfully! (Note: Non-critical DI warnings suppressed)"
echo ""
echo "📄 Files created:"
echo "  - Migrations/$(date +%Y%m%d%H%M%S)_${MIGRATION_NAME}.cs"
echo "  - migration_${MIGRATION_NAME}.sql"
echo ""
echo "📋 Next steps:"
echo "  1. Review the generated migration in Migrations/ folder"
echo "  2. Review the SQL script: migration_${MIGRATION_NAME}.sql"
echo "  3. For staging: Run the SQL script against your staging database"
echo "  4. For production: Follow your production deployment process"
echo ""
echo "💾 To apply locally:"
echo "  dotnet ef database update --startup-project ../MailEngine.Functions"
echo ""

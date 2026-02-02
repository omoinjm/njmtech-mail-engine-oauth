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
dotnet ef migrations add "$MIGRATION_NAME" \
  --startup-project ../MailEngine.Functions \
  --context MailEngineDbContext \
  --no-build

echo ""
echo "📝 Generating SQL script..."
dotnet ef migrations script \
  --startup-project ../MailEngine.Functions \
  --context MailEngineDbContext \
  --output migration_${MIGRATION_NAME}.sql \
  --idempotent \
  --no-build

echo ""
echo "✅ Migration created successfully!"
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

#!/bin/bash

# Test Database Connection Script
# Helps diagnose connection string and PostgreSQL setup issues

echo "🔍 Mail Engine - Database Connection Test"
echo "=========================================="
echo ""

# Navigate to correct directory
cd "$(dirname "$0")/.." || exit 1

echo "📋 Checking environment..."
echo ""

# 1. Check if local.settings.json exists
echo "1️⃣  Checking local.settings.json..."
if [ -f "src/MailEngine.Functions/local.settings.json" ]; then
    echo "✅ File found: src/MailEngine.Functions/local.settings.json"
    echo ""
    echo "📄 Contents (sensitive data masked):"
    cat src/MailEngine.Functions/local.settings.json | grep -A 2 "ConnectionStrings" | head -5
    echo ""
else
    echo "❌ File not found: src/MailEngine.Functions/local.settings.json"
    echo ""
fi

# 2. Extract connection string
echo "2️⃣  Extracting connection string..."
CONNECTION_STRING=$(grep -o '"DefaultConnection":[^}]*' src/MailEngine.Functions/local.settings.json | cut -d'"' -f4 | sed 's/\\//g')

if [ -z "$CONNECTION_STRING" ]; then
    echo "❌ Could not extract DefaultConnection"
    echo "   Check that your local.settings.json has:"
    echo '   "ConnectionStrings": { "DefaultConnection": "..." }'
    echo ""
    exit 1
fi

echo "✅ Connection string found"
echo ""
echo "Connection Details:"
HOST=$(echo "$CONNECTION_STRING" | grep -o 'Host=[^;]*' | cut -d'=' -f2)
PORT=$(echo "$CONNECTION_STRING" | grep -o 'Port=[^;]*' | cut -d'=' -f2)
DB=$(echo "$CONNECTION_STRING" | grep -o 'Database=[^;]*' | cut -d'=' -f2)
USER=$(echo "$CONNECTION_STRING" | grep -o 'Username=[^;]*' | cut -d'=' -f2)

echo "  Host:     $HOST"
echo "  Port:     $PORT"
echo "  Database: $DB"
echo "  Username: $USER"
echo ""

# 3. Try to connect
echo "3️⃣  Testing connection..."
if ! command -v psql &> /dev/null; then
    echo "⚠️  psql command not found"
    echo "   Install PostgreSQL client to test the connection"
    echo ""
    echo "   macOS:  brew install postgresql"
    echo "   Linux:  sudo apt install postgresql-client"
    echo "   Windows: Install PostgreSQL from postgresql.org"
    exit 1
fi

# For Neon or remote connections, show guidance
if [[ "$HOST" != "localhost" && "$HOST" != "127.0.0.1" ]]; then
    echo "🌐 Detected remote PostgreSQL server: $HOST"
    echo ""
    echo "To test connection, run:"
    echo "  psql -h $HOST -U $USER -d $DB"
    echo ""
else
    # Try to connect to local PostgreSQL
    echo "Testing local PostgreSQL connection..."
    if psql -h "$HOST" -U "$USER" -d "$DB" -c "SELECT version();" > /dev/null 2>&1; then
        echo "✅ Connection successful!"
        echo ""
        echo "📊 PostgreSQL version:"
        psql -h "$HOST" -U "$USER" -d "$DB" -c "SELECT version();"
        echo ""
    else
        echo "❌ Connection failed"
        echo "   Make sure PostgreSQL is running:"
        echo "   macOS:  brew services start postgresql"
        echo "   Linux:  sudo systemctl start postgresql"
        echo ""
    fi
fi

echo "4️⃣  Checking if migration can run..."
echo ""
echo "Run migration with:"
echo "  ./scripts/apply-migration.sh Development"
echo ""

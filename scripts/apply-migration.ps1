# Database Migration Application Script for Mail Engine
# Applies pending migrations to the database
# Usage: .\apply-migration.ps1 -Environment Development -ConnectionString "<connection>"
# Example: .\apply-migration.ps1 -Environment Development

param(
    [Parameter(Mandatory = $false)]
    [ValidateSet('Development', 'Staging', 'Production')]
    [string]$Environment = 'Development',
    
    [Parameter(Mandatory = $false)]
    [string]$ConnectionString = ""
)

Write-Host "🚀 Mail Engine - Apply Database Migration" -ForegroundColor Green
Write-Host "==========================================" -ForegroundColor Green
Write-Host "Environment: $Environment" -ForegroundColor Yellow
Write-Host ""

# Navigate to Infrastructure project
Push-Location src/MailEngine.Infrastructure

try {
    Write-Host "📦 Installing EF Core tools..." -ForegroundColor Cyan
    dotnet tool install --global dotnet-ef --ignore-failed-sources 2>$null | Out-Null
    
    Write-Host ""
    Write-Host "📋 Checking for pending migrations..." -ForegroundColor Cyan
    
    $migrationOutput = dotnet ef migrations list --startup-project ../MailEngine.Functions --context MailEngineDbContext 2>&1
    $hasPending = $migrationOutput | Select-String "Pending" -Quiet
    
    if (-not $hasPending) {
        Write-Host "✅ No pending migrations found. Database is up to date!" -ForegroundColor Green
        exit 0
    }
    
    Write-Host ""
    Write-Host "📝 Pending migrations detected. Preparing to apply..." -ForegroundColor Yellow
    Write-Host ""
    
    # Handle different environments
    switch ($Environment) {
        'Development' {
            Write-Host "💻 Development Environment" -ForegroundColor Cyan
            Write-Host "Applying migrations to local database..." -ForegroundColor Cyan
            Write-Host ""
            
            dotnet ef database update `
                --startup-project ../MailEngine.Functions `
                --context MailEngineDbContext
            
            Write-Host ""
            Write-Host "✅ Migrations applied successfully!" -ForegroundColor Green
            Write-Host ""
            Write-Host "📊 Verify tables were created:" -ForegroundColor Cyan
            Write-Host "  psql mail_engine_dev -c \"\dt\"" -ForegroundColor White
            Write-Host ""
        }
        
        'Staging' {
            Write-Host "🟡 Staging Environment" -ForegroundColor Yellow
            Write-Host ""
            Write-Host "⚠️  Manual verification required for staging" -ForegroundColor Yellow
            Write-Host ""
            Write-Host "Steps:" -ForegroundColor Cyan
            Write-Host "  1. Review generated SQL script: migration_*.sql" -ForegroundColor White
            Write-Host "  2. Test against staging database backup first" -ForegroundColor White
            Write-Host "  3. Then apply with connection string:" -ForegroundColor White
            Write-Host "     dotnet ef database update --connection=`"<connection-string>`" " -ForegroundColor Gray
            Write-Host "       --startup-project ../MailEngine.Functions" -ForegroundColor Gray
            Write-Host ""
        }
        
        'Production' {
            Write-Host "🔴 Production Environment" -ForegroundColor Red
            Write-Host ""
            Write-Host "⚠️  CRITICAL: Production migration requires approval" -ForegroundColor Red
            Write-Host ""
            Write-Host "Steps:" -ForegroundColor Cyan
            Write-Host "  1. Download migration artifact from GitHub Actions" -ForegroundColor White
            Write-Host "  2. Have DBA review the SQL script" -ForegroundColor White
            Write-Host "  3. Create backup of production database" -ForegroundColor White
            Write-Host "  4. Test rollback procedure" -ForegroundColor White
            Write-Host "  5. Apply migration during maintenance window:" -ForegroundColor White
            Write-Host "     dotnet ef database update --connection=`"<connection-string>`" " -ForegroundColor Gray
            Write-Host "       --startup-project ../MailEngine.Functions" -ForegroundColor Gray
            Write-Host ""
            
            $confirm = Read-Host "Continue with production migration? (yes/no)"
            if ($confirm -ne 'yes') {
                Write-Host "❌ Migration cancelled" -ForegroundColor Red
                exit 1
            }
            
            dotnet ef database update `
                --startup-project ../MailEngine.Functions `
                --context MailEngineDbContext
            
            Write-Host ""
            Write-Host "✅ Production migrations applied successfully!" -ForegroundColor Green
            Write-Host ""
        }
    }
    
    Write-Host "📚 For more information, see docs/MIGRATIONS.md" -ForegroundColor Cyan
}
finally {
    Pop-Location
}

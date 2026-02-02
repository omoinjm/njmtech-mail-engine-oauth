# Database Migration Script for Mail Engine (PowerShell)
# Usage: .\generate-migration.ps1 [-MigrationName "InitialCreate"] [-Environment "Development"]
# Example: .\generate-migration.ps1 -MigrationName "AddFailedMessagesTable"

param(
    [string]$MigrationName = "InitialCreate",
    [string]$Environment = "Development"
)

Write-Host "🔧 Mail Engine Database Migration" -ForegroundColor Cyan
Write-Host "==================================" -ForegroundColor Cyan
Write-Host "Migration Name: $MigrationName"
Write-Host "Environment: $Environment"
Write-Host ""

# Navigate to Infrastructure project
Push-Location src/MailEngine.Infrastructure

Write-Host "📦 Installing EF Core tools..." -ForegroundColor Yellow
dotnet tool install --global dotnet-ef --ignore-failed-sources 2>$null
if ($LASTEXITCODE -ne 0) {
    Write-Host "  (EF Core tools already installed)" -ForegroundColor Gray
}

Write-Host "🔄 Creating migration..." -ForegroundColor Yellow
dotnet ef migrations add "$MigrationName" `
  --startup-project ../MailEngine.Functions `
  --context MailEngineDbContext `
  --no-build

if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Migration creation failed!" -ForegroundColor Red
    Pop-Location
    exit 1
}

Write-Host ""
Write-Host "📝 Generating SQL script..." -ForegroundColor Yellow
$timestamp = Get-Date -Format "yyyyMMddHHmmss"
dotnet ef migrations script `
  --startup-project ../MailEngine.Functions `
  --context MailEngineDbContext `
  --output "migration_${MigrationName}.sql" `
  --idempotent `
  --no-build

if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ SQL script generation failed!" -ForegroundColor Red
    Pop-Location
    exit 1
}

Pop-Location

Write-Host ""
Write-Host "✅ Migration created successfully!" -ForegroundColor Green
Write-Host ""
Write-Host "📄 Files created:"
Write-Host "  - src/MailEngine.Infrastructure/Migrations/*_${MigrationName}.cs" -ForegroundColor White
Write-Host "  - src/MailEngine.Infrastructure/migration_${MigrationName}.sql" -ForegroundColor White
Write-Host ""
Write-Host "📋 Next steps:" -ForegroundColor Cyan
Write-Host "  1. Review the generated migration in Migrations/ folder" -ForegroundColor Gray
Write-Host "  2. Review the SQL script: migration_${MigrationName}.sql" -ForegroundColor Gray
Write-Host "  3. For staging: Run the SQL script against your staging database" -ForegroundColor Gray
Write-Host "  4. For production: Follow your production deployment process" -ForegroundColor Gray
Write-Host ""
Write-Host "💾 To apply locally:" -ForegroundColor Cyan
Write-Host "  dotnet ef database update --startup-project src/MailEngine.Functions --context MailEngineDbContext" -ForegroundColor White
Write-Host ""

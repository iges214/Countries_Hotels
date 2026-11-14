param(
    [string]$OutputRoot = (Join-Path $PSScriptRoot "..\artifacts\smarterasp")
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$frontendDir = Join-Path (Split-Path $repoRoot -Parent) "Frontend"
$apiDir = Join-Path $repoRoot "NoName_Hotel.api"
$projectFile = Join-Path $apiDir "NoName_Hotel.api.csproj"
$productionConfig = Join-Path $apiDir "appsettings.Production.json"
if (-not (Test-Path $productionConfig)) {
    throw "Create NoName_Hotel.api/appsettings.Production.json from docs/appsettings.SmarterAsp.example.json, fill in the SmarterASP SQL connection and a unique JWT key, then rerun this script."
}

$settings = Get-Content -Raw $productionConfig | ConvertFrom-Json
$connection = [string]$settings.ConnectionStrings.NoName_HotelDbConnectionString
$jwtKey = [string]$settings.JwtSettings.key
if ([string]::IsNullOrWhiteSpace($connection) -or $connection -match "YOUR_SQL_SERVER|YOUR_DATABASE|REPLACE_WITH|localdb") {
    throw "Production SQL connection is missing, still contains template text, or points to LocalDB."
}
if ([string]::IsNullOrWhiteSpace($jwtKey) -or $jwtKey -match "REPLACE_WITH" -or [Text.Encoding]::UTF8.GetByteCount($jwtKey) -lt 32) {
    throw "Production JwtSettings.key must be a unique random value with at least 32 UTF-8 bytes."
}

$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$outputPath = Join-Path (Resolve-Path (New-Item -ItemType Directory -Force -Path $OutputRoot).FullName).Path $timestamp
$publishPath = Join-Path $outputPath "publish"
$privatePath = Join-Path $outputPath "private-config"
New-Item -ItemType Directory -Force -Path $publishPath, $privatePath | Out-Null

Write-Host "Building React frontend..."
Push-Location $frontendDir
try {
    npm run build
    if ($LASTEXITCODE -ne 0) { throw "npm run build failed with exit code $LASTEXITCODE." }
} finally {
    Pop-Location
}
$frontendDist = Join-Path $frontendDir "dist"
if (-not (Test-Path (Join-Path $frontendDist "index.html"))) {
    throw "Frontend build did not produce dist/index.html."
}

Write-Host "Publishing ASP.NET Core API for SmarterASP Windows hosting..."
Push-Location $repoRoot
try {
    dotnet publish $projectFile --configuration Release --framework net10.0 --self-contained false --output $publishPath
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with exit code $LASTEXITCODE." }
} finally {
    Pop-Location
}

$wwwroot = Join-Path $publishPath "wwwroot"
New-Item -ItemType Directory -Force -Path $wwwroot | Out-Null
Copy-Item -Path (Join-Path $frontendDist "*") -Destination $wwwroot -Recurse -Force

# Keep production secrets out of the deploy ZIP; upload this file separately to the site root.
$publishedConfig = Join-Path $publishPath "appsettings.Production.json"
if (Test-Path $publishedConfig) {
    Move-Item -LiteralPath $publishedConfig -Destination (Join-Path $privatePath "appsettings.Production.json")
} else {
    Copy-Item -LiteralPath $productionConfig -Destination (Join-Path $privatePath "appsettings.Production.json")
}

$migrationScript = Join-Path $outputPath "database-migrations-idempotent.sql"
Push-Location $repoRoot
try {
    dotnet ef migrations script --idempotent --project (Join-Path $repoRoot "NoName_Hotel.Infrastructure") --startup-project $projectFile --output $migrationScript
    if ($LASTEXITCODE -ne 0) { throw "EF migration script generation failed with exit code $LASTEXITCODE." }
} finally {
    Pop-Location
}

$zipPath = Join-Path $outputPath "NoNameHotel-SmarterASP.zip"
Compress-Archive -Path (Join-Path $publishPath "*") -DestinationPath $zipPath

Write-Host "Deployment files created:"
Write-Host "  Package (no secrets): $zipPath"
Write-Host "  Private production settings (upload separately): $(Join-Path $privatePath 'appsettings.Production.json')"
Write-Host "  Idempotent database migration script: $migrationScript"
Write-Host "Upload package contents to the website root, then upload appsettings.Production.json beside web.config."

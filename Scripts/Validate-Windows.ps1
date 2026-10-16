[CmdletBinding()]
param(
    [string]$Configuration = 'Release',
    [switch]$Publish,
    [string]$Runtime = 'win-x64'
)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$solution = Join-Path $repoRoot 'UMLForge/UMLForge.sln'
Push-Location $repoRoot
try {
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw '.NET SDK introuvable. Installez le SDK .NET 10 puis relancez ce script.'
}
Write-Host '=== SDK .NET ===' -ForegroundColor Cyan
dotnet --info
if ($LASTEXITCODE -ne 0) { throw 'dotnet --info a échoué.' }
Write-Host '=== Restore ===' -ForegroundColor Cyan
dotnet restore $solution
if ($LASTEXITCODE -ne 0) { throw 'Restore échoué.' }
Write-Host '=== Build ===' -ForegroundColor Cyan
dotnet build $solution --configuration $Configuration --no-restore
if ($LASTEXITCODE -ne 0) { throw 'Build échoué : ne publiez pas cette version.' }
Write-Host '=== Tests ===' -ForegroundColor Cyan
dotnet test $solution --configuration $Configuration --no-build --logger 'console;verbosity=normal'
if ($LASTEXITCODE -ne 0) { throw 'Tests échoués : ne publiez pas cette version.' }
if ($Publish) {
    $appProject = Join-Path $repoRoot 'UMLForge/UMLForge.csproj'
    $publishDir = Join-Path $repoRoot "artifacts/publish/$Runtime"
    New-Item -ItemType Directory -Force -Path $publishDir | Out-Null
    Write-Host "=== Restore runtime : $Runtime ===" -ForegroundColor Cyan
    dotnet restore $appProject --runtime $Runtime
    if ($LASTEXITCODE -ne 0) { throw 'Restore pour le runtime échoué.' }
    Write-Host "=== Publication locale : $publishDir ===" -ForegroundColor Cyan
    dotnet publish $appProject --configuration $Configuration --runtime $Runtime --self-contained false --no-restore -o $publishDir
    if ($LASTEXITCODE -ne 0) { throw 'Publication locale échouée.' }
}
Write-Host 'VALIDATION TERMINEE : restore, build et tests réussis.' -ForegroundColor Green
if (-not $Publish) { Write-Host 'Relancez avec -Publish pour produire le dossier de publication Windows.' }

} finally {
    Pop-Location
}

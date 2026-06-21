[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$projectPath = Join-Path $PSScriptRoot "Zeepkist.GTR.Mod.csproj"
$outputPath = Join-Path $PSScriptRoot "bin\$Configuration\net472\net.tnrd.zeepkist.gtr.dll"

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw ".NET SDK not found. Install .NET 8 SDK: https://dotnet.microsoft.com/download/dotnet/8.0"
}

Write-Host "Restoring dependencies..."
& dotnet restore $projectPath
if ($LASTEXITCODE -ne 0) {
    throw "Dependency restore failed with exit code $LASTEXITCODE."
}

Write-Host "Building GTR mod ($Configuration)..."
& dotnet build $projectPath --configuration $Configuration --no-restore
if ($LASTEXITCODE -ne 0) {
    throw "Build failed with exit code $LASTEXITCODE."
}

if (-not (Test-Path -LiteralPath $outputPath)) {
    throw "Build succeeded but DLL was not found: $outputPath"
}

Write-Host "Built DLL: $outputPath"

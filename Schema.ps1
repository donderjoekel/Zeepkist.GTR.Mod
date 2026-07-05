[CmdletBinding()]
param(
    [string]$Endpoint = "https://graphql.zeepki.st",
    [switch]$SkipSchemaUpdate
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$projectPath = $PSScriptRoot
$generatedClientPath = Join-Path $projectPath "GraphQL\GtrClient.Client.cs"
$modifyGraphQLClientPath = Join-Path $projectPath "ModifyGraphQLClient.ps1"

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw ".NET SDK not found. Install .NET SDK: https://dotnet.microsoft.com/download"
}

Write-Host "Restoring StrawberryShake tool..."
& dotnet tool restore
if ($LASTEXITCODE -ne 0) {
    throw "Tool restore failed with exit code $LASTEXITCODE."
}

if (-not $SkipSchemaUpdate) {
    Write-Host "Updating GraphQL schema from $Endpoint..."
    & dotnet graphql update -p $projectPath --uri $Endpoint
    if ($LASTEXITCODE -ne 0) {
        throw "Schema update failed with exit code $LASTEXITCODE."
    }
}

Write-Host "Generating StrawberryShake client..."
& dotnet graphql generate $projectPath --rootNamespace "TNRD.Zeepkist.GTR" --outputDirectory "GraphQL" --disableStore
if ($LASTEXITCODE -ne 0) {
    throw "GraphQL client generation failed with exit code $LASTEXITCODE."
}

if (-not (Test-Path -LiteralPath $generatedClientPath)) {
    throw "GraphQL client generation succeeded but generated file was not found: $generatedClientPath"
}

Write-Host "Patching generated client for System.Memory alias..."
& $modifyGraphQLClientPath $generatedClientPath
if ($LASTEXITCODE -ne 0) {
    throw "Generated client patch failed with exit code $LASTEXITCODE."
}

Write-Host "Updated schema and generated client."

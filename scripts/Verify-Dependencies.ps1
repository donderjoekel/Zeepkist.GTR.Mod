param(
    [string]$Project = "Zeepkist.GTR.Mod.csproj"
)

$ErrorActionPreference = "Stop"

$nativePath = Join-Path $PSScriptRoot "..\RuntimeAssets\Native\discord_game_sdk.dll"
$expectedNativeHash = "A6B6D7DF00A58DC50248D91048578D0FE52182286B487EF89A961FD10467DBD1"
$actualNativeHash = (Get-FileHash -LiteralPath $nativePath -Algorithm SHA256).Hash
if ($actualNativeHash -ne $expectedNativeHash) {
    throw "discord_game_sdk.dll SHA256 mismatch. Expected $expectedNativeHash, got $actualNativeHash."
}

$auditOutput = & dotnet list $Project package --vulnerable --include-transitive 2>&1
$auditOutput | ForEach-Object { Write-Host $_ }
if ($LASTEXITCODE -ne 0) {
    throw "NuGet vulnerability audit failed with exit code $LASTEXITCODE."
}

if (($auditOutput -join "`n") -match "has the following vulnerable packages") {
    throw "NuGet vulnerability audit found vulnerable packages."
}

Write-Host "Dependency verification passed."

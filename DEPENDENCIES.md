# Dependency provenance

## Discord Game SDK

- Upstream: Discord Game SDK from Discord Developer Portal.
- Repository file: `RuntimeAssets/Native/discord_game_sdk.dll`.
- Architecture: Windows x86-64.
- Upstream release: unknown; original import did not record release metadata.
- SHA256: `A6B6D7DF00A58DC50248D91048578D0FE52182286B487EF89A961FD10467DBD1`.

Update process:

1. Download SDK archive directly from Discord Developer Portal.
2. Record SDK release identifier and archive checksum in this file.
3. Replace DLL with archive's Windows x86-64 binary.
4. Update expected hash in `scripts/Verify-Dependencies.ps1`.
5. Review managed wrapper compatibility and run release build plus tests.

## NuGet packages

`scripts/Verify-Dependencies.ps1` validates native DLL hash and runs NuGet transitive vulnerability audit. CI fails when audit cannot run or reports vulnerable packages.

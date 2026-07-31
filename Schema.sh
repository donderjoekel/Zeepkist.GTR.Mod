#!/usr/bin/env bash
set -euo pipefail

Endpoint="https://graphql.zeepki.st"
SkipSchemaUpdate=false

while [[ $# -gt 0 ]]; do
    case "$1" in
        --endpoint)
            Endpoint="$2"
            shift 2
            ;;
        --skip-schema-update)
            SkipSchemaUpdate=true
            shift
            ;;
        -h|--help)
            echo "Usage: $0 [--endpoint <url>] [--skip-schema-update]"
            exit 0
            ;;
        *)
            echo "Unknown option: $1" >&2
            echo "Usage: $0 [--endpoint <url>] [--skip-schema-update]" >&2
            exit 1
            ;;
    esac
done

projectPath="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
generatedClientPath="$projectPath/GraphQL/GtrClient.Client.cs"
modifyGraphQLClientPath="$projectPath/ModifyGraphQLClient.sh"

if ! command -v dotnet >/dev/null 2>&1; then
    echo ".NET SDK not found. Install .NET SDK: https://dotnet.microsoft.com/download" >&2
    exit 1
fi

echo "Restoring StrawberryShake tool..."
dotnet tool restore

if [[ "$SkipSchemaUpdate" != true ]]; then
    echo "Updating GraphQL schema from $Endpoint..."
    dotnet graphql update -p "$projectPath" --uri "$Endpoint"
fi

echo "Generating StrawberryShake client..."
dotnet graphql generate "$projectPath" --rootNamespace "TNRD.Zeepkist.GTR" --outputDirectory "GraphQL" --disableStore

if [[ ! -f "$generatedClientPath" ]]; then
    echo "GraphQL client generation succeeded but generated file was not found: $generatedClientPath" >&2
    exit 1
fi

echo "Patching generated client for System.Memory alias..."
"$modifyGraphQLClientPath" "$generatedClientPath"

echo "Updated schema and generated client."

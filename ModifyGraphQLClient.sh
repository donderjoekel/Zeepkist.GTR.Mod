#!/usr/bin/env bash
set -euo pipefail

file="${1:-}"
if [[ -z "$file" ]]; then
    echo "Usage: $0 <generated-client-file>" >&2
    exit 1
fi

if [[ ! -f "$file" ]]; then
    echo "File not found: $file" >&2
    exit 1
fi

if ! grep -q '^extern alias MemoryAlias;' "$file"; then
    sed -i '1i extern alias MemoryAlias;' "$file"
fi

sed -i 's/global::System\.ReadOnlySpan/MemoryAlias::System.ReadOnlySpan/g' "$file"

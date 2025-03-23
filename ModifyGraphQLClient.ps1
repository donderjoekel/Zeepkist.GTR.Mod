param (
    [string]$file
)

# Read the entire file as a single string while preserving newlines
$content = Get-Content -Raw $file  

# Check if the first line already contains 'extern alias MemoryAlias;'
if ($content -notmatch "^extern alias MemoryAlias;") {
    # Prepend the required line
    $content = "extern alias MemoryAlias;`n" + $content
}

# Replace 'global::System.ReadOnlySpan' with 'MemoryAlias::System.ReadOnlySpan'
$content = $content -replace "global::System\.ReadOnlySpan", "MemoryAlias::System.ReadOnlySpan"

# Write back the modified content while preserving newlines
$content | Out-File -FilePath $file -Encoding utf8

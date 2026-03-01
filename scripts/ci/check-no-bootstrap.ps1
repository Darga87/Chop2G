[CmdletBinding()]
param(
    [string[]]$Roots = @('src', 'styles'),
    [string[]]$ExcludeDirs = @('bin', 'obj', '.git', 'node_modules')
)

$ErrorActionPreference = 'Stop'

$patterns = @(
    'bootstrap(\.min)?\.css',
    'bootstrap(\.bundle|\.min)?\.js',
    'cdn\.jsdelivr\.net/.*/bootstrap',
    'unpkg\.com/.*/bootstrap',
    '/lib/bootstrap',
    '@import\s+["'']bootstrap',
    'PackageReference\s+Include\s*=\s*["'']bootstrap["'']'
)

$regex = [string]::Join('|', $patterns)
$hits = @()

foreach ($root in $Roots) {
    if (-not (Test-Path $root)) { continue }

    Get-ChildItem -Path $root -Recurse -File | ForEach-Object {
        $fullPath = $_.FullName
        foreach ($exclude in $ExcludeDirs) {
            if ($fullPath -match "[\\/]$exclude[\\/]") { return }
        }

        $match = Select-String -Path $fullPath -Pattern $regex -CaseSensitive:$false -SimpleMatch:$false
        if ($match) {
            $hits += $match | ForEach-Object {
                "{0}:{1}: {2}" -f $_.Path, $_.LineNumber, $_.Line.Trim()
            }
        }
    }
}

$bootstrapDirs = Get-ChildItem -Path src -Recurse -Directory -ErrorAction SilentlyContinue |
    Where-Object {
        $_.FullName -match '[\\/]lib[\\/]bootstrap$' -and
        $_.FullName -notmatch '[\\/](bin|obj)[\\/]'
    }

foreach ($dir in $bootstrapDirs) {
    $hits += "{0}: bootstrap directory exists" -f $dir.FullName
}

if ($hits.Count -gt 0) {
    Write-Host 'Bootstrap references detected (forbidden):'
    $hits | Sort-Object -Unique | ForEach-Object { Write-Host " - $_" }
    throw 'Anti-bootstrap guard failed.'
}

Write-Host 'Anti-bootstrap guard passed: no runtime bootstrap references found.'

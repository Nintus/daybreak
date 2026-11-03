param(
    [Parameter(Mandatory)][string]$PackagePath,
    [Parameter(Mandatory)][ValidatePattern('^[a-fA-F0-9]{40}$')][string]$Commit,
    [string]$RunId = 'local'
)
$ErrorActionPreference = 'Stop'
$packageRoot = (Resolve-Path -LiteralPath $PackagePath).Path
$binaries = @('Daymark.exe', 'Daymark.dll', 'Daymark.Core.dll')
$reference = [System.Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $packageRoot 'Daymark.dll'))
if (-not $reference.ProductVersion.Contains($Commit, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'The package must embed the supplied source commit.'
}
foreach ($name in $binaries) {
    $metadata = [System.Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $packageRoot $name))
    if ($metadata.ProductName -ne 'Daymark' -or $metadata.ProductVersion -ne $reference.ProductVersion -or
        $metadata.FileVersion -ne $reference.FileVersion) {
        throw "Inconsistent Daymark product/version metadata: $name"
    }
}
foreach ($required in @('LICENSE.txt', 'README.md', 'ROADMAP.md', 'docs/CODE_SIGNING.md',
        'docs/PRIVACY.md', 'docs/SIGNPATH_READINESS.md')) {
    if (-not (Test-Path -LiteralPath (Join-Path $packageRoot $required) -PathType Leaf)) {
        throw "Missing package documentation: $required"
    }
}
@{ commit = $Commit; run = $RunId; product = 'Daymark'; version = $reference.ProductVersion;
    signatureStatus = 'unsigned'; configuration = 'Release' } |
    ConvertTo-Json | Set-Content -LiteralPath (Join-Path $packageRoot 'build.json') -Encoding utf8
$sumsPath = Join-Path $packageRoot 'SHA256SUMS'
$lines = Get-ChildItem -LiteralPath $packageRoot -Recurse -File |
    Where-Object { $_.FullName -ne $sumsPath } | Sort-Object FullName | ForEach-Object {
        $relative = $_.FullName.Substring($packageRoot.Length + 1).Replace('\', '/')
        (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant() + " *$relative"
    }
$lines | Set-Content -LiteralPath $sumsPath -Encoding ascii
Write-Output "Verified $($binaries.Count) Daymark binaries, packaged documentation and $(@($lines).Count) checksums."

param([Parameter(Mandatory)][string]$AssetsPath, [Parameter(Mandatory)][string]$Destination)
$ErrorActionPreference = 'Stop'
$assets = Get-Content -LiteralPath $AssetsPath -Raw | ConvertFrom-Json
New-Item -ItemType Directory -Force $Destination | Out-Null
$index = @('# Daymark dependency notices', '', 'Pinned packages from the publish assets. Package metadata records each license expression or upstream license URL.', '')
foreach ($library in $assets.libraries.PSObject.Properties) {
    if ($library.Value.type -ne 'package') { continue }
    $found = $false
    foreach ($root in $assets.packageFolders.PSObject.Properties.Name) {
        $package = Join-Path $root $library.Value.path
        if (-not (Test-Path -LiteralPath $package)) { continue }
        $target = Join-Path $Destination ($library.Name.Replace('/', '-'))
        New-Item -ItemType Directory -Force $target | Out-Null
        Get-ChildItem -LiteralPath $package -File -Recurse | Where-Object { $_.Name -match '^(LICENSE|LICENCE|NOTICE|COPYING|THIRD.PARTY)' -or $_.Extension -eq '.nuspec' } | ForEach-Object {
            $relative = $_.FullName.Substring($package.Length).TrimStart('\','/').Replace('\','_').Replace('/','_')
            Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $target $relative)
        }
        $index += "- $($library.Name): https://www.nuget.org/packages/$($library.Name)"
        $found = $true; break
    }
    if (-not $found) { throw "Missing restored license metadata: $($library.Name)" }
}
$index | Set-Content -LiteralPath (Join-Path $Destination 'DEPENDENCIES.md') -Encoding utf8

$ErrorActionPreference = 'Stop'
$daymarkProject = Split-Path -Parent $PSScriptRoot
$daymarkRuntime = Join-Path $daymarkProject 'node_modules/electron/dist'
$daymarkBundle = Join-Path $daymarkProject 'release/Daymark-Windows'
if (-not (Test-Path -LiteralPath (Join-Path $daymarkRuntime 'electron.exe'))) { throw 'Install Electron first with pnpm install and node node_modules/electron/install.js.' }
New-Item -ItemType Directory -Force -Path $daymarkBundle | Out-Null
Get-ChildItem -LiteralPath $daymarkRuntime | ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination $daymarkBundle -Recurse -Force }
Move-Item -LiteralPath (Join-Path $daymarkBundle 'electron.exe') -Destination (Join-Path $daymarkBundle 'Daymark.exe') -Force
$daymarkResources = Join-Path $daymarkBundle 'resources/app'
New-Item -ItemType Directory -Force -Path $daymarkResources | Out-Null
foreach ($daymarkItem in @('src', 'desktop', 'package.json', 'LICENSE', 'THIRD-PARTY-NOTICES.md')) { Copy-Item -LiteralPath (Join-Path $daymarkProject $daymarkItem) -Destination $daymarkResources -Recurse -Force }
Copy-Item -LiteralPath (Join-Path $daymarkProject 'docs/WINDOWS.txt') -Destination (Join-Path $daymarkBundle 'START-HERE.txt') -Force
Get-ChildItem -LiteralPath $daymarkBundle -Recurse -File | Where-Object { $_.LastWriteTime.Year -lt 1980 } | ForEach-Object { $_.LastWriteTime = [datetime]'1980-01-01' }
Compress-Archive -LiteralPath $daymarkBundle -DestinationPath (Join-Path $daymarkProject 'release/Daymark-Windows.zip') -Force
Write-Output 'Built release/Daymark-Windows.zip. Extract the entire folder and launch Daymark.exe.'

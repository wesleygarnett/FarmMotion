param([string]$OutputDirectory = (Join-Path $PSScriptRoot 'dist\release'))
$ErrorActionPreference = 'Stop'
$dirty = & git -C $PSScriptRoot status --porcelain
if ($LASTEXITCODE -ne 0) { throw 'Release packaging requires a Git checkout' }
if ($dirty) { throw 'Commit or remove pending changes before packaging a release' }
$version = (Get-Content -LiteralPath (Join-Path $PSScriptRoot 'VERSION') -Raw).Trim()
if ($version -notmatch '^\d+\.\d+\.\d+$') { throw 'VERSION must use major.minor.patch' }
$stage = Join-Path ([IO.Path]::GetTempPath()) ('FarmMotionRelease-' + [Guid]::NewGuid().ToString('N'))
& (Join-Path $PSScriptRoot 'build.ps1') -OutputDirectory $stage -RunUiTests
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
$portable = Join-Path $stage 'portable'
New-Item -ItemType Directory -Path $portable | Out-Null
foreach ($name in @('FarmMotion.exe', 'FarmMotion.exe.config', 'SDL3.dll', 'SDL3-LICENSE.txt', 'FS25_FarmMotionTelemetry.zip', 'LICENSE', 'THIRD_PARTY.md', 'README.md')) {
    Copy-Item -LiteralPath (Join-Path $stage $name) -Destination $portable
}
foreach($name in @('Wpf.Ui.dll','Wpf.Ui.Abstractions.dll','System.Memory.dll','System.Buffers.dll','System.Numerics.Vectors.dll','System.Runtime.CompilerServices.Unsafe.dll','WPF-UI-LICENSE.md','MICROSOFT-RUNTIME-LICENSE.txt')) {
    Copy-Item -LiteralPath (Join-Path $stage $name) -Destination $portable
}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'CHANGELOG.md'), (Join-Path $PSScriptRoot 'SECURITY.md'), (Join-Path $PSScriptRoot 'CONTRIBUTING.md') -Destination $portable
New-Item -ItemType Directory -Path (Join-Path $portable 'assets') | Out-Null
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'assets\farmmotion.png') -Destination (Join-Path $portable 'assets')
New-Item -ItemType Directory -Path (Join-Path $portable 'docs') | Out-Null
foreach($doc in @('USER_GUIDE.md','TESTING.md','RELEASING.md','dashboard-compact.png')) { Copy-Item -LiteralPath (Join-Path $PSScriptRoot ('docs\'+$doc)) -Destination (Join-Path $portable 'docs') }
if(@(Get-ChildItem -LiteralPath $portable -Filter '*.exe').Count -ne 1 -or -not (Test-Path -LiteralPath (Join-Path $portable 'FarmMotion.exe'))) { throw 'Portable release must contain only FarmMotion.exe' }
$binary = Join-Path $OutputDirectory "FarmMotion-v$version-win-x64.zip"
Compress-Archive -Path (Join-Path $portable '*') -DestinationPath $binary -Force
$mod = Join-Path $OutputDirectory 'FS25_FarmMotionTelemetry.zip'
Copy-Item -LiteralPath (Join-Path $stage 'FS25_FarmMotionTelemetry.zip') -Destination $mod -Force
$source = Join-Path $OutputDirectory "FarmMotion-v$version-source.zip"
& git -C $PSScriptRoot archive --format=zip "--prefix=FarmMotion-v$version/" "--output=$source" HEAD
if ($LASTEXITCODE -ne 0) { throw 'Source archive requires a committed Git checkout' }
$hashes = foreach ($path in @($binary, $mod, $source)) {
    '{0}  {1}' -f (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant(), [IO.Path]::GetFileName($path)
}
$hashes | Set-Content -LiteralPath (Join-Path $OutputDirectory 'SHA256SUMS.txt') -Encoding ascii
Write-Host "Release packages: $OutputDirectory"

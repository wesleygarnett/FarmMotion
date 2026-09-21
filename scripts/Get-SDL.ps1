# Fetch a pinned official runtime. Run explicitly when updating vendored dependencies.
$ErrorActionPreference = 'Stop'
$version = '3.4.16'
$archiveSha256 = '4217944b4e51457af4a59c82d883f8443b3e65964b2acd8943484c492756c4b6'
$destination = Join-Path (Split-Path $PSScriptRoot -Parent) 'vendor\SDL3'
New-Item -ItemType Directory -Force -Path $destination | Out-Null
$staging = Join-Path ([IO.Path]::GetTempPath()) ('FarmMotion-SDL-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $staging | Out-Null
$archive = Join-Path $staging "SDL3-$version-win32-x64.zip"
Invoke-WebRequest -Uri "https://github.com/libsdl-org/SDL/releases/download/release-$version/SDL3-$version-win32-x64.zip" -OutFile $archive
if ((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash -ne $archiveSha256) { throw 'Official SDL archive checksum mismatch' }
Expand-Archive -LiteralPath $archive -DestinationPath (Join-Path $staging 'unpacked') -Force
$dll = @(Get-ChildItem -LiteralPath (Join-Path $staging 'unpacked') -Recurse -Filter SDL3.dll)
if ($dll.Count -ne 1) { throw 'Expected one SDL3.dll in official archive' }
Copy-Item -LiteralPath $dll[0].FullName -Destination (Join-Path $destination 'SDL3.dll') -Force
Copy-Item -LiteralPath (Join-Path $dll[0].DirectoryName 'LICENSE.txt') -Destination (Join-Path $destination 'LICENSE.txt') -Force
Write-Host "Verified SDL $version archive SHA256 $archiveSha256"

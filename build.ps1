param(
    [string]$OutputDirectory = (Join-Path $PSScriptRoot 'dist\build'),
    [switch]$RunUiTests
)
$ErrorActionPreference = 'Stop'
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$sources = @(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'src') -Filter '*.cs' | ForEach-Object FullName)
$sources += @(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'tests') -Filter '*.cs' | ForEach-Object FullName)
$common = @('/nologo', '/platform:x64', '/optimize+', '/warnaserror+',
    "/win32manifest:$PSScriptRoot\src\app.manifest", "/win32icon:$PSScriptRoot\assets\farmmotion.ico",
    '/r:System.Web.Extensions.dll', '/r:System.Windows.Forms.dll', '/r:System.Drawing.dll')
foreach ($target in @(@{Name='FarmMotion'; Type='exe'}, @{Name='FarmMotionUI'; Type='winexe'})) {
    & $compiler @common "/target:$($target.Type)" "/out:$OutputDirectory\$($target.Name).exe" @sources
    if ($LASTEXITCODE -ne 0) { throw "$($target.Name) compilation failed" }
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'src\App.config') -Destination (Join-Path $OutputDirectory "$($target.Name).exe.config") -Force
}
& (Join-Path $OutputDirectory 'FarmMotion.exe') --self-test
if ($LASTEXITCODE -ne 0) { throw 'Tests failed' }
if ($RunUiTests) {
    & (Join-Path $OutputDirectory 'FarmMotion.exe') --ui-test
    if ($LASTEXITCODE -ne 0) { throw 'Dashboard checks failed' }
}
$modFiles = @((Join-Path $PSScriptRoot 'mod\FarmMotionTelemetry.lua'), (Join-Path $PSScriptRoot 'mod\modDesc.xml'), (Join-Path $PSScriptRoot 'LICENSE'))
Compress-Archive -LiteralPath $modFiles -DestinationPath (Join-Path $OutputDirectory 'FS25_FarmMotionTelemetry.zip') -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'LICENSE'), (Join-Path $PSScriptRoot 'README.md'), (Join-Path $PSScriptRoot 'THIRD_PARTY.md') -Destination $OutputDirectory -Force
Write-Host "Built and tested: $OutputDirectory"

param(
    [string]$OutputDirectory = (Join-Path $PSScriptRoot 'dist\build'),
    [switch]$RunUiTests
)
$ErrorActionPreference = 'Stop'
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
$sdl = Join-Path $PSScriptRoot 'vendor\SDL3\SDL3.dll'
if (-not (Test-Path -LiteralPath $sdl)) { throw 'SDL3 dependency missing. Run scripts/Get-SDL.ps1 first.' }
if ((Get-FileHash -LiteralPath $sdl -Algorithm SHA256).Hash.ToLowerInvariant() -ne '1f98969319302a100931f4385e5918a0bd53ab07773040682d22e7edb54858c0') { throw 'SDL3 dependency checksum mismatch. Restore it with scripts/Get-SDL.ps1.' }
Copy-Item -LiteralPath $sdl -Destination $OutputDirectory -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'vendor\SDL3\LICENSE.txt') -Destination (Join-Path $OutputDirectory 'SDL3-LICENSE.txt') -Force
$wpfVendor=Join-Path $PSScriptRoot 'vendor\WpfUi'
if(-not (Test-Path -LiteralPath (Join-Path $wpfVendor 'SHA256SUMS.txt'))) { throw 'WPF UI dependencies missing. Run scripts/Get-WpfUi.ps1 first.' }
$wpfReferences=@()
foreach($lineWpf in Get-Content -LiteralPath (Join-Path $wpfVendor 'SHA256SUMS.txt')) {
    $partsWpf=$lineWpf -split '  ',2
    $dllWpf=Join-Path $wpfVendor $partsWpf[1]
    if((Get-FileHash -LiteralPath $dllWpf -Algorithm SHA256).Hash.ToLowerInvariant() -ne $partsWpf[0]) { throw "WPF UI dependency checksum mismatch: $dllWpf" }
    Copy-Item -LiteralPath $dllWpf -Destination $OutputDirectory -Force
    $wpfReferences+=('/r:'+$dllWpf)
}
Copy-Item -LiteralPath (Join-Path $wpfVendor 'WPF-UI-LICENSE.md'),(Join-Path $wpfVendor 'MICROSOFT-RUNTIME-LICENSE.txt') -Destination $OutputDirectory -Force
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$version = (Get-Content -LiteralPath (Join-Path $PSScriptRoot 'VERSION') -Raw).Trim()
if ($version -notmatch '^\d+\.\d+\.\d+$') { throw 'VERSION must use major.minor.patch' }
$assemblyText = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'src\AssemblyInfo.cs') -Raw
if (-not $assemblyText.Contains('AssemblyInformationalVersion("' + $version + '")') -or -not $assemblyText.Contains('AssemblyFileVersion("' + $version + '.0")')) { throw 'Assembly metadata does not match VERSION' }
$sources = @(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'src') -Filter '*.cs' | Where-Object { $_.Name -notin @('Dashboard.cs','AppOptionsDialog.cs') } | ForEach-Object FullName)
$sources += @(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'tests') -Filter '*.cs' | ForEach-Object FullName)
$common = @('/nologo', '/platform:x64', '/optimize+', '/warnaserror+',
    "/win32manifest:$PSScriptRoot\src\app.manifest", "/win32icon:$PSScriptRoot\assets\farmmotion.ico",
    '/r:System.Web.Extensions.dll', '/r:System.Windows.Forms.dll', '/r:System.Drawing.dll', '/r:System.IO.Compression.dll')
$common+=$wpfReferences
$wpfFramework=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\WPF'
foreach($nameWpf in @('PresentationFramework.dll','PresentationCore.dll','WindowsBase.dll')) { $common+=('/r:'+(Join-Path $wpfFramework $nameWpf)) }
$common+='/r:System.Xaml.dll'
$common+=('/resource:'+(Join-Path $PSScriptRoot 'assets\farmmotion.png')+',FarmMotion.Logo.png')
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
$modFiles = @((Join-Path $PSScriptRoot 'mod\FarmMotionTelemetry.lua'), (Join-Path $PSScriptRoot 'mod\modDesc.xml'), (Join-Path $PSScriptRoot 'mod\icon_farmMotion.dds'))
Compress-Archive -LiteralPath $modFiles -DestinationPath (Join-Path $OutputDirectory 'FS25_FarmMotionTelemetry.zip') -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'LICENSE'), (Join-Path $PSScriptRoot 'README.md'), (Join-Path $PSScriptRoot 'THIRD_PARTY.md') -Destination $OutputDirectory -Force
Write-Host "Built and tested: $OutputDirectory"

# Explicitly restore pinned WPF UI and its .NET Framework runtime dependencies.
param([string]$Destination = (Join-Path (Split-Path $PSScriptRoot -Parent) 'vendor\WpfUi'))
$ErrorActionPreference = 'Stop'
$packages = @(
    @('wpf-ui','4.3.0','net472','Wpf.Ui.dll','522f9a300fb3f3a885e7c0acbdd9b991d638f6011d2a67f3e9ccc6a3c3daad2b'),
    @('wpf-ui.abstractions','4.3.0','net472','Wpf.Ui.Abstractions.dll','0c4d22ee4089541c10fd65677d4e719a02eb8e25e7271743897d564ccb321ef2'),
    @('system.memory','4.6.3','net462','System.Memory.dll','26078aeb758c9ae985e8bf851f973026061da6a5eb4837204d0c2d2204c72955'),
    @('system.buffers','4.6.1','net462','System.Buffers.dll','b00451e91d016fbec091ad1e361f3a7015e1d91d4047f7e48a74455b2a673d79'),
    @('system.numerics.vectors','4.6.1','net462','System.Numerics.Vectors.dll','2bc500a86dcb02f2032d6d877f9e2d6e9e4a79080e57239b4198679d4031f2c7'),
    @('system.runtime.compilerservices.unsafe','6.1.2','net462','System.Runtime.CompilerServices.Unsafe.dll','5f6a7f53af3465f92beb6da873ebe0e496206c313313b98badee4355a6b25937')
)
$stageWpf=Join-Path ([IO.Path]::GetTempPath()) ('FarmMotion-WpfUi-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $stageWpf,$Destination | Out-Null
$hashesWpf=@()
foreach($packageWpf in $packages) {
    $idWpf=$packageWpf[0]; $versionWpf=$packageWpf[1]; $archiveWpf=Join-Path $stageWpf ($idWpf+'.zip')
    Invoke-WebRequest -Uri "https://api.nuget.org/v3-flatcontainer/$idWpf/$versionWpf/$idWpf.$versionWpf.nupkg" -OutFile $archiveWpf
    if((Get-FileHash -LiteralPath $archiveWpf -Algorithm SHA256).Hash.ToLowerInvariant() -ne $packageWpf[4]) { throw "Package checksum mismatch: $idWpf" }
    $unpackWpf=Join-Path $stageWpf $idWpf
    Expand-Archive -LiteralPath $archiveWpf -DestinationPath $unpackWpf
    $binaryWpf=Join-Path $unpackWpf ('lib\'+$packageWpf[2]+'\'+$packageWpf[3])
    Copy-Item -LiteralPath $binaryWpf -Destination $Destination -Force
    $hashesWpf+=((Get-FileHash -LiteralPath $binaryWpf -Algorithm SHA256).Hash.ToLowerInvariant()+'  '+$packageWpf[3])
    if($idWpf -eq 'wpf-ui') { Copy-Item -LiteralPath (Join-Path $unpackWpf 'LICENSE.md') -Destination (Join-Path $Destination 'WPF-UI-LICENSE.md') -Force }
}
$hashesWpf | Set-Content -LiteralPath (Join-Path $Destination 'SHA256SUMS.txt') -Encoding ascii
@'
MIT License

Copyright (c) Microsoft Corporation.

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
'@ | Set-Content -LiteralPath (Join-Path $Destination 'MICROSOFT-RUNTIME-LICENSE.txt') -Encoding utf8
Write-Host "Verified WPF UI 4.3.0 dependencies: $Destination"

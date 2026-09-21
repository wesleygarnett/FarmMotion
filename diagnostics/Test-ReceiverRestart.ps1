param([string]$AppPath=(Join-Path $PSScriptRoot '..\dist\beta-0.4.0\FarmMotionUI.exe'))
$ErrorActionPreference='Stop'
$assembly=[Reflection.Assembly]::LoadFrom($AppPath)
$type=$assembly.GetType('FarmMotion.Receiver')
$timer=[Diagnostics.Stopwatch]::StartNew()
$name='FarmMotionRestartTest-'+[Guid]::NewGuid().ToString('N')
$packet=[Text.Encoding]::UTF8.GetBytes('{"v":1,"active":false}'+[char]10)
function New-Receiver { [Activator]::CreateInstance($type,[object[]]@($timer,$name)) }
function Send-Packet($stream) { $stream.Write($packet,0,$packet.Length); $stream.Flush() }
$r=New-Receiver
$c=[IO.Pipes.NamedPipeClientStream]::new('.',$name,[IO.Pipes.PipeDirection]::Out)
try {
 $c.Connect(3000); Send-Packet $c
 Start-Sleep -Milliseconds 100
 Write-Output "Before restart packets: $($type.GetField('Packets').GetValue($r))"
 $r.Dispose(); Write-Output 'Original receiver disposed while producer remained open'
 $r=New-Receiver
 $failed=$false
 for($attempt=0;$attempt -lt 20 -and !$failed;$attempt++) {
  try { Send-Packet $c } catch { $failed=$true; Write-Output "Old producer handle failed: $($_.Exception.InnerException.Message)" }
  Start-Sleep -Milliseconds 50
 }
 if(!$failed) { throw 'Expected old handle to fail' }
 Start-Sleep -Milliseconds 200
 Write-Output "Replacement receiver before client reopen: $($type.GetField('Packets').GetValue($r)) packets"
 $c.Dispose()
 $c=[IO.Pipes.NamedPipeClientStream]::new('.',$name,[IO.Pipes.PipeDirection]::Out)
 $c.Connect(3000); Send-Packet $c
 Start-Sleep -Milliseconds 100
 $count=$type.GetField('Packets').GetValue($r)
 if($count -ne 1) { throw "Expected resumed delivery; got $count" }
 Write-Output 'PASS: replacement receiver gets telemetry once producer closes and reopens its handle'
} finally { $c.Dispose(); $r.Dispose() }

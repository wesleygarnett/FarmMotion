# SDL3 controller runtime

Pinned version: **3.4.16**, official Windows x64 runtime.

Upstream: https://github.com/libsdl-org/SDL/releases/tag/release-3.4.16

Archive: `SDL3-3.4.16-win32-x64.zip`

Official GitHub release SHA256: `4217944b4e51457af4a59c82d883f8443b3e65964b2acd8943484c492756c4b6`

Run `scripts/Get-SDL.ps1` to acquire and verify this pinned dependency. Ship
`SDL3.dll` beside each FarmMotion executable and include the upstream license.
FarmMotion uses ordinary two-motor rumble only. Bluetooth enhanced reports are
disabled unless explicitly opted in, to preserve DirectInput compatibility.

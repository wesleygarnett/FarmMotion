# Provenance

FarmMotion is distributed under GPL-2.0-only (LICENSE).

The Lua exporter is adapted from Mhytee's **Trueforce-For-All v0.2.6**, copyright 2026 Mhytee, GPL-2.0-only:

- https://github.com/Mhytee/Trueforce-For-All/blob/v0.2.6/gamemods/FarmingSimulator/TF4ALLTelemetry/TF4ALLTelemetry.lua
- https://github.com/Mhytee/Trueforce-For-All/blob/v0.2.6/src/TrueforceForAll.Plugin/FarmingSimulatorTelemetrySource.cs
- https://github.com/Mhytee/Trueforce-For-All/blob/v0.2.6/LICENSE

Changes: motion-only export; separate pipe and mod name; timing and vehicle identity; body vertical and local angular velocity; GUI inactivity gating; optional road-contact/tyre metadata; strict pipe write/flush failure handling. Upstream's reader informed the suspension derivative approach; its complete reader is not part of the distributed source or binaries. The companion implements a smaller motion-only signal path and Windows DirectInput output without SimHub or proprietary SDK dependencies.

The upstream LICENSE includes Mhytee's attribution to mescon/logitech-rs50-linux-driver; it is preserved verbatim. FarmMotion's FM icon was generated for this project with OpenAI imagegen; it contains no Adobe artwork or redistributed font files. See assets/README.md for the generation prompt and icon packaging process.

Windows interface declarations/constants checked against Microsoft's SDK header:
https://github.com/microsoft/win32metadata/blob/main/generation/WinSDK/RecompiledIdlHeaders/um/dinput.h

Engine API reference:
https://gdn.giants-software.com/documentation_scripting_fs25.php?version=engine

Surface telemetry uses the documented WheelPhysics ground-friction classification, tyre type, ground contact and netInfo.xDriveSpeed (radians/second), rather than assumed material IDs:
https://gdn.giants-software.com/documentation_scripting_fs25.php?category=93&class=909&version=script
https://gdn.giants-software.com/documentation_scripting_fs25.php?category=91&class=899&version=script

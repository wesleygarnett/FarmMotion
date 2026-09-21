# Contributing

Use Windows x64 with .NET Framework 4.8 and Windows PowerShell 5.1. No NuGet packages, SDK download or proprietary wheel SDK is required.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -RunUiTests
```

`src/` contains the desktop app, feedback processing and transport. `tests/` contains release hardening checks and a frozen V1 reference. `mod/` contains the FS25 Lua telemetry mod.

Keep feedback behavior changes separate from UI/packaging changes. Preserve the 10% absolute force cap, 150 ms stale-telemetry cutoff, 50 ms requested effect lifetime, foreground gating and disarmed startup. Tests must never open or arm a physical wheel. Tests use isolated named pipes and temporary recordings/settings.

For a pull request, describe the user-visible change, the checks run, and any hardware testing still needed. Do not commit personal settings, recordings, device identifiers, game logs or session notes. Contributions are under GPL-2.0-only; preserve upstream attribution.

Release steps are in [RELEASING.md](docs/RELEASING.md).

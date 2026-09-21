# Contributing

Use Windows x64 with .NET Framework 4.8 and Windows PowerShell 5.1. Pinned WPF UI and SDL dependencies are vendored and hash-checked; no package restore, SDK download or proprietary wheel SDK is required.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -RunUiTests
```

`src/` contains the desktop app, feedback processing and transport. `tests/` contains release hardening checks and a frozen V1 reference. `mod/` contains the FS25 Lua telemetry mod.

Keep feedback behavior changes separate from UI/packaging changes. Preserve the 0–100% strength range, signed full-scale output bounds, 150 ms stale-telemetry cutoff, 50 ms requested effect lifetime and foreground gating. Default-enabled startup must wait for active fresh telemetry and game focus; replay remains disabled until opted into. Tests must never open or arm a physical wheel or send controller rumble. Tests use isolated named pipes and temporary recordings/settings.

For a pull request, describe the user-visible change, the checks run, and any hardware testing still needed. Do not commit personal settings, recordings, device identifiers, game logs or session notes. Contributions are under GPL-2.0-only; preserve upstream attribution.

Release steps are in [RELEASING.md](docs/RELEASING.md).

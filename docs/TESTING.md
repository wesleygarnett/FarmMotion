# Beta validation

## Automated checks

The 0.4.1 beta passes 156 hardware-free companion checks, 22 simulated Lua mod checks, compact WPF UI checks, and the Windows GitHub Actions build. The mod passes all 15 GIANTS TestRunner modules (TestRunner 0.9.21). TestRunner success is not ModHub approval.

Companion coverage includes feedback bounds and stale-data cutoff, isolated Windows named-pipe transport/restart recovery, controller envelopes, updater archive validation and rollback, saved settings, and single-executable packaging. Lua tests include local-player gating, explicit and silent IO failure scenarios, restart-marker changes, and healthy connection stability.

Build and run app checks with build.ps1 -RunUiTests. Run tests/Test-Mod.py using Python with lupa installed. Mock UI and automated tests do not enable physical output. Tests do not replace real gameplay validation.

## Hardware and gameplay status

MOZA R3 wheel feedback has been tested during development. Exact torque, output cadence and commanded high-frequency reproduction have not been measured. Other wheel/controller models, USB/Bluetooth combinations and multiplayer need beta-tester validation. Controller output uses ordinary rumble only.

The 1.0.0.1 telemetry write/flush hotfix was confirmed working in gameplay. The later app-instance marker in app 0.4.0+ and mod 1.0.0.2 passes automated reconnect tests; recovery after restarting the app with FS25 still running needs live confirmation. Custom profile paths and game file-read behavior also need real-world coverage.

The app is unsigned. Artwork requirements and multiplayer testing remain before a ModHub submission; this is an independent GitHub beta.

## Useful beta reports

Use the repository bug-report form. Include app/mod versions, wheel/controller model, USB or Bluetooth, reproducible steps and telemetry counters. Include only the relevant error excerpt. Remove local usernames, paths and device identifiers before posting logs; do not attach whole savegames or personal settings.

# Changelog

## 0.4.1

- One public app executable: FarmMotion.exe opens the WPF interface. Console diagnostics move to developer-only FarmMotion.Diagnostics.exe.
- Release packaging and updater validate the single-executable layout.
- Upgrading from 0.4.0 or earlier requires a manual download into a fresh folder; saved tuning is preserved. Update shortcuts and re-enable Start with Windows in the new app if used.


## 0.4.0 beta candidate / telemetry mod 1.0.0.2

- App-instance marker restores mod reconnects after app restarts without relying on IO error returns. Live restart validation remains pending.
- Correct no-return file write/flush handling from mod 1.0.0.1.
- Gentler defaults: 10% strength, 0.25x sensitivity, 10% bumps/body, 100% fine texture.
- Adjustable movement response curve (default 2), sensitivity down to 0.01x, and separate 2% fine-texture ceiling.
- Road tire buzz spelling and 0.01% adjustment increments.
- Updated beta installation, compatibility and security documentation.

## Telemetry mod 1.0.0.0

- Descriptor 113, English/German descriptions, DDS placeholder icon and embedded full license.
- Correct local-player vehicle lookup, dedicated-server isolation, visible script failures and explicit pipe reconnect handling.
- All 15 TestRunner modules and 14 simulated Lua regression tests pass; live multiplayer and submission artwork remain unverified.

## 0.3.0

- One Enable/Disable output button replaces STOP; disabling resets feedback and stops replay, matching Escape.

- Wheel strength spans 0–100%, defaults to 50%, and reaches the full DirectInput command range; existing saved strengths are preserved.

- Removed processing comparison; desktop startup uses Motion-shaped V2, preserving saved gains and frequencies.

- Windows 11 Fluent dark WPF UI dashboard with Feedback, Devices and Recordings; Basic and Advanced tuning expanders inside Feedback, and the original embedded FM logo.
- Responsive layout down to 560 × 520 logical pixels, with persistent output controls and vertical scrolling.
- Compact dark preferences window, version display and existing update/startup controls.
- Feedback processing extracted into a UI-independent engine; saved tuning and comparison algorithms retained.
- Asynchronous wheel discovery ignores stale scan results; settings writes stay outside the feedback lock.
- Bundled WPF UI runtime dependencies and licenses; initial migration from 0.2.0 requires manual extraction.

144 hardware-free checks and WPF layout checks pass. Physical feedback and controller compatibility still require hardware validation.

## 0.2.0

- Independent wheel and controller output, with SDL3 ordinary rumble and separate controller strength.
- Solo selection for bumps, body movement, fine texture and road buzz without changing saved tuning.
- Output enabled by default for eligible live gameplay; STOP remains latched and replay stays opt-in.
- Remembered output devices, capability detection, controller hotplug and independent backend errors.
- App version in the UI, per-user Windows startup toggle, and GitHub release update controls.
- Separate application preferences; existing wheel tuning and All-mode processing retained.

Physical controller compatibility, simultaneous game input, real sign-in and updating from a published release require acceptance testing. The bundled mod remains 0.2.0.0; no new mod installation is required for these app features.

## 0.1.0

First packaged Windows preview release.

- Vehicle movement, suspension bumps, body motion and fine rumble through DirectInput.
- Optional road tyre buzz with an independent pitch control.
- Basic and collapsible Advanced modes, slider explanations, and recording/replay.
- Compact high-DPI dashboard and red FM Windows icon.
- Clear empty-device state and stable packet/status updates.
- Current-user-only telemetry pipe with network access denied.
- Bounded input, atomic settings saves, asynchronous bounded recording, and reduced hot-loop allocation.
- Portable Windows x64 package, bundled telemetry mod, checksums and source archive.

The bundled telemetry mod retains version **0.2.0.0** from its separate development history. The desktop release is **0.1.0**; protocol v1 and surface protocol v1 are unchanged.

This is a preview: live force cadence, wheel-driver behavior, and recovery after restarting the companion during a game still require hardware validation. See [review notes](docs/TESTING.md).

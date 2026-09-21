# Changelog

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

This is a preview: live force cadence, wheel-driver behavior, and recovery after restarting the companion during a game still require hardware validation. See [review notes](docs/REVIEW.md).

# 0.2.0 development validation

Implemented requested items 2–7. Item 1 is excluded: use MOZA software for steering resistance. No centering, damping, or native passthrough was added.

## Delivered behavior

- Independent wheel and controller rumble, including controller-only operation; separate rumble strength, remembered device identities, capability detection, hotplug and isolated backend faults.
- SDL 3.4.16 ordinary rumble backend for supported Xbox, DualShock 4, DualSense and generic devices. Bluetooth PlayStation enhanced reports are opt-in. Session-only controller identities require a fresh selection after launch.
- Enabled-by-default live output, gated by fresh active telemetry and game focus. STOP remains latched; replay and the first restart after an update remain disabled until explicitly enabled. Corrupt app preferences start disabled with an error.
- Solo selector for bumps, body, fine texture and road buzz. Original mode exposes its combined movement channel and road buzz. Saved tuning stays unchanged.
- Version 0.2.0 in the dashboard, app preferences, Windows sign-in registration, and GitHub stable/preview update check/install controls.
- Bounded update downloads, SHA-256 verification, archive/path/asset checks, executable version checks, staged helper, atomic per-file replacement, rollback on ordinary installation/restart failure and retained backups.

## Verified locally

- .NET Framework x64 build with warnings treated as errors.
- **119 hardware-free checks passed**, including the real local pipe transport, settings, rumble signal/selection primitives, release pagination/version ordering, archive rejection, actual compiled package extraction/version validation, install/rollback fixtures and cleanup.
- Frozen V1 and V2 comparisons each cover 4,000 exact force samples; V2 also checks limiter equivalence.
- Basic, Advanced, compact/scrolling dashboard and preferences screenshots reviewed. UI checks exercise solo/comparison switching, unchanged gains, device-less/controller-only selection, nonpersistent session identities, STOP surviving refresh, and PerMonitorV2 awareness.
- All 20 imported SDL functions resolved from the pinned DLL without initializing controller hardware.

## Not yet verified on hardware or external services

- Actual controller motor response, USB/Bluetooth device coverage, wheel/controller simultaneous FS25 input and rumble-writer contention.
- Wheel output timing with FS25 foreground and FarmMotion covered/minimized, and live companion restart recovery.
- A real Windows sign-out/sign-in. Development tests do not create startup entries.
- A download/install from a published GitHub release, private-repository authentication, or an OS-interrupted update. Local update fixtures and compiled-package validation passed. No release was published.

The updater retains `update-backup-*` directories. The whole multi-file replacement is not atomic: power loss can require manually restoring the retained previous files. The app does not modify the installed FS25 mod. Existing settings, installed app, game state and physical outputs were not changed during this development session.

## Test build

The runnable development copy is in `dist/v0.2.0-preview/FarmMotionUI.exe`, with SDL and executable configs beside it. It is a local development build, not a published release. Close an older FarmMotion instance before launching it. Output is enabled by default and starts only when gameplay conditions are met. No new telemetry mod is required for these companion features.

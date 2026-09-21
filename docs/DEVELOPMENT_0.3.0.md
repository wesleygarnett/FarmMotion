# WPF UI migration validation

The desktop uses WPF UI 4.3.0 dark resources and WPF controls on .NET Framework 4.8. WpfDashboard and WpfOptionsWindow replace the WinForms views; historical Dashboard and AppOptionsDialog source files are excluded from compilation. CompanionEngine owns feedback, telemetry, replay and device state independently of the view.

Validation: 128 hardware-free checks pass, including frozen signal-processing comparisons, lifecycle/update package validation, output gating, solo behavior and engine state. WPF checks run at 960x760, 640x600 and 560x520 logical window sizes, verify no horizontal scrolling and visible output toggle, and exercise navigation, comparison and solo. Rendered screenshots of compact feedback, devices, recordings, settings and scrolled diagnostics were reviewed. No real hardware output was sent.

The existing v0.2.0-preview build remains available for comparison. Version 0.3.0 is a local preview, not a published release. Copy every dependency alongside the executables. The older 0.2.0 updater rejects new WPF dependencies, so this first migration needs manual extraction. Later updates accept the WPF files.

Unverified: physical wheel/controller response, simultaneous game input, live telemetry reconnect after game/app restart, Windows sign-in startup and installation from a published release. Saved user tuning was not modified by the tests.

Final UI: Feedback, Devices and Recordings are separate tabs. Feedback contains Basic (strength/sensitivity, open by default) and Advanced (remaining tuning, collapsed by default) expanders. Settings remains in the header. Original FM artwork is embedded. Section descriptions are removed.

Framework setup follows https://wpfui.lepo.co/documentation/getting-started.html using ThemesDictionary with Dark and ControlsDictionary, registered programmatically in Program.cs because this application has no compiled App.xaml. ApplicationThemeManager.Apply(Dark) initializes the theme/system accent. UiTheme uses framework resources and controls, with no custom button colours or template replacements. The 128 hardware-free checks plus animated-expander, three-tab, unmodified framework background and compact-window checks pass.

Processing selection removed: desktop startup selects V2 after loading settings, preserving numerical tuning. Legacy algorithms remain only for regression coverage/internal compatibility. 144 hardware-free checks and UI checks pass. Wheel strength now spans 0–100% with a 50% default. Settings validation, UI and DirectInput conversion share the full-scale range; legacy console processing also supports it. Existing saved strengths are preserved. Tests verify defaults, invalid values and signed zero/half/full-scale DirectInput commands without opening hardware. No hardware output was enabled.

Single-toggle validation: actual WPF button events enable/disable and update labels; Escape disables; refresh does not re-arm. Replay disable uses the same state reset as Stop. Shared gating blocks disabled output, stale live telemetry, missing game focus and replay without consent/focus. 144 hardware-free checks and compact UI checks pass; physical device stop latency is unmeasured.

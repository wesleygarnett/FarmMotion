<p align="center"><img src="assets/farmmotion.png" width="112" alt="FarmMotion FM icon"></p>

# FarmMotion | Force feedback & rumble for Farm Simulator

A small Windows companion that turns **Farming Simulator 25** vehicle movement into steering-wheel feedback: suspension bumps, body movement, fine texture and optional road tyre buzz.

**0.3.0 preview · Windows x64 · Local-player telemetry · GPL-2.0-only**

![Dark WPF UI dashboard](dashboard.png)

## Download and install

Download the portable Windows ZIP from [Releases](https://github.com/wesleygarnett/FarmMotion/releases). While the repository is private, GitHub access is required.

1. Extract `FarmMotion-v0.3.0-win-x64.zip` into a permanent writable folder. Keep all included DLLs beside the app; WPF UI and controller rumble need them.
2. With FS25 closed, copy the included **`FS25_FarmMotionTelemetry.zip`** to `Documents\My Games\FarmingSimulator2025\mods`. Keep this inner ZIP zipped. If your Documents folder is redirected, use the mods folder beside your game's `log.txt`. Keep a copy of an older mod before replacing it.
3. Launch **`FarmMotionUI.exe`**. Select your wheel and/or rumble controller. A single eligible device is selected automatically on first use; subsequent launches remember the selection.
4. Start FS25, enable **FarmMotion Telemetry** for your save, and enter a vehicle. Check that packet counts increase.
5. Output is enabled by default and waits for fresh active telemetry and game focus. Start with low strength, then return to the game. **Disable output** disables both devices; **Enable output** resumes them.

Requires **.NET Framework 4.8** and a Windows DirectInput wheel supporting constant-force effects and/or a supported rumble controller. MOZA R3 is the initial wheel target; other wheels are unverified. Controller output uses bundled SDL3 for Xbox, DualShock 4, DualSense and compatible generic devices. Recognition as an input device does not guarantee rumble support. No installer, administrator access, SimHub or proprietary wheel SDK is needed. Run the game and app as the same Windows user.

Keep each `.exe.config` file beside its executable: these enable per-monitor DPI scaling. The binaries are currently unsigned. Release checksums are in `SHA256SUMS.txt`; verify a download with `Get-FileHash -Algorithm SHA256 <file>`.

## Everyday controls

The **Feedback** tab has a **Basic** dropdown containing only strength and sensitivity:

| Control | Effect |
|---|---|
| Maximum strength | Caps total force at 0–100% of driver full-scale; defaults to 50%. |
| Movement sensitivity | Makes incoming vehicle movement feel calmer or more pronounced. |

The **Advanced** dropdown in Feedback contains the remaining tuning sliders, Solo buttons, Save tuning and reset controls. Device selection and rumble strength remain on **Devices**; record/replay remains on **Recordings**.

The dark WPF UI layout supports windows down to **560 × 520 logical pixels**. Content scrolls vertically while navigation, Enable/Disable output remain visible. Wider windows put the live monitor beside the tuning controls. The top navigation remains **Feedback**, **Devices** and **Recordings**. The original FM logo is embedded in the application.

**Disable output** disables output and replay. **Escape while the dashboard is focused** disables output. Live output requires FS25 foreground focus and fresh active telemetry. The startup preference defaults to enabled; it does not acquire the wheel or send force on the desktop. Refreshing devices or returning to the game does not undo Disable output. Replay still starts disabled.

**Wheel** and **Controller rumble** independently choose output routes; either can run alone. **Rumble strength** is separate from maximum wheel strength. Controller low-frequency rumble follows bumps/body movement; high-frequency rumble follows fine texture/road activity. Ordinary rumble does not reproduce an exact wheel texture pitch. Use the MOZA app for steering resistance; FarmMotion adds no centering or damping.

**Solo feedback** lets you feel bumps, body movement, texture, or road buzz independently on enabled devices. Click the active **Unsolo** button again to restore the mix. Solo does not save over tuning and resets on restart. A zero-gain or inactive road channel stays silent.

PlayStation USB is the initial compatibility target. Bluetooth rumble is opt-in under **Settings** because enhanced reports can disrupt DirectInput input in other apps until controller power cycling. Device/transport compatibility and simultaneous in-game input still need hardware validation. Avoid competing game/remapper rumble writers and select one physical or virtual controller route.

**Settings** contains **Enable output when FarmMotion starts**, **Start with Windows**, Bluetooth compatibility and release updates. Windows startup registers this copy at sign-in: keep its folder in a permanent location. Checking for updates is manual; **Include preview releases** is enabled by default and can be switched off for stable-only updates. Private GitHub releases need a token with repository Contents read access (held only in the dialog), or use **Release page**. Installation stages and verifies the package, closes the app, replaces files with rollback on copy failure, and restarts with output disabled once. Settings stay outside the app folder. Updating FarmMotion does not install the game mod. If power is lost during installation, recover the previous files from the retained `update-backup-*` folder.

**Save tuning** and normal window closure save tuning to `%LOCALAPPDATA%\FarmMotion\settings.json` and device/startup-output preferences to `app-options.json` in the same folder. Reset strength & sensitivity restores only those controls to 50% / 0.25x. Updates preserve saved tuning.

## Feedback processing

The desktop app uses **Motion-shaped V2**: per-wheel filtering, layered texture and softer peak compression. Older saved processing selections move to V2 on launch while retaining all gain and frequency settings.

Road tyre buzz is separate from movement texture. It requires eligible agricultural tyres, rotation, travel speed and the game's broad road-class ground contact. Its 50–90 Hz pitch is a commanded software carrier; actual reproduction depends on the output loop and wheel driver. The road amount is a percentage of the selected strength cap, not driver full-scale. Road buzz uses remaining force headroom so larger bumps retain priority. This is not a precise asphalt-material detector.

The graph shows incoming suspension and acceleration, a force preview, and commanded force—not measured wheel torque. This app synthesizes tactile movement feedback; it is not Logitech TrueForce or a physical steering-rack simulation.

## Record and replay

**Record drive** saves a short `.fmr` telemetry recording. **Stop recording** finishes it. **Load replay** and **Replay again** start disabled. To send replay output to devices, explicitly check **Allow device output during replay**, enable output, and keep the dashboard focused. **Return to live** disables again.

Recordings are local and never uploaded. Files larger than 64 MB are rejected. Recording uses a bounded queue; storage that cannot keep up stops recording with an error instead of accumulating unlimited data.

## Troubleshooting

| Symptom | Check |
|---|---|
| no device detected | Check power, USB and the wheel driver, then Refresh wheels. Detection only lists attached force-feedback devices. |
| Packets stay at zero | Enable the mod for the save; ensure game and app run as the same user. If it happened after restarting the companion, leave the app open and save/restart FS25. |
| Packets arrive but output is quiet | Enter a vehicle, close menus, enable output, focus FS25 and check route selection, strength and Solo. |
| Road buzz is absent | Install the bundled mod; check road amount, tyre type, travel speed and the surface status line. |
| Text is blurry | Keep the supplied config files beside the executables and avoid a Windows compatibility override that forces bitmap scaling. |
| No device output during replay | Allow replay output, enable output separately, and keep the dashboard focused. |
| Controller missing or quiet | Check SDL3.dll, device rumble capability, selected route and USB/Bluetooth compatibility; Refresh controllers retries a fault. |

Do not assume the displayed loop frequency proves physical wheel response. See the [review and known limitations](REVIEW.md).

## Build and test

On Windows x64 with Windows PowerShell and .NET Framework 4.8:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -RunUiTests
```

Outputs go to `dist/build`; use `-OutputDirectory PATH` to choose another folder. The build uses the framework compiler already installed with Windows, treats warnings as errors, and runs the hardware-free suite. No NuGet restore or .NET SDK is required.

UI checks use a separate test-instance mutex, mock data and no hardware output. They verify the real WPF UI controls, compact layouts without horizontal scrolling, persistent output-toggle access, solo without gain changes, V2 startup and the preferences window, then save screenshots beside the test executable. The full hardware-free suite currently contains 144 checks.

WPF UI 4.3.0 and compatible .NET Framework dependencies are pinned and hash-checked under vendor/WpfUi. The optional scripts/Get-WpfUi.ps1 script reacquires these dependencies. The UI is built in C# using WPF controls and the documented WPF UI ThemesDictionary (Dark) and ControlsDictionary, with ApplicationThemeManager applying the dark theme and system accent. Native Fluent control templates supply button, checkbox, expander, slider and card styling; local colour/template overrides are not applied.

**Upgrading from 0.2.0:** extract the complete 0.3.0 package manually into a new folder. The older updater does not accept the new WPF dependency files. Existing preferences and tuning are loaded from the same local application-data folder; re-save Start with Windows if moving the app.

`FarmMotion.exe --self-test` runs the checks; `--list-wheels` lists devices and `--monitor` runs console diagnostics. Everyday use should launch `FarmMotionUI.exe`.

Run `release.ps1` from a committed checkout to produce the portable ZIP, mod ZIP, committed-source ZIP and checksums. See [contributing](CONTRIBUTING.md), [release procedure](RELEASING.md), [changelog](CHANGELOG.md), and [security](SECURITY.md).

## License and attribution

GPL-2.0-only. The Lua exporter is adapted from Mhytee's Trueforce-For-All v0.2.6; attribution and upstream sources are in [THIRD_PARTY.md](THIRD_PARTY.md). License and source accompany releases. FarmMotion is an independent project, not affiliated with GIANTS Software, MOZA or Adobe.

## ModHub validation

The bundled telemetry mod is now 1.0.0.0 (descriptor 113) and passes all 15 GIANTS TestRunner modules. See [fixes and remaining submission work](modhub-test-2026-09-21/FIXES.md). Local-player multiplayer isolation is implemented but real multiplayer playtesting remains outstanding. The mod icon is a code-drawn placeholder, not approved ModHub artwork. Reinstall the bundled telemetry ZIP to use the updated exporter.

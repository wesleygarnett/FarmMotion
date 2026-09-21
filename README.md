<p align="center"><img src="assets/farmmotion.png" width="112" alt="FarmMotion logo"></p>

# FarmMotion

Ever wish farm simulator had force feedback on steering wheels? This mod fixes that. Feel every bump in the road and field, and customize how strong you want to feel it.

For **Farming Simulator 25 on Windows**. Tested on Moza R3. Beta release.

## Downloads

| Download | What you need |
|---|---|
| **[Windows app — v0.3.0](https://github.com/wesleygarnett/FarmMotion/releases/download/v0.3.0/FarmMotion-v0.3.0-win-x64.zip)** | Contains **FarmMotionUI.exe** and everything it needs. Extract this ZIP. |
| **[FS25 mod — v1.0.0.0](https://github.com/wesleygarnett/FarmMotion/releases/download/v0.3.0/FS25_FarmMotionTelemetry.zip)** | Copy this ZIP into your FS25 mods folder. **Do not extract it.** |

[Release notes and checksums](https://github.com/wesleygarnett/FarmMotion/releases/tag/v0.3.0)

Preview release. While this repository is private, downloads require access to it.

## Install

1. **Download both files above.** Extract the Windows app ZIP into a folder you want to keep.
2. **Close FS25.** Copy `FS25_FarmMotionTelemetry.zip` into `Documents\My Games\FarmingSimulator2025\mods`, replacing the old copy if installed.
3. **Run FarmMotionUI.exe** from the extracted app folder. On **Devices**, select your wheel or controller.
4. **Start FS25**, enable **FarmMotion Telemetry** for your save, and enter a vehicle.

Keep the app's files together. Output starts enabled and waits for active gameplay. New settings default to **50% wheel strength**; existing tuning is preserved. Adjust **Basic → Wheel strength** before driving, and use **Disable output** to stop feedback.

Requires Windows x64 and .NET Framework 4.8. The app and mod both need to be running for feedback.

## Controls

- **Feedback:** Basic strength/sensitivity; Advanced effects, frequencies and Solo.
- **Devices:** independent wheel and controller output, with separate rumble strength.
- **Recordings:** record and replay telemetry to compare tuning.
- **Settings:** start with Windows and check for updates.

![FarmMotion dashboard](docs/dashboard-compact.png)

## More information

[User guide](docs/USER_GUIDE.md) · [Changelog](CHANGELOG.md) · [Build instructions](CONTRIBUTING.md) · [License](LICENSE)

The mod passes all 15 GIANTS TestRunner modules. Live multiplayer, hardware compatibility and ModHub submission artwork still need validation; this is not a ModHub-approved release. [Test details](docs/modhub-test-2026-09-21/FIXES.md)

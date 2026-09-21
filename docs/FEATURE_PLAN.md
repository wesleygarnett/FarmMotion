# FarmMotion feature investigation and implementation plan

Development scope update: the user chose MOZA software for resistance. Native passthrough, centering and damping (item 1 / segment D) are excluded from implementation. The remaining sections document the original investigation and acceptance targets.

Planning scope: the seven requested features. No runtime settings, installed app, hardware output, startup registration, or release publication were changed during this investigation.

## Recommendation

Make FarmMotion capable of independently driving a wheel and a rumble controller from the same telemetry. Treat native FS25 force feedback as optional. Add optional wheel centering/damping if the wheel feels too loose without native feedback. Do not make native passthrough a dependency for controller support.

Deliver this as several small changes with replay comparisons between them. Preserve existing tuning, Original/V1/V2 comparison modes, and the current all-effects wheel waveform. Output enabled by default means ready to run when live-game conditions are met; it does not mean acquiring the wheel or vibrating at Windows login.

## Current implementation findings

- `src/Wheel.cs` opens the wheel using background exclusive DirectInput access. It sends one constant-force effect with a requested 50 ms lifetime and a 10% command clamp.
- `src/Dashboard.cs` owns the output thread and a single `armed` flag. It opens the wheel when armed, even if game-focus gating makes the commanded force zero. It requires a selected wheel for any output and resets arming during refresh/device selection/replay transitions.
- `src/Feel.cs`, `src/FeelV2.cs`, and `src/RoadTexture.cs` already contain separate signal ingredients, but expose a final signed wheel-force sample rather than a shared channel frame.
- `FeelSettings` currently mixes only feel parameters; application preferences and output-device selection are not persisted separately.
- App version is already 0.1.0 in `VERSION` and assembly attributes. The dashboard does not display it. Release packaging and assembly metadata have separate version sources.
- Distribution is a portable ZIP. Repository instructions describe private releases; current GitHub visibility and latest published release were not verified. There is no startup registration or updater.
- The current checkout includes release-preparation changes beyond the saved night handoff, including foreground-process caching and Lua write-failure handling. The handoff's older test count is not a current test result. Live restart recovery and foreground-game output timing remain unverified.

## 1. Native wheel feedback: ownership, passthrough, replacement

### Finding

The acquisition in `Wheel.Open` explains a direct conflict: flags `9` request exclusive/background access. Microsoft's DirectInput documentation requires exclusive acquisition for force feedback. Changing that flag to nonexclusive is not a supported solution for combining two applications' effects. Merely writing zero does not release ownership.

The separately reported FS25 behavior when another controller connects is treated as a user-observed game limitation. This investigation did not establish its internal cause or prove that every hardware combination behaves identically. It can occur independently of FarmMotion's acquisition.

There is no implemented stream of native game effects for FarmMotion to pass through. Capturing those commands through game integration, an API proxy, or a virtual-device driver would be a separate substantial project. None inherently fixes the game choosing to stop generating wheel feedback when another controller is present.

### Proposed implementation

1. Make wheel ownership follow actual output eligibility. Do not acquire merely because startup output is enabled. Stop and release on disable/shutdown; define deliberate release/reacquisition on leaving the game without continuous acquire/release thrashing during brief telemetry gaps.
2. First compare existing motion feedback with native FFB disabled. If driver-level centering already provides the desired steering weight, an app spring may be unnecessary. Do not assume that driver centering survives app acquisition on every wheel.
3. If needed, add optional **Centering** and **Damping**, separate from terrain/movement strength. Begin with simple fixed behavior. Speed-dependent weight is a later tuning option, not a claim of native-effect parity or simulated steering-rack physics.
4. Investigate device-supported spring/damper effects versus a software spring using measured steering position. The current wheel interface does not read position, so software centering requires axis range/calibration, position sampling, and damping filtering.
5. Enforce an explicit total force budget. The present 10% clamp covers only the constant-force effect; separate spring/damper effects can add to it. Reserve headroom and bound each effect, or combine them into one bounded software command. Keep existing motion-only output unchanged when centering/damping is off.

**Do we need native FFB?** Not for FarmMotion's motion feedback or simultaneous wheel/controller output. We need a comfortable source of centering/resistance if the wheel otherwise feels loose. That can come from the driver or FarmMotion. Exact FS25 native effect composition remains unverified; an older GIANTS Drivable source is insufficient to claim exact FS25 parity.

**Acceptance:** R3 steering/buttons remain usable in FS25; wheel plus controller works; enabling/disabling does not leave force running; native-only restoration is tested without promising automatic game reacquisition; optional centering has correct direction and total bounds; compare user feel with the saved V2 baseline.

## 2. Generic controller rumble alongside wheel feedback

### Proposed backend and coverage

Use a small controller-output adapter, with an SDL3 evaluation as the first prototype. SDL provides ordinary low/high-frequency rumble, controller discovery, and device-specific handling across Xbox and supported PlayStation/generic devices. A native x64 DLL can be called from the existing .NET Framework app; no migration of the whole UI is inherently required. Pin the dependency, include its license, and extend the explicit release allowlist.

An XInput-only backend is a simpler fallback or first proof of concept, but it does not satisfy native DualShock/DualSense coverage by itself. Avoid depending on virtual Xbox-controller software for the core design: it introduces another device into a game already reported to mishandle multiple controllers.

| Device | Planned support and limits |
|---|---|
| Xbox/XInput-compatible | Ordinary two-motor rumble; validate wired and wireless independently. |
| DualShock 4 / DualSense over USB | SDL ordinary rumble path; no adaptive triggers or advanced haptics. Verify game inputs still work. |
| PlayStation over Bluetooth | Separate compatibility investigation. SDL enhanced reports needed for rumble can disrupt DirectInput input in other applications until power cycling. Do not silently enable this mode or advertise parity before testing FS25. |
| Generic controllers | Only devices/drivers exposing a supported rumble capability. Input support alone does not establish output support; show unavailable capability clearly. |

### Signal and output design

- Expose pre-carrier bump/body signals and texture/road activity envelopes. Map bump/body magnitude through a short attack/release envelope primarily to the large/low motor; map fine texture and road activity primarily to the small/high motor. Tune weights by feel.
- Do not send the signed wheel waveform directly or just take its absolute value at a slower update rate. Carrier sampling, cancellation, and compression would produce inconsistent rumble. Generic rumble controls motor intensity; it cannot preserve arbitrary 28/85 Hz wheel pitch.
- Give controller output its own strength control and saturation. The wheel's 10% torque cap is not an appropriate rumble intensity limit.
- Support wheel only, controller only, and both. Device selectors, enable states, connection state, and failures should be independent. Master STOP stops both. Failure of one backend should not silently stop the healthy backend.
- Use a bounded rumble refresh cadence, initially around 50–100 Hz, independent of the faster wheel loop. Keep controller I/O off the wheel timing path and service SDL events/joystick updates as its API requires. Use short-duration commands where supported and explicitly send zero on every stop path; actual firmware expiry remains a hardware test.
- Identify/deduplicate devices, handle hotplug, and avoid sending to both physical and virtual representations. XInput slots are not stable physical-device identities. Device identification quality must be exposed honestly.
- Test contention with FS25/Steam Input/other remappers. Multiple writers to the same controller are not guaranteed to mix. FarmMotion owning rumble may require native game rumble disabled.

**Acceptance:** wheel-only output remains identical; controller-only works with no wheel attached; both run together; bumps feel slower/heavier than fine texture; unplugging either device leaves the other functional; pause/stale telemetry/STOP/exit silence both; steering and controller inputs work while rumble is active. Publish a tested device/transport matrix rather than claiming universal generic support.

## 3. Output on by default

Introduce an application preference `EnableOutputOnStartup`, default true, with a clearly labelled toggle. Persist the preference, not a transient currently-running motor state. Keep tuning migration independent.

- Startup becomes **Enabled — waiting for game/telemetry/device**. Acquire and send only when a valid selected device, fresh active telemetry, and live FS25 foreground conditions are met.
- Remember explicitly selected devices. A unique eligible device may be auto-selected; ambiguous or missing devices require a choice. Do not auto-route to an arbitrary replacement device after disconnect.
- STOP/disable remains latched for the current session. Refresh, hotplug, focus regain, or new packets must not undo it. Separate requested output, readiness, and actual sending state instead of spreading assignments to `armed` throughout the UI.
- Replay stays a deliberate opt-in and starts disarmed. Startup auto-enable applies only to live operation. UI tests must always bypass real hardware.
- On faults, stop the affected backend and show its error; avoid automatic retry loops that repeatedly seize the wheel. A temporary focus loss can resume an already enabled session; an explicit stop or fault cannot be treated the same way.

**Acceptance:** fresh install and migrated settings have the requested default; launch outside FS25 produces no force/acquisition; first eligible live session starts without clicking Enable; STOP survives device refresh/focus changes; replay remains opt-in; existing gains stay unchanged.

## 4. Start with Windows toggle

Implement per-user startup at sign-in, not a service or an administrator task. Use the current user's Run key with a properly quoted stable launcher/executable path and `--startup` argument. Only modify FarmMotion's own entry. Default this separate preference off.

The saved development app path was in `%TEMP%`; a reliable startup/updater design needs a durable location. Offer a per-user installation under `%LOCALAPPDATA%\FarmMotion\app`, retaining portable operation for users who prefer it. A portable registration must clearly target its current path and report if it moves.

Handle `--startup` in `Program.Main` without falling into the console argument parser. The first version can open normally; an optional follow-on is minimized startup with a tray icon offering Open, STOP, and Exit. Make duplicate launches activate the existing instance instead of raising the current mutex error. Detect/report Windows disabling startup without repeatedly fighting the user's OS setting. Keep startup registration and executable relocation consistent across updates.

**Acceptance:** toggle creates/removes only the app's entry, quoted paths work, a real sign-out/sign-in starts exactly one instance, no output occurs on the desktop, tray controls work if included, and moving/updating the app cannot leave a stale startup target.

## 5. Solo each feedback type

Expose a runtime-only channel selection: **All**, **Individual bumps**, **Body movement**, **Fine texture**, and **Road tyre buzz**. Add a Solo button beside each effect; choosing another moves the solo, and clicking it again restores All. Display the active solo prominently. If centering/damping are added, give them explicit treatment in the same solo model; movement-only solo should not hide an unexplained background centering contribution.

Introduce a shared channel-frame API while retaining legacy force entry points for comparison. Continue updating every filter while solo is active; mask channels at output composition. Never implement solo by saving zero values into the user's tuning. Preserve all gains and clear solo on restart and comparison-mode changes.

All-mode output must remain equivalent to the current algorithms, including V2 compression/headroom and road mixing. Do not auto-normalize or boost a soloed channel. Combined output can differ from the sum of isolated output because the existing mixer is nonlinear; document this and avoid claiming additive identity. Test the chosen compression/headroom policy explicitly.

Original 18 Hz mode is one combined movement channel, not independent bump/body/texture channels. Label it honestly and expose only Original movement and Road buzz there. V1/V2 expose the four requested channels. A solo whose gain is zero or whose road eligibility is absent stays silent with an explanation.

Apply the same solo selection to wheel and controller; let users disable either device separately. Keep telemetry recording unchanged so the same replay can compare channels.

**Acceptance:** All matches the old output across deterministic recordings; every solo excludes the others; toggling solo preserves settings/filter history; no extra gain is introduced; inactive/stale data remains silent; UI and output agree for each comparison mode.

## 6. Show version in UI

Display `FarmMotion v0.1.0` unobtrusively in the title/header and expose copyable version information near update controls. Read assembly informational version rather than hardcoding another UI string. Use `VERSION` to generate assembly version metadata during builds, or fail the build if the two disagree.

Keep desktop version and telemetry-mod version distinct. The included mod is versioned independently. Check the version remains readable in Basic/Advanced/compact layouts and that packaged ZIP, executable, and UI agree.

## 7. Update from GitHub releases

Split this into a useful update check and a complete restart-and-install updater.

### First delivery: discover releases

- Add **Check for updates**, current version, release notes, and download/open-release actions. Network work must not block the UI or output thread.
- Fetch metadata from the fixed `wesleygarnett/FarmMotion` repository using GitHub's documented API. The `latest` endpoint excludes prereleases; this project uses preview releases. Define Stable and Preview channels and enumerate published releases for Preview, with pagination and semantic version comparison. Never compare version strings lexicographically or assume list order is SemVer order.
- Handle offline/rate-limited/authentication cases separately from "up to date." Missing/wrong-architecture assets are not installable updates.
- While the repository remains private, a logged-in browser can support a manual release-page workflow. Fully automated private downloads require explicit authentication. Do not embed a PAT/client secret or assume the app inherits developer `gh` credentials. Public releases simplify this path, but changing visibility is outside this plan's authorization.

### Complete updater

1. Establish the stable app/launcher location shared with Windows startup. Continue to support portable mode explicitly, including unwritable-directory fallback to a manual download.
2. Download the exact release ZIP and checksum/manifest to a unique staging directory. Handle GitHub asset redirects without forwarding authentication headers to unrelated hosts. Validate size, expected asset identity, version/architecture, checksum, and extraction paths. A checksum from the same release detects corruption; it is not independent publisher authentication. Signed release manifests/code signing are desirable follow-on distribution work.
3. Extract only expected application files; reject traversal, absolute paths, duplicate destinations, and invalid packages. Preserve user settings/recordings separately. Existing release scripts use an allowlist: extend it deliberately for SDL/updater/launcher dependencies.
4. On **Install and restart**, stop all outputs, finish recording, save settings, and close cleanly. A separate helper waits for actual process exit before switching app files/version directories. Avoid replacing running executables or force-killing normal shutdown.
5. Retain a previous version and make interrupted installation recoverable. Validate relaunch, keep a usable launcher after failure, and avoid an update loop. Relaunch once with output off and show the new version; ordinary future launches honor the startup-output preference. This one-time update behavior should be explicit in the UI.
6. Do not silently replace the FS25 mod while the game is running. Keep application updates separate from mod installation; show bundled/required mod versions and instructions when a release needs a newer mod.

**Acceptance:** stable/preview selection, equal/older/malformed versions, private access, offline/rate limits, missing assets, wrong checksums, interrupted download/extraction, locked files, unwritable destination, failed restart, rollback, startup-path continuity, and preservation of user settings. Exercise install/update using local fixture packages before a real release.

## Delivery sequence and gates

| Segment | Work | Completion gate |
|---|---|---|
| A — visible quick improvements | UI version; separate app preferences; default-on state model and remembered devices | UI/migration checks; no unwanted desktop output; STOP/replay behavior verified |
| B — shared effect channels | Refactor channels, add Solo, preserve legacy/all-mode mixing | Existing suite plus deterministic waveform regression; replay/feel comparison |
| C — independent output devices | Controller backend spike, then mapping/routing/hotplug/UI | Xbox and PlayStation USB tests; wheel/controller simultaneous test; Bluetooth status explicit |
| D — steering resistance | Compare native-off feel; optional bounded spring/damping if needed | Correct centering, total-force budget, and subjective R3 comparison |
| E — stable app lifecycle | Durable app location, tray behavior, startup toggle, release check | Real login/single-instance check and version/channel/auth handling |
| F — complete updater | Staging/helper/restart/rollback and package integration | Fixture failure cases, real old-to-new update, settings/startup preservation |

A can begin without choosing a controller library. C depends on B's channel interface. D does not block rumble. Windows startup can target a durable portable path before a launcher exists, but its final lifecycle should be coordinated with F to avoid replacing the registration design twice. The release checker can ship before automatic installation.

Cross-cutting verification: benchmark the wheel loop with FS25 in the foreground and FarmMotion covered/minimized before claiming 50–90 Hz wheel texture reproduction. Windows 11 does not guarantee elevated timer resolution for an occluded/minimized window-owning process. The current 2 ms sleep is not a measured output rate. Assess timer strategy from measured gaps, keep controller/network I/O away from wheel scheduling, and retain the existing stale cutoff. Reproduce companion restart recovery during a supervised game session before relying on updater restarts while FS25 stays open.

## Research sources

- Microsoft: [DirectInput cooperative levels](https://learn.microsoft.com/en-us/previous-versions/windows/desktop/ee416848(v=vs.85)). Exclusive access is an ownership constraint, not an effect-mixing facility.
- SDL: [ordinary gamepad rumble](https://wiki.libsdl.org/SDL3/SDL_RumbleGamepad), [enhanced-report mode and PlayStation compatibility caveat](https://wiki.libsdl.org/SDL3/SDL_HINT_JOYSTICK_ENHANCED_REPORTS).
- Microsoft: [XInputSetState](https://learn.microsoft.com/en-us/windows/win32/api/xinput/nf-xinput-xinputsetstate), [Run/RunOnce registry keys](https://learn.microsoft.com/en-us/windows/win32/setupapi/run-and-runonce-registry-keys), and [timeBeginPeriod](https://learn.microsoft.com/en-us/windows/win32/api/timeapi/nf-timeapi-timebeginperiod).
- GitHub: [Releases REST API](https://docs.github.com/en/rest/releases/releases), [release assets API](https://docs.github.com/en/rest/releases/assets).

No runtime changes or hardware tests were performed for this plan. Device feel, coexistence, Bluetooth input compatibility, native-effect restoration, and restart recovery require the explicit acceptance work above.

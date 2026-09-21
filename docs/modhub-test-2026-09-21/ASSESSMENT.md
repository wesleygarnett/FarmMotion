# FarmMotion ModHub test — 2026-09-21

**Result: FAIL. The current shipped telemetry mod is not ready for ModHub submission.**

Tested the exact ZIP from dist/v0.3.0-preview/FS25_FarmMotionTelemetry.zip. Its Lua and modDesc.xml match the current source byte-for-byte. ZIP integrity passed. No mod files, installed mods, saved games, companion settings or hardware output were changed. This was a TestRunner/static assessment, not a new in-game test or a submission.

## Evidence and environment

- Supplied folder: TestRunner_public_0_9_20. The executable and result metadata identify **TestRunner 0.9.21**, build 2026-09-08 04:55:53.
- Executable SHA-256: 2351a66b0501f7299dc60f55f99f8ba0d115f9209433de6c5fb44baa7ea1fac4
- Mod ZIP SHA-256: c2e4282aaff79ef43635efbc27672c27f3cd9a5661e079589f7c821b8f3fcb14
- GIANTS Editor 10.0.13 and the installed FS25 game were resolved using explicit paths.
- The existing game log reports ModDesc Version 113. This corroborates the runner's expected version, but the historical game log is not a fresh playtest.
- Runner output: **15 modules, 3 failed, 12 succeeded; all 6 data collectors passed.**
- There are no I3D, texture or store-item assets. Their successful modules do not demonstrate visual or gameplay validation; EditorCheck explicitly had no I3D files to check.
- Process exit code was 0 despite the FAIL result. Automation must parse testrunOutcome in the XML.
- Original [HTML report](TestRunner-FAIL.html), [XML report](TestRunner-FAIL.xml), and [execution log](TestRunner.log) are preserved.

## TestRunner findings

| Module | Finding | Next step |
|---|---|---|
| ModDescCheck | descVersion 92; required range 113–113 | Target the installed/current patch with descriptor 113 and retest. The forum's older version-95 example is not the runner's current requirement. |
| ModDescCheck | Missing German description | Add accurate German text alongside English. |
| ModDescCheck | Missing iconFilename | Add a compliant original mod icon and reference it. |
| ModDescCheck | Mod version 0.2.0.0 fails major-version requirement | Choose a ModHub mod release version with major at least 1, such as 1.0.0.0; desktop and telemetry mod versions are separate. |
| ModDescCheck | English description lacks CDATA | Wrap localized descriptions in CDATA. |
| ModDescCheck | Missing changelog, inferred from version | Include meaningful release history; review this after choosing the intended submission version. |
| ModDescCheck | multiplayer supported=false | Multiplayer is not currently claimed. Implement and test support or ask GIANTS whether this single-player script is eligible; do not simply change the flag. |
| ObsoleteFiles | LICENSE has an unsupported file format | Resolve acceptable attribution/license packaging. Do not discard upstream license obligations merely to clear the checker. |
| PublicLuaCheck | Eleven pcall uses flagged as hiding errors | Review protected calls and error reporting, preserving disconnect recovery; do not disguise calls to evade the check. |

The Lua findings are at lines **60, 63, 73, 79, 83, 85, 101, 110, 128, 132, 137**.

Current behavior: failed vehicle lookup becomes inactive telemetry; failed optional velocity/surface calls omit data; pipe-open failure retries; payload failure sends inactive telemetry; write/flush failure closes the connection and retries. Exceptions are not logged. Some handling is intentional, but unexpected failures are hidden. A sensible revision would separate expected missing-companion/disconnect conditions from unexpected game/API failures and provide bounded error logging. Logging alone may still leave the runner's pcall pattern check failing, so retesting and potential GIANTS review are necessary.

## Manual guideline assessment

Based on the [GIANTS ModHub guidelines](https://forum.giants-software.com/viewtopic.php?t=209169), reviewed 2026-09-21:

- Filename FS25_FarmMotionTelemetry.zip follows the prefix requirement and contains no version suffix.
- Extra Lua scripts make this a PC-only submission, not console/crossplay.
- A mod icon should be 512×512 BC1 DDS without mipmaps, named icon_*.dds.
- The guidelines prohibit AI-created imagery. assets/README.md records the current FM artwork as image-generated. It is **not in the tested mod ZIP**, but should not be reused as the ModHub submission icon.
- At least three 1600×900 submission screenshots are required. The repository's desktop UI captures are not a prepared in-game screenshot set.
- Script descriptions should explain functionality and required actions. Clarify the external Windows companion requirement, start/enable workflow, platform limits and any hotkeys.

The tool cannot establish acceptance of an external companion dependency. That needs confirmation during GIANTS review. No account changes, uploads, bug reports or messages were sent.

## Remaining validation

After addressing submission metadata and Lua diagnostics, rerun this exact tool against the rebuilt ZIP. Then perform fresh in-game checks: clean loading/logs, vehicle entry/exit, pause/menu behavior, companion absent, connection loss/reconnect, save/reload, and supported vehicle types. Multiplayer needs separate implementation and client/server testing if it will be advertised. Hardware feedback and controller coexistence are separate companion acceptance tests.

## Reproduction

Run in a writable test folder, with an isolated copy of editor.xml and pre-created results/logs directories:

```powershell
.\TestRunner_public.exe .\FS25_FarmMotionTelemetry.zip -g 'F:\Games\SteamLibrary\steamapps\common\Farming Simulator 25' -e 'C:\Program Files\GIANTS Software\GIANTS_Editor_10.0.13\editor.exe' --editorXML .\editor-test.xml --outputPath .\results --logPath .\logs --noPause --disableAutoOpen --verbose
```

No modules were skipped through --skipGuiPrograms. The runner did not need Editor geometry checks because the mod contains no I3D files.

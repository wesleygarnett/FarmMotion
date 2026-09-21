# ModHub technical fixes — 2026-09-21

**TestRunner 0.9.21: PASS, all 15 modules; 6/6 collectors passed.**

Corrected package: mod version **1.0.0.0**, descriptor **113**. The desktop remains 0.3.0.
ZIP SHA-256: ea149b94b9d6d98d6f35aabcaaf6d9a28ed2ce3a6a65a2af42909273b89c5145

## Changes
- English/German CDATA descriptions explain setup, local telemetry and the separate Windows companion.
- Initial ModHub version 1.0.0.0 uses initial-release metadata; development history remains in the repository changelog.
- Added a 512×512 BC1 DDS with no mipmaps. This is a **code-drawn placeholder created during this AI-assisted task**, not certified human-created submission art. Original desktop artwork is unchanged.
- Removed the standalone LICENSE entry from the mod ZIP. Its full text and upstream attribution are embedded in a comment in the loaded Lua source, so standalone mod downloads retain the license.
- Replaced protected calls with availability checks, valid-node checks and normal I/O return handling. Unexpected engine/API failures are no longer swallowed. The companion stops on stale telemetry.
- Replaced player-system lookup with the documented local-player vehicle method using its instance.
- Enabled multiplayer loading with local-player-only telemetry, no network events and no pipe access on dedicated servers. This is implemented client isolation, not a claim of completed multiplayer playtesting.
- Pipe open retries when the companion is absent. Write/flush failures close the connection, retry after a delay and report bounded warnings.

## Validation
- [Original FAIL](TestRunner-FAIL.xml) preserved; [final PASS XML](TestRunner-PASS.xml), [HTML](TestRunner-PASS.html), and [log](TestRunner-PASS.log).
- 14 executable Lua tests with simulated FS25 and I/O: local player/method lookup; velocity; server transition; dedicated-server isolation; no local player; pause/menu; absent companion; write failure; reconnect; flush failure; invalid node; visible unexpected error; map unload.
- 144 companion software checks and compact UI checks passed. No hardware force/rumble was sent.
- tests/Test-Mod.py runs with Python and lupa; this session used isolated lupa 2.8 in TEMP.
- No geometry/editor checks were exercised because this script-only mod contains no I3D files.

## Remaining before submission
- Replace or have GIANTS approve the placeholder icon; passing DDS checks does not establish compliance with the ban on AI-created imagery.
- Prepare three genuine in-game submission screenshots.
- Test actual single-player, multiplayer host/client and dedicated-server behavior, reconnect after companion restart, and supported vehicles.
- GIANTS must assess the external companion dependency and final submission.
- No mod installed into the game's mods folder, no save changed, no upload or Git push performed in this fix pass.

References: [ModHub guidelines](https://forum.giants-software.com/viewtopic.php?t=209169); [GIANTS VehicleSystem local-player usage](https://gdn.giants-software.com/documentation_scripting_fs25.php?category=91&class=897&version=engine).

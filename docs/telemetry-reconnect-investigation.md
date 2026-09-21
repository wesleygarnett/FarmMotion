# Telemetry reconnect investigation

## Findings

The mod retains one write-only pipe handle until a write/flush explicitly returns false or an error. Mod 1.0.0.1 accepts nil without an error because treating that as failure caused the earlier constant-disconnect issue; the user confirmed that hotfix restored gameplay telemetry. If the engine IO wrapper also suppresses failure returns, the mod cannot distinguish successful delivery from a broken pipe.

The updater waits for the old process to exit and then launches the new app. Restarting replaces the receiving pipe instance. A surviving game-side handle cannot reconnect itself to the new receiver.

## Isolated evidence

- diagnostics/Test-ReceiverRestart.ps1 loads the actual dynamic-response receiver on a random test pipe. One packet arrived before restart. A retained client handle failed with Pipe is broken after the old pending read completed. The replacement receiver had zero packets until the client closed and reopened; delivery then resumed.
- diagnostics/Test-SilentDisconnect.py uses the actual mod in mocked Lua. Silent failed writes left the original connection held for 16 simulated seconds with no reopen. An explicit error triggered retry/reopen. All 17 mod checks passed.
- Existing receiver regression tests reconnect a client to the same server; they did not cover retaining a producer handle across an app restart.

This demonstrates the mechanism but does not prove how GIANTS returns errors on an actual disconnected handle. No live app/game restart or hardware output was triggered for this investigation. The inspected game log had already ended and supplied no useful reconnect error.

## Recommended fix

Add an out-of-band app instance token readable by the mod (a tiny file in the game profile, using GIANTS-supported file IO). Publish a fresh token once the receiver starts. The mod checks it at a low rate and closes/reopens its pipe only when a new token is observed, even if its old writes appear successful. Keep existing explicit-error retries and stale-force cutoff. Initialize the token before the first connection so a normal initial read does not cause needless reconnect. Ignore partial/missing reads and write the token atomically; do not periodically recycle healthy pipes.

Before implementation, verify the profile path and read semantics in FS25. Test startup ordering, app restart with a held handle, missing/partial marker, unchanged marker without gaps, pause/resume, and update rollback. Then validate one actual app restart with FS25 left running. This change needs a mod update as well as an app update.

## Implemented in 0.4.0 / mod 1.0.0.2

A per-receiver token is now published atomically once before listening. The mod polls the bounded token and reconnects on a change. Healthy client reconnects retain the token to avoid feedback loops. Missing/partial markers are ignored, first late-arriving markers reopen an already-held connection, and marker publication errors appear in the UI. Custom profiles use FARMMOTION_FS25_PROFILE. Real Windows receiver restart tests and mocked Lua silent-failure tests pass; actual game restart recovery remains to be confirmed.

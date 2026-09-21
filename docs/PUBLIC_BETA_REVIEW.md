# Public beta readiness: 0.4.0

Repository remains private. Candidate includes mod 1.0.0.2; no live app or game replacement was performed.

## Automated checks

156 hardware-free app checks including actual Windows pipe instance replacement, token stability and recovery; 22 simulated Lua tests including silent IO errors, marker changes, unchanged/missing/partial markers; compact mock WPF UI checks; all 15 GIANTS TestRunner modules passed. GIANTS file read behavior and reconnect with the game left running still need live confirmation.

## Privacy and repository review

Targeted pattern scan of 138 text blobs (including candidate changes) reachable from fetched Git refs found no matches for GitHub tokens, private keys, AWS access keys, OpenAI keys or long assigned-secret patterns. This is a limited pattern screen, not a security audit or proof that the history is secret-free. Commit author emails use GitHub noreply addresses.

Four historical TestRunner HTML/XML report blobs contain the developer's local Windows username/profile paths. Current report copies are redacted; prior commits and old source release archives still expose those paths. History was not rewritten, and existing releases were not deleted. Decide whether that exposure is acceptable before making the repository public. All nine available GitHub Actions logs were screened with the same patterns. No credential-pattern matches were found; path matches contained only the standard hosted runner account.

Binary packages include end-user docs only; development diagnostics and old reports remain source documentation. Licenses and dependency notices are retained. Security documentation now explains GitHub updates, enabled-by-default output and restart marker behavior. Published README download URLs target v0.4.0 and require that release before public launch.

## Remaining manual acceptance

1. Install candidate app and mod with FS25 closed once.
2. Load a save, confirm telemetry, close/reopen the app while keeping FS25 running; confirm recovery without restarting the game. Repeat with a pause between app sessions.
3. Confirm gentle defaults on a fresh settings profile, output disable and device disconnect behavior, and the new curve using actual driving.
4. Confirm any wheel/controller combinations advertised as tested; treat other devices and multiplayer as unverified.
5. Resolve or accept historical path exposure, then publish a prerelease and explicitly approve making the repository public.

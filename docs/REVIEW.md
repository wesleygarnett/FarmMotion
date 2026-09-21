# Release review: 0.1.0

This review records the 0.1.0 release. See [0.3.0 WPF UI validation](DEVELOPMENT_0.3.0.md) for the current interface migration. See [0.2.0 development validation](DEVELOPMENT_0.2.0.md) for the subsequent controller, solo and lifecycle features and their remaining acceptance work.

Scope: C# companion, DirectInput interop, Lua exporter, settings/recordings, build scripts and release packaging. This is a source review plus automated software validation, not an independent security audit or hardware certification.

## Changes made

| Area | Finding | Release change |
|---|---|---|
| Security | Named pipe relied on Windows default access rules. | Explicit current-user ACL, inherited permissions removed, network tokens denied. Real same-user transport/reconnect tests pass. |
| Security | Settings reads had no size/nesting bound. | 64 KiB limit, bounded JSON depth, value validation. |
| Reliability | An interrupted settings write could truncate saved tuning. | Write to a unique sibling file, then atomically replace the previous file. |
| Input handling | Vehicle/session text could consume most of a packet and expand UI labels. | Limit identity fields to 128 characters; retain 16 KiB packet and finite-number checks. |
| Performance | Every output update opened a Process object to check foreground ownership. | Check foreground PID each update, cache process classification for at most one second, dispose Process wrappers. |
| Performance | Every force sample allocated a graph array; every paint copied the history. | Value-type queue and fixed 1,000-frame drawing ring; repaint once per UI tick. |
| Performance | Recording performed disk writes while holding the feedback lock. | Background writer with a 2,048-frame queue; a full queue reports failure instead of unbounded memory growth. |
| Reconnect | Lua treated a nil write/flush result without an error string as success. | Treat every nil result as failure and retry the connection. Live restart recovery remains unverified. |
| Distribution | Development paths, personal checkpoints and manual packaging made redistribution fragile. | Portable allowlisted package, embedded icon, version metadata, source archive, checksums, clean documentation and ignored local notes. |
| CI | No repeatable remote validation. | Windows build/test/package workflow, read-only token and SHA-pinned actions. |

The local pipe policy follows [Microsoft's named-pipe guidance](https://learn.microsoft.com/en-us/windows/win32/ipc/named-pipes). CI permissions/pinning follow [GitHub's secure-use guidance](https://docs.github.com/en/actions/reference/security/secure-use).

## Checks performed

- 66 hardware-free checks: current-user/network-deny ACL, bounded settings and telemetry, atomic saves, asynchronous recording order/flush, real named-pipe transport/reconnection, malformed packets, stale/inactive cutoff, output caps and road signal behavior.
- Frozen V1 comparison across 4,000 force samples remains exact.
- Dashboard checks: Basic/Advanced visibility and collapse preserving tuning, no-device placeholder, comparison controls, separate road pitch, PerMonitorV2 awareness.
- Normal, Advanced and compact dashboard screenshots inspected at 144 DPI (150%). The new FM icon is embedded in both executables and visible in the window title bar.
- Compilation treats warnings as errors. Release contents use an explicit allowlist; local checkpoints, recordings, device IDs and session notes are excluded from Git and binary packages.

## Remaining limitations

1. **Physical force output is not certified.** Software tests confirm command limits, not actual motor torque, firmware expiry or emergency-stop behavior. The DirectInput effect requests a 50 ms lifetime; telemetry fades to zero by 150 ms and software output is clamped to 10% full-scale.
2. **Windows timing is not real-time.** The loop requests a 2 ms sleep and 1 ms timer resolution. Prior live observations showed substantially lower rates. The allocation/process changes remove overhead but do not prove a sustained 500 Hz loop or physical 50–90 Hz rumble. Measure while FS25 is foreground and output armed during a supervised drive.
3. **The game exporter writes synchronously.** A blocked reader can stall a Lua pipe write. A same-user malicious process can spoof telemetry or interfere with the pipe. Current-user ACLs intentionally do not defend against software running as that same user; foreground process-name checks are a usability gate, not authentication.
4. **Restart recovery needs a live game test.** Nil write/flush handling was tightened, but companion restart while FS25 remains open previously required restarting the game. Do not claim the incident is fully resolved.
5. **Storage can still be slow.** Recording no longer writes from the force path. Stopping/closing waits up to two seconds for the writer; if storage remains blocked, an incomplete tail is possible when the process exits. Replay loading is synchronous and limited to 64 MB; use short comparison drives.
6. **Distribution is intentionally simple.** A ZIP has no auto-update, signing certificate or installer. Users must install/enable the bundled game mod and retain the executable config files. Windows may warn about unsigned downloads. .NET Framework 4.8 and a working wheel driver are external prerequisites.
7. **Hardware/game compatibility is narrow.** MOZA R3 is the initial target. Other wheels, custom vehicles/maps, multiplayer, remote sessions and extreme multi-monitor DPI transitions are unverified.

No feedback gains, saved user values, filter algorithms or road-carrier tuning were intentionally changed by this release preparation.

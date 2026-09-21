# Releasing

1. Update `VERSION`, assembly version attributes, changelog and release notes. The telemetry mod has its own version; do not lower it to match the desktop app.
2. Run `powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -RunUiTests` and inspect Basic/Advanced/compact previews.
3. Commit the source, generated icon and reviewed documentation. Run `powershell -NoProfile -ExecutionPolicy Bypass -File .\release.ps1`. It rebuilds and tests, packages only allowlisted files, exports committed source, and writes SHA-256 checksums.
4. Confirm the source archive matches the intended commit. Never publish a release built from uncommitted changes. Run `git status --short` and inspect `dist/release`.
5. Create and push the annotated version tag. Wait for the Windows build workflow to pass.
6. Create a GitHub release using `docs/releases/v0.1.0.md` (or the next version's notes) and attach the portable ZIP, mod ZIP, source ZIP and SHA256SUMS.txt. Mark previews as prereleases. Keep repository visibility private until explicitly approved otherwise.

The binaries are unsigned. Checksums detect download corruption, but are not a substitute for a trusted release source or code signing. No signing key is stored in this repository. GitHub Actions has read-only repository permissions and cannot publish releases automatically.

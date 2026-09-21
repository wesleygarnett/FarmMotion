# Security

FarmMotion is a local desktop preview. It collects no analytics and does not require administrator privileges. User-requested update checks and downloads contact GitHub over HTTPS. Downloaded packages are checked against release SHA-256 metadata and validated before installation; this is not code signing or independent publisher authentication. Output starts enabled by default and is gated by active, fresh telemetry and game focus.

Telemetry uses a Windows named pipe restricted to the current user, with network clients explicitly denied. The pipe is not an authentication boundary against malicious software already running as the same Windows user. Run the game and companion as the same ordinary user.

Telemetry and replay files are untrusted input: packet size and JSON nesting are bounded, numeric values must be finite, and final wheel force is clamped. Recordings should come from a source you trust. Do not publish settings or recordings containing personal information.

For a suspected vulnerability, use the repository's **Security → Report a vulnerability** feature when available. If unavailable on this private repository, contact the owner privately through your existing channel; do not post exploit details or sensitive logs in a public issue.

Version 0.4.x is the currently maintained preview line. No security audit or hardware certification is claimed. Known limitations and validation are documented in [docs/REVIEW.md](docs/REVIEW.md).

The companion writes a random instance token to farmMotionReceiver.txt in the FS25 profile. The mod polls this local file to detect app restarts; it contains no credentials or gameplay data. Marker failures are shown in telemetry status and leave explicit-error reconnect handling available.

# Telemetry hotfix 1.0.0.1

The live FS25 log repeatedly reported the exporter-generated `pipe disconnected` warning without an IO error. The exporter classified a nil write/flush return as a disconnect and skipped flushing after a nil write. This is consistent with a game file wrapper returning no values. The precise runtime return values have not been instrumented.

Accept no-value success, always flush after a write without an explicit failure, and retain reconnect on false or an error return. Added a 180-packet continuous-stream regression, explicit false checks, and retained error/reconnect coverage. All 17 Lua tests and all 15 GIANTS TestRunner modules passed.

Artifact: dist/telemetry-hotfix-1.0.0.1/FS25_FarmMotionTelemetry.zip. Version 1.0.0.1. Not installed or published; running game remains untouched. Requires replacement of mod and game restart for live validation.

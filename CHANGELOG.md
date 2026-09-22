# Changelog

## 1.0.0 — 2026-09-22

First stable release of **Don-Chan Telemetry Contract v1**.

### Stable boundary

- LIGHT snapshot + LIGHT event envelope
- `schemaVersion = 1`
- per-run `sessionId`
- shared monotonic `sequence`
- UTC `observedAt`
- semantic action events intended for AITuber aggregation
- AUDIT output explicitly excluded from the stable consumer contract

### Final semantic vocabulary corrections

- robotic sledge / turret actual activation: `action.deployable.fire`
- broken glass consumption: `Consumable / Hazard`
- semantic `source`: `DonChanTelemetryProbe`

### Acceptance basis

The final pre-release semantic sweep verified a continuous sequence stream and representative gameplay classes including firearms, powered tools, utility tools, consumables, robotic deployables, refuel/reload lifecycle, quests, focus, movement, combat pressure, and vehicle state.

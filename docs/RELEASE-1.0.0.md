# Don-Chan Telemetry Probe v1.0.0

This release freezes **Don-Chan Telemetry Contract v1**.

## What is stable

- LIGHT snapshot structure and omission rules
- semantic action vocabulary
- `schemaVersion/sessionId/sequence/observedAt` envelope
- separation between LIGHT contract output and AUDIT discovery output
- AITuber consumer profile: aggregate first, do not send raw event volume directly to AI

## Final changes from v0.1.13

- `action.deployable.use` renamed to `action.deployable.fire`
- `resourceBrokenGlass` classified as `Consumable / Hazard`
- semantic `source` identifies the actual producer: `DonChanTelemetryProbe`
- candidate wording removed from the contract documentation

See `docs/TELEMETRY-CONTRACT.md` for the normative contract.

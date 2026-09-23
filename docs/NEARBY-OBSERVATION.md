# Horizontal nearby observation foundation

`world.nearbyObservation` is an additive schemaVersion 1 raw observation contract. Existing `world.nearby` keeps its old meanings. Combat events are unchanged and have no distance filter.

- XZ distance <=15m, Y ignored, finite coordinates required.
- Invokes IsDead() explicitly. Dead entities are excluded. Failed/absent life read is unknown, not alive.
- Captures entity ID, class ID, runtime type and up to 12 type ancestors for classification audit.
- Classification is pending: records may contain animals outside the requested six groups. They must NOT be counted as enemies without a subsequent verified classifier.
- Target reference: player_reference / other_reference / none_observed / unknown. These are raw member observations, not verified combat states. Staleness is not yet resolved. Null member values differ from missing/failed reads. ID-only sentinel values remain unknown.
- scanComplete reports successful enumeration; recordsComplete additionally requires no unknown life/position readings or output omissions. Up to128 nearby records retained, omissions counted. Zero records is not safe/all-clear, especially with incomplete records or pending classification.
- Parent snapshot supplies timestamp/session. Individual records do not claim persistent spawn-generation identity.

This is stage 1, not a ready-to-install release. Six-group classification, target semantics, shared radar classifier and AITuber consumption are pending. No bundled DLL was rebuilt. Pure C# tests use synthetic objects and verify boundaries, death exclusion, unknown readings and incomplete scans; they do not verify game API behavior or full MOD build.

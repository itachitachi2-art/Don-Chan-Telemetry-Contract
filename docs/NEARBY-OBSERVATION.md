# Horizontal nearby observation foundation

`world.nearbyObservation` is an additive schemaVersion 1 raw observation contract. Existing `world.nearby` keeps its old meanings. Combat events are unchanged and have no distance filter.

- XZ distance <=15m, Y ignored, finite coordinates required.
- Invokes IsDead() explicitly. Dead entities are excluded. Failed/absent life read is unknown, not alive.
- Captures entity ID, class ID, runtime type and up to 12 type ancestors for classification audit.
- Each record now has group/className from shared/NearbyTargetClassifier.cs. The selected groups are humanoid_zombie, zombie_dog, zombie_bear, vulture, dire_wolf and grace. Normal wildlife is not automatically included. Records still include excluded/unknown observations for audit; never count all records as enemies. classifiedAliveTargets counts only recognized living targets. classificationStatus is partial when reads/classification/enumeration are incomplete; a zero classified count then does not establish absence.
- Target reference: player_reference / other_reference / none_observed / unknown. These are raw member observations, not verified combat states. Staleness is not yet resolved. Null member values differ from missing/failed reads. ID-only sentinel values remain unknown.
- scanComplete reports successful enumeration; recordsComplete additionally requires no unknown life/position readings or output omissions. Up to128 nearby records retained, omissions counted. Zero records is not safe/all-clear, especially with incomplete records or pending classification.
- Parent snapshot supplies timestamp/session. Individual records do not claim persistent spawn-generation identity.

This is stage 1, not a ready-to-install release. The six-group classifier is shared with radar source. Exact runtime EntityClass.list / entityClassName binding and coverage on the installed game version, target semantics and AITuber consumption remain unverified. No bundled DLL was rebuilt. Pure C# tests use synthetic objects and verify boundaries, death exclusion, unknown readings and incomplete scans; they do not verify game API behavior or full MOD build.

## Classifier evidence and limits

Exact animal identifiers were cross-checked against an exported entityclasses.xml (wink-/7DTDConfigs, blob b9eaadc271e35c04a46659549c12723cb72ae824). This is reference evidence, NOT the user installed game version. Normal bear/wolf/boar inheritance shows why type-name matching alone cannot distinguish zombie bear/dire wolf/Grace. No arbitrary prefix matching is used. EntityZombieDog and EntityVulture exact runtime ancestry are considered before EntityZombie. Unknown variants remain unknown; six-group coverage is not claimed runtime-verified.

Class name resolution reads the runtime EntityClass registry via reflection. Missing/changed members return unknown. Both MOD source builds include the SAME shared classifier file. Radar source must be built from the full repository checkout; copying only its directory omits the shared source. Existing prebuilt radar DLL remains the original 0.1.3 and does not contain this change. No ready-to-install package is released by this PR.

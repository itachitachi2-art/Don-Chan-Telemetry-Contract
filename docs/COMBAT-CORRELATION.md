# Combat correlation v3

DamageEntity prefix opens a session-scoped, thread-local damageActionId. Its nested synchronous death/kill/response callbacks and final damage record carry the same ID. Nested DamageEntity calls get their own ID and restore their parent in a Harmony finalizer; the original exception is preserved. A hit callback outside this scope has no ID. Do not infer a common ID by timestamp.

Consumers may join death and fatal response only on session, target entityId and damageActionId. Repeated corpse Kill calls without a new death do not imply another kill. Missing IDs (v1/v2), unknown owner IDs, and unmatched records retain unknown attribution. Damage and response values are not exact HP deltas or shot counts. Thread-scoped linkage is limited to synchronous callbacks; asynchronous callbacks remain unlinked.

Diagnostic markers now explicitly carry kind=diagnostic, matching the HTTP event contract. File validation can be disabled for normal use; damage IDs and normal combat.response remain enabled.

Full DLL build against supplied game assemblies passed. New correlation IDs require runtime validation; prior v2 logs verify body-part/classification behavior but cannot validate v3 correlation.

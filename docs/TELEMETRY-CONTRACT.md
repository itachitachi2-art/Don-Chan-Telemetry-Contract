# Don-Chan Telemetry Contract v1

Status: **stable / frozen for v1 consumers**  
Producer release: **Don-Chan Telemetry Probe v1.0.0**  
Target: **7 Days to Die V3.x**

## 1. Purpose

Don-Chan Telemetry Contract v1 is the stable boundary between the 7DTD telemetry producer and downstream consumers such as AITuber.

The contract intentionally separates:

- **game facts** produced by the mod,
- **aggregation / interpretation** performed by the application,
- **natural-language generation** performed later by AI.

Consumers should not need to know 7DTD internal class layouts to use the v1 feed.

## 2. Modes

### LIGHT — default / contract mode

`AuditMode=false`

Produces:

- `session.json`
- `snapshots.jsonl`
- `events.jsonl`
- `snapshots/latest.json`
- `capabilities.jsonl`

Only LIGHT records are part of the long-running v1 feed.

### AUDIT — discovery mode

`AuditMode=true`

Adds:

- `catalog/*`
- `audit/events.raw.jsonl`
- `audit/snapshots.jsonl`

AUDIT output is diagnostic and **not** part of the stable v1 consumer contract.

## 3. Common envelope

Every LIGHT snapshot and LIGHT event contains:

```json
{
  "schemaVersion": 1,
  "sessionId": "32-hex-guid",
  "sequence": 1234,
  "observedAt": "2026-09-22T11:53:12.8333684Z"
}
```

### Semantics

- `schemaVersion`
  - integer contract schema version.
  - v1 consumers accept `1`.
- `sessionId`
  - regenerated when the mod initializes.
  - records from different sessions must not be merged as one continuous timeline.
- `sequence`
  - monotonically increasing within one session.
  - shared by LIGHT snapshots and LIGHT events.
  - consumers can detect gaps, duplicates, and out-of-order delivery.
- `observedAt`
  - UTC observation time.
  - normally derived from the record's original `utc` timestamp.

The producer may include additional fields. A v1 consumer must ignore unknown fields unless explicitly configured otherwise.

## 4. Session metadata

`session.json` identifies the current producer session and runtime:

```json
{
  "schemaVersion": 1,
  "sessionId": "...",
  "startedUtc": "...",
  "modName": "DonChanTelemetryProbe",
  "mode": "light",
  "assemblyCSharp": "...",
  "probeAssembly": "..."
}
```

`session.json` itself is metadata and is not part of the shared `sequence` stream.

## 5. Snapshot v1

LIGHT snapshots are compact state observations. Fields may be omitted when the current game build does not expose or populate them.

Top-level shape:

```json
{
  "worldLoaded": true,
  "playerLoaded": true,
  "player": {},
  "item": {},
  "weapon": {},
  "focus": {},
  "movement": {},
  "vehicle": {},
  "status": {},
  "quest": {},
  "world": {},
  "schemaVersion": 1,
  "sessionId": "...",
  "sequence": 1234,
  "observedAt": "..."
}
```

### 5.1 Player

Representative fields:

- entity ID
- position / rotation
- aiming / crouching
- game stage
- core temperature
- HP / stamina / food / water
- level / skill points / XP
- biome

### 5.2 Held item

Representative fields:

- internal item name
- localized name
- quality
- durability
- selected slot
- stack count
- display type / tech tier

### 5.3 Weapon / powered resource state

Firearms and ranged weapons may expose:

```json
{
  "kind": "ranged",
  "loaded": 27,
  "magazineSize": 60,
  "ammoItem": "ammo762mmBulletBall",
  "reserve": 341,
  "reloading": false
}
```

Powered tools are normalized separately:

```json
{
  "kind": "poweredTool",
  "fuel": {
    "loaded": 941,
    "capacity": 944,
    "item": "ammoGasCan",
    "reserve": 8237
  }
}
```

### 5.4 Focus

The current hit/focus target may be normalized as:

- `kind = entity`
- `kind = block`

Entity focus can include entity identity and distance.

Block focus can include:

- block name / ID
- material
- position
- maximum HP
- accumulated damage
- remaining HP
- distance

### 5.5 Movement

The raw game run flag is preserved as `runModeActive`.

Derived fields:

- `moving`
- `walking`
- `running`
- `jumping`
- `inAir`
- `onGround`
- `mounted`
- `locomotion`

`locomotion` values:

```text
idle | walking | running | crouching | airborne | mounted | moving
```

The v1 producer does not equate `runModeActive=true` with actual movement.

### 5.6 Vehicle

When mounted, the snapshot may include:

- vehicle subtype / runtime type
- speed
- engine state
- driver state
- fuel
- health
- storage availability

### 5.7 Important status

`status.important` is conservative and semantic.

Candidate flags include:

- bleeding
- infected
- brokenBone
- sprained
- stunned
- burning
- encumbered
- concussion
- laceration
- abrasion
- dysentery
- dehydrated
- hungry
- hot
- cold

`stunned` uses a whitelist of known stun buff IDs. Substring matching is not permitted because stamina-related buff names can contain `stunt/stun` without representing player stun.

### 5.8 Quest

The tracked/active quest may include:

- quest code
- state / phase
- tracked / active
- POI name / position
- distance in meters
- active objective count
- compact objective list

### 5.9 Nearby entities / combat pressure

`world.nearby` may include:

- loaded living entities
- loaded zombies
- zombies within 5 / 10 / 15 / 20 / 30 meters
- zombies targeting the local player
- nearest zombie 3D distance
- nearest zombie horizontal distance
- loaded animals / drones / vehicles / turrets

Horizontal distance intentionally ignores Y difference.

## 6. Event tiers

`events.jsonl` contains several event tiers.

### 6.1 Semantic events — primary commentary input

`kind = "semantic"`

These are the preferred events for AITuber aggregation.

For v1, semantic records use:

```json
{
  "kind": "semantic",
  "event": "action.ranged.fire",
  "source": "DonChanTelemetryProbe",
  "data": {
    "topic": "Ranged",
    "subtype": "Rifle",
    "itemName": "gunM60"
  }
}
```

### 6.2 Normalized delegate / Harmony events — supporting evidence

`kind = "delegate"` or `kind = "harmony"`

Examples:

- harvest
- block changes
- inventory changes
- combat hit / damage / kill / death
- buff add / remove
- entity load / unload

These are useful for aggregation and diagnostics but should not be forwarded wholesale to the AI.

## 7. Stable semantic action vocabulary

### 7.1 Consumables

Event:

```text
action.consume.complete
```

Topic:

```text
Consumable
```

Subtypes:

```text
Food | Drink | Medicine | Boost | Hazard | Generic
```

`resourceBrokenGlass` is normalized as `Hazard`.

### 7.2 Melee weapons

Event:

```text
action.melee
```

Subtypes include:

```text
Knife | Spear | Sledge | Baton | Fist | Club | Generic
```

Utility tools are not emitted as melee merely because they inherit a melee action implementation.

### 7.3 Hand tools

Event:

```text
action.tool.use
```

Subtypes include:

```text
Axe | Pickaxe | Shovel | Wrench | Ratchet | ImpactDriver | Hammer | Torch | Nailgun | Generic
```

### 7.4 Powered tools

Event:

```text
action.poweredTool.use
```

Subtypes:

```text
Auger | Chainsaw | Generic
```

Fuel lifecycle:

```text
action.refuel.request
action.refuel.complete
action.refuel.cancel
```

A cancel event is emitted only when a matching request was observed first for the same action data.

### 7.5 Ranged fire

Event:

```text
action.ranged.fire
```

The event is emitted from the actual fired callback, not merely from button input.

Subtypes include:

```text
Pistol | Magnum | DesertVulture | Rifle | Shotgun | SMG |
Bow | Crossbow | Launcher | Nailgun | Generic
```

Reload lifecycle:

```text
action.reload.request
action.reload.complete
action.reload.cancel
```

A cancel event is emitted only after a matching request was observed.

### 7.6 Robotic deployables

Event:

```text
action.deployable.fire
```

Subtypes:

```text
RoboticSledge | RoboticTurret | Generic
```

This name intentionally uses `fire`, not `use`: the hook is the actual holding-entity fired callback, so the event represents activation/firing rather than placement.

Robotic turret reloads use the normal `action.reload.*` lifecycle with topic `Deployable`.

### 7.7 Craft

```text
action.craft.complete
```

Emitted only when the recipe output operation reports success.

### 7.8 Vehicle

```text
action.vehicle.enter
```

Subtypes:

```text
Bicycle | Minibike | Motorcycle | Jeep | Gyrocopter | Generic
```

Emitted only after the local player is actually attached to the vehicle.

## 8. Consumer profile for AITuber

The v1 commentary consumer should primarily read:

1. LIGHT snapshots
2. semantic events
3. selected low-level combat / harvest / block evidence only when aggregation requires it

Do **not** stream every raw event directly to the AI.

Recommended pipeline:

```text
7DTD
  ↓
Don-Chan Telemetry Contract v1
  ↓
AITuber input adapter
  ↓
short-term event/snapshot store
  ↓
aggregation / deltas / episode state
  ↓
existing game policy / repetition control / persona generation
  ↓
speech
```

## 9. Omission and uncertainty rules

- Missing fields mean unavailable/not populated; consumers must not invent values.
- `null`, omitted, and `0` are not interchangeable.
- Audit-only reflection fields are not stable API.
- Game-update breakage should result in omitted fields or capability failure logs, not fabricated contract values.
- Semantic normalization may evolve only through a new schema version when the change would alter v1 meaning.

## 10. Compatibility rules

A v1 consumer:

- accepts `schemaVersion = 1`
- rejects or explicitly quarantines unsupported schema versions
- partitions state by `sessionId`
- uses `sequence` for ordering/deduplication
- ignores unknown additive fields
- does not rely on AUDIT output
- does not rely on private 7DTD member names

## 11. v1 acceptance evidence

The pre-release v0.1.13 full semantic sweep produced:

- 294 LIGHT snapshots
- 1,749 LIGHT events
- a combined `sequence` stream from 1 through 2,043
- zero sequence gaps
- zero duplicate sequence values
- one consistent session ID

Verified semantic cases included:

- Auger -> `PoweredTool / Auger`
- Auger refuel -> request -> complete
- Tactical AR -> `Ranged / Rifle`
- Wrench -> `Tool / Wrench`
- Antibiotics -> `Consumable / Medicine`
- Hackers / Fort Bites -> `Consumable / Boost`
- canned/meal food -> `Consumable / Food`
- Robotic Turret -> `Deployable / RoboticTurret`
- ranged/deployable reload request/complete/cancel lifecycle

The two final v1 vocabulary corrections are:

- `action.deployable.use` -> `action.deployable.fire`
- `resourceBrokenGlass` -> `Consumable / Hazard`

These corrections do not alter the envelope or sequence model.

## 12. Versioning

The wire schema is identified by `schemaVersion`.

- Probe v1.0.0 ships **Don-Chan Telemetry Contract v1**
- additive optional fields may be introduced without changing `schemaVersion`
- removal, incompatible renaming, or semantic reinterpretation requires a new schema version

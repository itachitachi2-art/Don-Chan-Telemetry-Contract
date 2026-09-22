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

- `schemaVersion`: integer contract schema version.
- `sessionId`: regenerated when the mod initializes.
- `sequence`: monotonically increasing within one session and shared by LIGHT snapshots/events.
- `observedAt`: UTC observation time.

## 4. Session metadata

`session.json` identifies the current producer session and runtime and is not part of the shared `sequence` stream.

## 5. Snapshot v1

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

Representative fields: entity ID, position/rotation, aiming/crouching, game stage, core temperature, HP/stamina/food/water, level/skill points/XP, biome.

### 5.2 Held item

Representative fields: internal/localized name, quality, durability, selected slot, stack count, display type / tech tier.

### 5.3 Weapon / powered resource state

Firearms/ranged weapons may expose:

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

The current hit/focus target may be normalized as `entity` or `block`. Block focus can include block name/ID, material, position, maximum HP, accumulated damage, remaining HP, and distance.

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

Values:

```text
idle | walking | running | crouching | airborne | mounted | moving
```

### 5.6 Vehicle

When mounted, the snapshot may include vehicle subtype/type, speed, engine/driver state, fuel, health, and storage availability.

### 5.7 Important status

`status.important` is conservative and semantic.

Candidate flags include bleeding, infected, brokenBone, sprained, stunned, burning, encumbered, concussion, laceration, abrasion, dysentery, dehydrated, hungry, hot, cold.

`stunned` uses a whitelist of known stun buff IDs. Substring matching is not permitted.

### 5.8 Quest

Tracked/active quest may include quest code, state/phase, tracked/active, POI name/position, distance in meters, active objective count, and compact objective list.

### 5.9 Nearby entities / combat pressure

`world.nearby` may include loaded living entities/zombies, zombies within 5/10/15/20/30 m, zombies targeting the player, nearest zombie 3D/horizontal distance, and loaded animals/drones/vehicles/turrets.

Horizontal distance intentionally ignores Y difference.

## 6. Event tiers

### 6.1 Semantic events — primary commentary input

`kind = "semantic"`

Example:

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

Examples include harvest, block/inventory changes, combat hit/damage/kill/death, buff add/remove, entity load/unload. These are useful for aggregation/diagnostics but should not be forwarded wholesale to the AI.

## 7. Stable semantic action vocabulary

### 7.1 Consumables

`action.consume.complete`

Topic: `Consumable`

Subtypes:

```text
Food | Drink | Medicine | Boost | Hazard | Generic
```

`resourceBrokenGlass` is normalized as `Hazard`.

### 7.2 Melee weapons

`action.melee`

```text
Knife | Spear | Sledge | Baton | Fist | Club | Generic
```

### 7.3 Hand tools

`action.tool.use`

```text
Axe | Pickaxe | Shovel | Wrench | Ratchet | ImpactDriver | Hammer | Torch | Nailgun | Generic
```

### 7.4 Powered tools

`action.poweredTool.use`

```text
Auger | Chainsaw | Generic
```

Fuel lifecycle:

```text
action.refuel.request
action.refuel.complete
action.refuel.cancel
```

### 7.5 Ranged fire

`action.ranged.fire`

Subtypes:

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

### 7.6 Robotic deployables

`action.deployable.fire`

```text
RoboticSledge | RoboticTurret | Generic
```

Robotic turret reloads use the normal `action.reload.*` lifecycle with topic `Deployable`.

### 7.7 Craft

`action.craft.complete`

### 7.8 Vehicle

`action.vehicle.enter`

```text
Bicycle | Minibike | Motorcycle | Jeep | Gyrocopter | Generic
```

## 8. Consumer profile for AITuber

The v1 commentary consumer should primarily read:

1. LIGHT snapshots
2. semantic events
3. selected low-level combat / harvest / block evidence only when aggregation requires it

Do **not** stream every raw event directly to the AI.

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
- Semantic reinterpretation that changes v1 meaning requires a new schema version.

## 10. Compatibility rules

A v1 consumer:

- accepts `schemaVersion = 1`
- rejects/quarantines unsupported schema versions
- partitions state by `sessionId`
- uses `sequence` for ordering/deduplication
- ignores unknown additive fields
- does not rely on AUDIT output
- does not rely on private 7DTD member names

## 11. v1 acceptance evidence

The pre-release v0.1.13 full semantic sweep produced:

- 294 LIGHT snapshots
- 1,749 LIGHT events
- combined `sequence` stream 1 through 2,043
- zero sequence gaps
- zero duplicate sequence values
- one consistent session ID

Verified semantic cases included Auger, refuel, Tactical AR, Wrench, Medicine/Boost/Food, Robotic Turret, and reload/deployable lifecycles.

Final v1 vocabulary corrections:

- `action.deployable.use` -> `action.deployable.fire`
- `resourceBrokenGlass` -> `Consumable / Hazard`

## 12. Versioning

- Probe v1.0.0 ships **Don-Chan Telemetry Contract v1**
- additive optional fields may be introduced without changing `schemaVersion`
- removal, incompatible renaming, or semantic reinterpretation requires a new schema version

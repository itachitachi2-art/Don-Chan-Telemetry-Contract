# Don-Chan Telemetry Contract v1 candidate (v0.1.13)

## Purpose

Long-running gameplay telemetry for commentary/automation must be small, stable, and explicit. Reflection dumps are for discovery only.

## Modes

### LIGHT (default)

- `AuditMode=false`
- `snapshots.jsonl`: compact state, default 1 Hz
- `events.jsonl`: compact normalized events
- no assembly catalog
- no deep recursive object serialization

### AUDIT

- `AuditMode=true`
- keeps all LIGHT outputs
- adds `catalog/*`
- adds `audit/events.raw.jsonl`
- adds `audit/snapshots.jsonl`

Use AUDIT after a 7DTD update or when discovering a new parameter. Do not use it as a permanent streaming format.

## Snapshot v1

Representative shape:

```json
{
  "utc": "...",
  "worldLoaded": true,
  "playerLoaded": true,
  "player": {
    "entityId": 171,
    "position": {"x": -360.2, "y": 39.1, "z": -748.7},
    "rotation": {"x": 0, "y": 91.2, "z": 0},
    "aiming": false,
    "crouching": false,
    "gameStage": 123,
    "vitals": {
      "health": {"value": 300, "max": 300, "percent": 1.0},
      "stamina": {"value": 280, "max": 280, "percent": 1.0},
      "food": {"value": 200, "max": 200, "percent": 1.0},
      "water": {"value": 200, "max": 200, "percent": 1.0}
    },
    "progression": {"level": 189, "skillPoints": 5, "expToNextLevel": 149000},
    "biome": {"name": "forest", "localizedName": "..."}
  },
  "item": {
    "quality": 6,
    "durabilityPercent": 0.658,
    "class": {
      "name": "meleeWpnSledgeT3SteelSledgehammer",
      "localizedName": "鉄鋼製スレッジハンマー",
      "displayType": "meleeHeavy"
    }
  },
  "world": {
    "worldTime": 951278,
    "day": 49,
    "bloodMoon": false,
    "nearby": {
      "zombiesLoaded": 5,
      "zombiesWithin10m": 1,
      "zombiesWithin20m": 3,
      "zombiesWithin30m": 4,
      "nearestZombieDistance": 3.42
    }
  }
}
```

Fields are omitted when the current game build does not expose or populate them.

## Normalized event v1

Every line contains:

```json
{
  "utc": "...",
  "kind": "delegate|harmony",
  "event": "...",
  "data": {}
}
```

Known events receive semantic summaries:

- `QuestEventManager.HarvestItem` -> tool, harvested stack, block
- `QuestEventManager.BlockDestroy/Place/Change/Upgrade` -> block, position
- `QuestEventManager.AddItem/CraftItem/RepairItem/ScrapItem` -> item/stack
- `QuestEventManager.EntityKill` -> actor, target
- Entity load/unload/spawn/despawn -> entity
- `combat.hit` -> block/entity hit, damage, harvest flag, critical flag, material
- `combat.damage` -> target, amount, damage source/type
- `combat.kill` -> target and compact `DamageResponse`
- `combat.death` -> target
- `buff.add/remove` -> buff name and owner when available

Unknown events fall back to shallow type/scalar summaries instead of recursive serialization.

## Semantic action events (v0.1.9)

These high-confidence action events are adapted from the verified DonChanActionTheater 0.5.0 hooks:

- `action.consume.complete` -> `Food` or `Healing`, emitted after `ItemActionEat.consume` completes
- `action.melee` -> melee subtype, emitted for local-player `ItemActionMelee` / `ItemActionDynamicMelee` press
- `action.ranged.fire` -> ranged subtype, emitted from `ItemActionRanged.onHoldingEntityFired` only after an actual shot
- `action.craft.complete` -> emitted only when `XUiC_RecipeStack.outputStack` returns success
- `action.vehicle.enter` -> vehicle subtype, emitted only after the local player is actually attached to the vehicle

Known subtypes mirror Action Theater: Knife, Spear, Sledge, Baton, Axe, Pickaxe, Shovel, Chainsaw, Fist, Club, Pistol, Magnum, DesertVulture, Rifle, Shotgun, SMG, Bow, Crossbow, Launcher, Bicycle, Minibike, Motorcycle, Jeep, Gyrocopter, and Generic.

Semantic events use `kind: "semantic"` and include `source: "DonChanActionTheater"`. They complement rather than replace the lower-level delegate/Harmony events.

## Stability rule

The JSON contract is intentionally independent from most internal field names. Reflection is used only inside the mod to adapt the current runtime object into this contract.


## Pre-Contract gameplay expansion (v0.1.10)

Before freezing the wire-level Don-Chan Telemetry Contract v1, LIGHT snapshots gained six compact context groups. These are candidate inputs; the transport contract may rename or further normalize them.

```json
{
  "weapon": {
    "kind": "ranged",
    "loaded": 27,
    "magazineSize": 60,
    "ammoItem": "ammo762mmBulletBall",
    "reserve": 341,
    "reloading": false
  },
  "focus": {
    "kind": "entity",
    "distanceMeters": 4.2,
    "entity": { "type": "EntityZombie", "entityId": 123 }
  },
  "movement": { "speedMetersPerSecond": 4.7, "running": true, "mounted": false },
  "vehicle": null,
  "status": { "important": { "bleeding": true }, "matchedBuffs": ["buffInjuryBleeding"] },
  "quest": { "questCode": 42, "tracked": true, "distanceMeters": 81.3 }
}
```

`world.nearby` additionally includes `zombiesWithin5m`, `zombiesWithin15m`, `zombiesTargetingPlayer`, and `nearestZombieHorizontalDistance`.

Reload lifecycle is also available as normalized semantic events:

- `action.reload.request`
- `action.reload.complete`
- `action.reload.cancel`

The current implementation uses the V3.2 members discovered by the audit catalog (for example `EntityPlayerLocal.HitInfo`, `ItemActionRanged.GetMaxAmmoCount`, ranged action-data reload flags, `QuestJournal.TrackedQuest`, and vehicle fuel/health methods). When a member is unavailable after a game update, the field is omitted rather than fabricated.


## v0.1.11 contract envelope

Every light event and snapshot now carries a transport-neutral envelope:

```json
{
  "schemaVersion": 1,
  "sessionId": "32-hex-guid",
  "sequence": 1234,
  "observedAt": "2026-09-22T11:00:58.1234567Z"
}
```

- `sessionId` is regenerated when the mod initializes.
- `sequence` is monotonic inside one mod session and shared by light events/snapshots.
- `observedAt` reuses the record's original `utc` when available.
- Existing payload fields remain intact for compatibility.

### Movement semantics

The raw game running flag is no longer exposed as `running` by itself. It is retained as `runModeActive`. Derived fields are:

- `moving`: speed >= 0.15 m/s
- `running`: moving + run mode + grounded/on-foot/non-crouched
- `walking`: moving + non-run mode + grounded/on-foot/non-crouched
- `locomotion`: `idle | walking | running | crouching | airborne | mounted | moving`

### Powered tools

Auger/chainsaw-style gas-consuming actions are normalized as `weapon.kind = poweredTool`. Their resource state is emitted under `weapon.fuel` (`loaded`, `capacity`, `reserve`, `item`) rather than pretending it is firearm ammunition.

### Important-status policy

Important status normalization is intentionally conservative. `stunned` no longer uses substring matching. Only known stun buff IDs (`buffStunned`, `buffStun`, `buffInjuryStunned1`, `buffInjuryStunned2`) are accepted. This prevents stamina-related buffs such as `buffPowerAttackStaminaStunt` from creating a false player stun.


## v0.1.12 semantic event correction

- firearm shot: `action.ranged.fire`
- auger / motor tool use: `action.poweredTool.use`
- chainsaw use: `action.melee` + subtype `Chainsaw`
- firearm reload lifecycle: `action.reload.*`
- powered tool refuel lifecycle: `action.refuel.*`

`reload.cancel` / `refuel.cancel` は、同一 action-data について `request` が先に観測された場合のみ発行する。


## Semantic event normalization candidate (v0.1.13)

Before Contract v1 freeze, item-action hooks are normalized by gameplay meaning rather than inheritance path:

- `action.melee` — melee weapons (`meleeWpn*`)
- `action.tool.use` — hand utility tools (`meleeTool*`, excluding powered tools)
- `action.poweredTool.use` — auger / chainsaw
- `action.ranged.fire` — actual projectile/weapon fire
- `action.deployable.use` — robotic sledge / robotic turret item use
- `action.consume.complete` — `Consumable` with `Food / Drink / Medicine / Boost` subtype

Low-level combat/block events remain separate evidence and may be combined by the AITuber aggregation layer.

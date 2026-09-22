# DonChanActionTheater reference used by Telemetry Probe v0.1.9

Reference repository: `itachitachi2-art/DonChanActionTheater`
Reference branch: `main`
Observed head during integration: `8301a33b1af1cbb2312fbf0d54b8b1fefa9fc9d1`
Reference files: `Scripts/ActionPatches.cs`, `Scripts/ActionEvent.cs`

## Hooks reused

| Meaning | Proven Action Theater hook | Telemetry event |
|---|---|---|
| Consumable completed | `ItemActionEat.consume` | `action.consume.complete` |
| Melee action | `ItemActionMelee.ExecuteAction` | `action.melee` |
| Dynamic melee action | `ItemActionDynamicMelee.ExecuteAction` | `action.melee` |
| Actual ranged shot | `ItemActionRanged.onHoldingEntityFired` | `action.ranged.fire` |
| Craft output success | `XUiC_RecipeStack.outputStack` | `action.craft.complete` |
| Vehicle entry success | `EntityVehicle.EnterVehicle` | `action.vehicle.enter` |

The ranged callback is deliberately used instead of input/button state because it occurs after an actual shot and therefore excludes an empty-magazine click or a shot rejected by another mod.

## Classifiers reused

### Consumable

`Healing` when the internal item name contains common medical markers such as bandage, firstaid, medical, medkit, painkiller, antibiotic, vitamin, splint, cast, steroid, aloe, or heal. Otherwise `Food`.

### Melee

`Chainsaw`, `Knife`, `Spear`, `Sledge`, `Baton`, `Shovel`, `Pickaxe`, `Axe`, `Fist`, `Club`, or `Generic`.

### Ranged

`DesertVulture`, `Magnum`, `Crossbow`, `Bow`, `Shotgun`, `SMG`, `Rifle`, `Launcher`, `Pistol`, or `Generic`.

### Vehicle

`Bicycle`, `Minibike`, `Motorcycle`, `Jeep`, `Gyrocopter`, or `Generic`.

## Telemetry-specific changes

Action Theater triggers animations. Telemetry Probe instead writes compact JSONL events and includes a compact ItemValue/Entity summary when available. The source field is set to `DonChanActionTheater` so downstream consumers can distinguish these semantic events from raw delegate/Harmony events.

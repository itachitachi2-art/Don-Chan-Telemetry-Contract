using System;
using System.Collections;
using System.Collections.Generic;

namespace DonChan.TelemetryProbe
{
    internal static class LightTelemetry
    {
        public static Dictionary<string, object> BuildSnapshot(SnapshotRoots roots)
        {
            var data = new Dictionary<string, object>();
            data["utc"] = DateTime.UtcNow;
            data["worldLoaded"] = roots != null && roots.World != null;
            data["playerLoaded"] = roots != null && roots.Player != null;

            if (roots == null || roots.Player == null)
            {
                data["player"] = null;
                data["item"] = null;
                data["world"] = roots == null ? null : WorldSummary(roots.World, null);
                return data;
            }

            data["player"] = PlayerSummary(roots.Player, roots.Stats, roots.Progression);
            data["item"] = ItemValueSummary(roots.HoldingItem, roots.Inventory);
            data["weapon"] = GameplayTelemetry.WeaponSummary(roots);
            data["focus"] = GameplayTelemetry.FocusSummary(roots.World, roots.Player);
            data["movement"] = GameplayTelemetry.MovementSummary(roots.Player);
            data["vehicle"] = GameplayTelemetry.VehicleSummary(roots.Player);
            data["status"] = GameplayTelemetry.StatusSummary(roots.Buffs);
            data["quest"] = GameplayTelemetry.QuestSummary(roots.QuestJournal, roots.Player);
            data["world"] = WorldSummary(roots.World, roots.Player);
            return data;
        }

        public static Dictionary<string, object> PlayerSummary(object player, object stats, object progression)
        {
            if (player == null) return null;
            var d = new Dictionary<string, object>();
            Put(d, "type", ReflectionUtil.TypeName(player));
            Put(d, "entityId", Read(player, "entityId", "EntityId"));
            Put(d, "name", Read(player, "LocalizedEntityName", "EntityName", "name"));
            Put(d, "position", VectorSummary(Read(player, "position", "Position")));
            Put(d, "rotation", VectorSummary(Read(player, "rotation", "Rotation")));
            Put(d, "aiming", Read(player, "AimingGun", "bAimingGun", "isAimingScoped"));
            Put(d, "crouching", Read(player, "Crouching", "IsCrouching", "bCrouching"));
            Put(d, "gameStage", Read(player, "gameStage", "GameStage"));
            Put(d, "bloodMoonParticipation", Read(player, "BloodMoonParticipation", "IsBloodMoon"));
            Put(d, "coreTemp", Read(stats, "CoreTemp"));

            object healthStat = Read(stats, "Health");
            object staminaStat = Read(stats, "Stamina");
            object foodStat = Read(stats, "Food");
            object waterStat = Read(stats, "Water");

            var vitals = new Dictionary<string, object>();
            vitals["health"] = StatSummary(healthStat, Read(player, "Health"), ReflectionUtil.InvokeNoArgs(player, "GetMaxHealth"));
            vitals["stamina"] = StatSummary(staminaStat, Read(player, "Stamina"), ReflectionUtil.InvokeNoArgs(player, "GetMaxStamina"));
            vitals["food"] = StatSummary(foodStat, null, null);
            vitals["water"] = StatSummary(waterStat, Read(player, "Water"), ReflectionUtil.InvokeNoArgs(player, "GetMaxWater"));
            d["vitals"] = vitals;

            var prog = new Dictionary<string, object>();
            Put(prog, "level", Read(progression, "Level"));
            Put(prog, "skillPoints", Read(progression, "SkillPoints"));
            Put(prog, "expToNextLevel", Read(progression, "ExpToNextLevel"));
            Put(prog, "expDeficit", Read(progression, "ExpDeficit"));
            if (prog.Count > 0) d["progression"] = prog;

            object biome = Read(player, "biomeStandingOn", "BiomeStandingOn");
            Put(d, "biome", BiomeSummary(biome));
            return d;
        }

        private static Dictionary<string, object> StatSummary(object stat, object fallbackValue, object fallbackMax)
        {
            var d = new Dictionary<string, object>();
            object value = Read(stat, "Value", "m_value");
            object max = Read(stat, "Max", "ModifiedMax", "BaseMax", "m_baseMax");
            if (value == null) value = fallbackValue;
            if (max == null) max = fallbackMax;
            Put(d, "value", value);
            Put(d, "max", max);
            object pct = Read(stat, "ValuePercent", "ValuePercentUI");
            if (pct != null) Put(d, "percent", pct);
            return d;
        }

        public static Dictionary<string, object> ItemValueSummary(object itemValue, object inventory)
        {
            if (itemValue == null) return null;
            var d = new Dictionary<string, object>();
            Put(d, "type", ReflectionUtil.TypeName(itemValue));
            Put(d, "itemType", Read(itemValue, "type", "Type"));
            Put(d, "quality", Read(itemValue, "Quality"));
            Put(d, "durabilityPercent", Read(itemValue, "PercentUsesLeft"));
            Put(d, "useTimes", Read(itemValue, "UseTimes"));
            Put(d, "maxUseTimes", Read(itemValue, "MaxUseTimes", "MaxUseTimesUI"));
            Put(d, "selectedAmmoTypeIndex", Read(itemValue, "SelectedAmmoTypeIndex"));
            if (inventory != null)
            {
                Put(d, "slot", Read(inventory, "holdingItemIdx", "m_HoldingItemIdx"));
                Put(d, "count", Read(inventory, "holdingCount"));
            }

            object itemClass = Read(itemValue, "ItemClass", "ItemClassOrMissing");
            var cls = ItemClassSummary(itemClass);
            if (cls != null) d["class"] = cls;
            return d;
        }

        public static Dictionary<string, object> ItemClassSummary(object itemClass)
        {
            if (itemClass == null) return null;
            var d = new Dictionary<string, object>();
            Put(d, "name", Read(itemClass, "pName", "Name"));
            Put(d, "localizedName", Read(itemClass, "localizedName", "LocalizedName"));
            Put(d, "displayType", Read(itemClass, "DisplayType"));
            Put(d, "itemTechType", Read(itemClass, "ItemTechType"));
            return d;
        }

        public static Dictionary<string, object> ItemStackSummary(object stack)
        {
            if (stack == null) return null;
            var d = new Dictionary<string, object>();
            Put(d, "count", Read(stack, "count", "Count"));
            object itemValue = Read(stack, "itemValue", "ItemValue");
            var item = ItemValueSummary(itemValue, null);
            if (item != null) d["item"] = item;
            return d;
        }

        public static Dictionary<string, object> EntitySummary(object entity)
        {
            if (entity == null) return null;
            var d = new Dictionary<string, object>();
            Put(d, "type", ReflectionUtil.TypeName(entity));
            Put(d, "entityId", Read(entity, "entityId", "EntityId"));
            Put(d, "entityClass", Read(entity, "entityClass", "EntityClass"));
            Put(d, "name", Read(entity, "LocalizedEntityName", "EntityName", "name"));
            Put(d, "position", VectorSummary(Read(entity, "position", "Position")));
            Put(d, "health", Read(entity, "Health"));
            Put(d, "dead", Read(entity, "bDead", "IsDead"));
            return d;
        }

        public static Dictionary<string, object> BlockSummary(object block)
        {
            if (block == null) return null;
            var d = new Dictionary<string, object>();
            Put(d, "type", ReflectionUtil.TypeName(block));
            Put(d, "id", Read(block, "blockID", "BlockID"));
            Put(d, "name", Read(block, "blockName", "BlockName", "name"));
            object material = Read(block, "blockMaterial", "BlockMaterial");
            if (material != null)
            {
                var m = new Dictionary<string, object>();
                Put(m, "type", ReflectionUtil.TypeName(material));
                Put(m, "id", Read(material, "id", "Id"));
                Put(m, "name", Read(material, "Name", "name"));
                if (m.Count > 0) d["material"] = m;
            }
            return d;
        }

        public static Dictionary<string, object> BlockValueSummary(object blockValue)
        {
            if (blockValue == null) return null;
            var d = new Dictionary<string, object>();
            Put(d, "type", Read(blockValue, "type", "Type"));
            Put(d, "damage", Read(blockValue, "damage", "Damage"));
            Put(d, "isTerrain", Read(blockValue, "isTerrain", "IsTerrain"));
            Put(d, "isAir", Read(blockValue, "isair", "IsAir"));
            object block = Read(blockValue, "Block", "block");
            var b = BlockSummary(block);
            if (b != null) d["block"] = b;
            return d;
        }

        public static Dictionary<string, object> BiomeSummary(object biome)
        {
            if (biome == null) return null;
            var d = new Dictionary<string, object>();
            Put(d, "id", Read(biome, "m_Id", "Id"));
            Put(d, "name", Read(biome, "m_sBiomeName", "Name"));
            Put(d, "localizedName", Read(biome, "LocalizedName"));
            Put(d, "difficulty", Read(biome, "Difficulty"));
            Put(d, "weather", Read(biome, "weatherName"));
            return d;
        }

        public static Dictionary<string, object> WorldSummary(object world, object player)
        {
            if (world == null) return null;
            var d = new Dictionary<string, object>();
            Put(d, "worldTime", Read(world, "worldTime", "WorldTime"));
            Put(d, "day", Read(world, "WorldDay", "worldDay"));
            Put(d, "bloodMoon", Read(world, "isEventBloodMoon", "IsEventBloodMoon"));
            Put(d, "dawnHour", Read(world, "DawnHour"));
            Put(d, "duskHour", Read(world, "DuskHour"));
            if (player != null)
            {
                d["nearby"] = NearbySummary(world, player);
                d["nearbyObservation"] = NearbyObservation.Collect(world, player);
            }
            return d;
        }

        private static Dictionary<string, object> NearbySummary(object world, object player)
        {
            var d = new Dictionary<string, object>();
            object listObject = Read(world, "EntityAlives", "entityAlives");
            IEnumerable list = listObject as IEnumerable;
            if (list == null) return d;

            double px, py, pz;
            if (!TryVector(Read(player, "position", "Position"), out px, out py, out pz)) return d;

            int loaded = 0, zombies = 0, animals = 0, drones = 0, vehicles = 0, turrets = 0;
            int z5 = 0, z10 = 0, z15 = 0, z20 = 0, z30 = 0, targetingPlayer = 0;
            double nearest = double.MaxValue;
            double nearestHorizontal = double.MaxValue;
            object playerId = Read(player, "entityId", "EntityId");
            try
            {
                foreach (object entity in list)
                {
                    if (entity == null || object.ReferenceEquals(entity, player)) continue;
                    loaded++;
                    string type = ReflectionUtil.TypeName(entity) ?? string.Empty;
                    bool zombie = type.IndexOf("Zombie", StringComparison.OrdinalIgnoreCase) >= 0;
                    if (zombie)
                    {
                        zombies++;
                        double ex, ey, ez;
                        if (TryVector(Read(entity, "position", "Position"), out ex, out ey, out ez))
                        {
                            double dx = ex - px, dy = ey - py, dz = ez - pz;
                            double dist = Math.Sqrt(dx * dx + dy * dy + dz * dz);
                            double horizontal = Math.Sqrt(dx * dx + dz * dz);
                            if (dist < nearest) nearest = dist;
                            if (horizontal < nearestHorizontal) nearestHorizontal = horizontal;
                            if (dist <= 5.0) z5++;
                            if (dist <= 10.0) z10++;
                            if (dist <= 15.0) z15++;
                            if (dist <= 20.0) z20++;
                            if (dist <= 30.0) z30++;
                        }
                        object attackTarget = Read(entity, "attackTarget", "attackTargetClient");
                        if (attackTarget != null && (object.ReferenceEquals(attackTarget, player) || object.Equals(Read(attackTarget, "entityId", "EntityId"), playerId)))
                            targetingPlayer++;
                        else
                        {
                            object attackTargetId = Read(entity, "attackTargetEntityId", "AttackTargetEntityId");
                            if (attackTargetId != null && playerId != null && object.Equals(Convert.ToString(attackTargetId), Convert.ToString(playerId))) targetingPlayer++;
                        }
                    }
                    else if (type.IndexOf("Animal", StringComparison.OrdinalIgnoreCase) >= 0) animals++;
                    else if (type.IndexOf("Drone", StringComparison.OrdinalIgnoreCase) >= 0) drones++;
                    else if (type.IndexOf("Vehicle", StringComparison.OrdinalIgnoreCase) >= 0 || type.IndexOf("Motorcycle", StringComparison.OrdinalIgnoreCase) >= 0 || type.IndexOf("Jeep", StringComparison.OrdinalIgnoreCase) >= 0 || type.IndexOf("Gyro", StringComparison.OrdinalIgnoreCase) >= 0) vehicles++;
                    else if (type.IndexOf("Turret", StringComparison.OrdinalIgnoreCase) >= 0) turrets++;
                    if (loaded >= 512) break;
                }
            }
            catch { }

            d["loadedAlive"] = loaded;
            d["zombiesLoaded"] = zombies;
            d["zombiesWithin5m"] = z5;
            d["zombiesWithin10m"] = z10;
            d["zombiesWithin15m"] = z15;
            d["zombiesWithin20m"] = z20;
            d["zombiesWithin30m"] = z30;
            d["zombiesTargetingPlayer"] = targetingPlayer;
            if (nearest < double.MaxValue) d["nearestZombieDistance"] = Math.Round(nearest, 2);
            if (nearestHorizontal < double.MaxValue) d["nearestZombieHorizontalDistance"] = Math.Round(nearestHorizontal, 2);
            d["animalsLoaded"] = animals;
            d["dronesLoaded"] = drones;
            d["vehiclesLoaded"] = vehicles;
            d["turretsLoaded"] = turrets;
            return d;
        }

        public static Dictionary<string, object> VectorSummary(object vector)
        {
            if (vector == null) return null;
            double x, y, z;
            if (!TryVector(vector, out x, out y, out z)) return null;
            return new Dictionary<string, object>
            {
                { "x", Math.Round(x, 3) }, { "y", Math.Round(y, 3) }, { "z", Math.Round(z, 3) }
            };
        }

        public static object CompactSummary(object value)
        {
            if (value == null) return null;
            Type t = value.GetType();
            if (t.IsPrimitive || t.IsEnum || value is string || value is decimal || value is DateTime || value is Guid) return value is Enum ? value.ToString() : value;
            string name = t.FullName ?? t.Name;
            if (name == "ItemValue") return ItemValueSummary(value, null);
            if (name == "ItemStack") return ItemStackSummary(value);
            if (name == "Block" || name.EndsWith(".Block")) return BlockSummary(value);
            if (name == "BlockValue") return BlockValueSummary(value);
            if (name == "Vector3i" || name == "UnityEngine.Vector3") return VectorSummary(value);
            if (name.IndexOf("Entity", StringComparison.OrdinalIgnoreCase) >= 0) return EntitySummary(value);
            return new Dictionary<string, object> { { "type", name } };
        }

        public static object Read(object target, params string[] names)
        {
            return ReflectionUtil.ReadMember(target, names);
        }

        private static bool TryVector(object vector, out double x, out double y, out double z)
        {
            x = y = z = 0.0;
            if (vector == null) return false;
            object ox = Read(vector, "x", "X");
            object oy = Read(vector, "y", "Y");
            object oz = Read(vector, "z", "Z");
            if (ox == null || oy == null || oz == null) return false;
            try
            {
                x = Convert.ToDouble(ox); y = Convert.ToDouble(oy); z = Convert.ToDouble(oz);
                return true;
            }
            catch { return false; }
        }

        private static void Put(Dictionary<string, object> dict, string key, object value)
        {
            if (value != null) dict[key] = value;
        }
    }
}


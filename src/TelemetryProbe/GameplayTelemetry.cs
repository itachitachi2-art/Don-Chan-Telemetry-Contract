using System;
using System.Collections;
using System.Collections.Generic;

namespace DonChan.TelemetryProbe
{
    internal static class GameplayTelemetry
    {
        public static Dictionary<string, object> WeaponSummary(SnapshotRoots roots)
        {
            if (roots == null || roots.HoldingItem == null || roots.Inventory == null) return null;

            object holdingData = LightTelemetry.Read(roots.Inventory, "holdingItemData", "HoldingItemData");
            object itemClass = LightTelemetry.Read(roots.HoldingItem, "ItemClass", "ItemClassOrMissing");
            object rangedAction;
            object rangedData;
            if (!TryFindRangedAction(itemClass, holdingData, out rangedAction, out rangedData)) return null;

            var d = new Dictionary<string, object>();
            object loaded = LightTelemetry.Read(roots.HoldingItem, "Meta");
            object magazineSize = ReflectionUtil.Invoke(rangedAction, "GetMaxAmmoCount", rangedData);
            if (magazineSize == null) magazineSize = LightTelemetry.Read(rangedAction, "BulletsPerMagazine");
            string ammoName = SelectedAmmoName(rangedAction, roots.HoldingItem);
            int reserve = string.IsNullOrEmpty(ammoName) ? 0 : CountItemByName(roots.Inventory, roots.Bag, ammoName);
            bool poweredTool = IsPoweredTool(itemClass, ammoName);

            d["kind"] = poweredTool ? "poweredTool" : "ranged";
            Put(d, "selectedAmmoTypeIndex", LightTelemetry.Read(roots.HoldingItem, "SelectedAmmoTypeIndex"));
            Put(d, "usesMagazines", LightTelemetry.Read(rangedAction, "UsesMagazines"));
            Put(d, "infiniteAmmo", LightTelemetry.Read(rangedAction, "InfiniteAmmo"));

            if (poweredTool)
            {
                var fuel = new Dictionary<string, object>();
                Put(fuel, "loaded", loaded);
                Put(fuel, "capacity", magazineSize);
                if (!string.IsNullOrEmpty(ammoName)) fuel["item"] = ammoName;
                if (!string.IsNullOrEmpty(ammoName)) fuel["reserve"] = reserve;
                d["fuel"] = fuel;
            }
            else
            {
                Put(d, "loaded", loaded);
                Put(d, "magazineSize", magazineSize);
                if (!string.IsNullOrEmpty(ammoName))
                {
                    d["ammoItem"] = ammoName;
                    d["reserve"] = reserve;
                }
            }

            if (rangedData != null)
            {
                Put(d, "reloading", FirstBool(rangedData, "isReloading", "isWeaponReloading"));
                Put(d, "reloadRequested", LightTelemetry.Read(rangedData, "isReloadRequested"));
                Put(d, "reloadCancelled", FirstBool(rangedData, "isReloadCancelled", "isWeaponReloadCancelled", "wasReloadCancelled", "wasWeaponReloadCancelled"));
                Put(d, "changingAmmoType", LightTelemetry.Read(rangedData, "isChangingAmmoType"));
                Put(d, "reloadAmount", LightTelemetry.Read(rangedData, "reloadAmount"));
            }
            Put(d, "canReload", ReflectionUtil.Invoke(rangedAction, "CanReload", rangedData));
            return d;
        }

        public static Dictionary<string, object> FocusSummary(object world, object player)
        {
            if (world == null || player == null) return null;
            object hitInfo = LightTelemetry.Read(player, "HitInfo");
            if (hitInfo == null) return null;
            object valid = LightTelemetry.Read(hitInfo, "bHitValid");
            if (valid is bool && !(bool)valid) return null;

            var d = new Dictionary<string, object>();
            d["valid"] = valid == null ? (object)true : valid;

            Type attackType = ReflectionUtil.FindType("ItemActionAttack");
            object entity = null;
            object blockValue = null;
            if (attackType != null)
            {
                entity = ReflectionUtil.Invoke(attackType, "GetEntityFromHit", hitInfo);
                if (entity == null) entity = ReflectionUtil.Invoke(attackType, "FindHitEntity", hitInfo);
                blockValue = ReflectionUtil.Invoke(attackType, "GetBlockHit", world, hitInfo);
            }

            if (entity != null)
            {
                d["kind"] = "entity";
                d["entity"] = LightTelemetry.EntitySummary(entity);
                double distance;
                if (TryDistance(player, entity, false, out distance)) d["distanceMeters"] = Math.Round(distance, 2);
            }
            else if (blockValue != null && !IsAirBlock(blockValue))
            {
                d["kind"] = "block";
                d["block"] = LightTelemetry.BlockValueSummary(blockValue);
                object pos = LightTelemetry.Read(hitInfo, "lastBlockPos");
                Put(d, "position", LightTelemetry.VectorSummary(pos));

                object block = LightTelemetry.Read(blockValue, "Block", "block");
                double maxDamage = 0.0;
                double damage;
                if (TryDouble(LightTelemetry.Read(block, "MaxDamage"), out maxDamage)) d["maxHp"] = maxDamage;
                if (TryDouble(LightTelemetry.Read(blockValue, "damage", "Damage"), out damage))
                {
                    d["damage"] = damage;
                    if (maxDamage > 0) d["remainingHp"] = Math.Max(0.0, maxDamage - damage);
                }
                double distance;
                if (TryDistanceToVector(player, pos, out distance)) d["distanceMeters"] = Math.Round(distance, 2);
            }
            else
            {
                d["kind"] = "none";
            }
            return d;
        }

        public static Dictionary<string, object> MovementSummary(object player)
        {
            if (player == null) return null;
            var d = new Dictionary<string, object>();
            object velocity = ReflectionUtil.InvokeNoArgs(player, "GetVelocityPerSecond");
            Put(d, "velocity", LightTelemetry.VectorSummary(velocity));
            double speed = 0.0;
            bool hasSpeed = TryMagnitude(velocity, out speed);
            if (hasSpeed) d["speedMetersPerSecond"] = Math.Round(speed, 2);

            object runRaw = LightTelemetry.Read(player, "IsRunning", "MovementRunning", "bMovementRunning");
            bool runModeActive = runRaw is bool && (bool)runRaw;
            object jumpingRaw = LightTelemetry.Read(player, "Jumping", "bJumping", "jumpTrigger");
            object inAirRaw = LightTelemetry.Read(player, "InAir", "inAir", "bAirBorne");
            object onGroundRaw = LightTelemetry.Read(player, "onGround", "wasOnGround");
            object crouchingRaw = LightTelemetry.Read(player, "Crouching", "IsCrouching", "bCrouching");
            object attached = LightTelemetry.Read(player, "AttachedToEntity", "AttachedMainEntity");

            bool mounted = attached != null;
            bool inAir = inAirRaw is bool && (bool)inAirRaw;
            bool crouching = crouchingRaw is bool && (bool)crouchingRaw;
            bool moving = hasSpeed && speed >= 0.15;
            bool running = moving && runModeActive && !inAir && !mounted && !crouching;
            bool walking = moving && !runModeActive && !inAir && !mounted && !crouching;

            d["runModeActive"] = runModeActive;
            d["moving"] = moving;
            d["running"] = running;
            d["walking"] = walking;
            Put(d, "jumping", jumpingRaw);
            Put(d, "inAir", inAirRaw);
            Put(d, "onGround", onGroundRaw);
            Put(d, "crouching", crouchingRaw);
            d["mounted"] = mounted;
            d["locomotion"] = mounted ? "mounted" : inAir ? "airborne" : !moving ? "idle" : crouching ? "crouching" : running ? "running" : walking ? "walking" : "moving";
            return d;
        }

        public static Dictionary<string, object> VehicleSummary(object player)
        {
            if (player == null) return null;
            object entity = LightTelemetry.Read(player, "AttachedToEntity", "AttachedMainEntity");
            if (entity == null || !LooksLikeVehicle(entity)) return null;

            var d = new Dictionary<string, object>();
            d["type"] = ReflectionUtil.TypeName(entity);
            d["subtype"] = VehicleSubtype(entity);
            Put(d, "entityId", LightTelemetry.Read(entity, "entityId", "EntityId"));
            Put(d, "engineRunning", LightTelemetry.Read(entity, "IsEngineRunning"));
            Put(d, "hasDriver", LightTelemetry.Read(entity, "hasDriver"));

            object velocity = ReflectionUtil.InvokeNoArgs(entity, "GetVelocityPerSecond", "GetRBVelocity");
            Put(d, "velocity", LightTelemetry.VectorSummary(velocity));
            double speed;
            if (TryMagnitude(velocity, out speed)) d["speedMetersPerSecond"] = Math.Round(speed, 2);

            object vehicle = LightTelemetry.Read(entity, "vehicle", "Vehicle");
            if (vehicle != null)
            {
                var fuel = new Dictionary<string, object>();
                Put(fuel, "item", ReflectionUtil.InvokeNoArgs(vehicle, "GetFuelItem"));
                Put(fuel, "level", ReflectionUtil.InvokeNoArgs(vehicle, "GetFuelLevel"));
                Put(fuel, "max", ReflectionUtil.InvokeNoArgs(vehicle, "GetMaxFuelLevel"));
                Put(fuel, "percent", ReflectionUtil.InvokeNoArgs(vehicle, "GetFuelPercent"));
                if (fuel.Count > 0) d["fuel"] = fuel;

                var health = new Dictionary<string, object>();
                Put(health, "value", ReflectionUtil.InvokeNoArgs(vehicle, "GetHealth"));
                Put(health, "max", ReflectionUtil.InvokeNoArgs(vehicle, "GetMaxHealth"));
                Put(health, "percent", ReflectionUtil.InvokeNoArgs(vehicle, "GetHealthPercent"));
                if (health.Count > 0) d["health"] = health;
                Put(d, "hasStorage", ReflectionUtil.InvokeNoArgs(vehicle, "HasStorage"));
            }
            else
            {
                Put(d, "health", LightTelemetry.Read(entity, "Health"));
                Put(d, "maxHealth", ReflectionUtil.InvokeNoArgs(entity, "GetMaxHealth"));
                Put(d, "fuelCount", ReflectionUtil.InvokeNoArgs(entity, "GetFuelCount"));
                Put(d, "hasStorage", ReflectionUtil.InvokeNoArgs(entity, "hasStorage"));
            }
            return d;
        }

        public static Dictionary<string, object> StatusSummary(object buffs)
        {
            if (buffs == null) return null;
            IEnumerable active = LightTelemetry.Read(buffs, "ActiveBuffs") as IEnumerable;
            if (active == null) return null;

            var state = new Dictionary<string, object>();
            var matched = new List<object>();
            try
            {
                foreach (object buff in active)
                {
                    string name = Convert.ToString(LightTelemetry.Read(buff, "BuffName", "buffName"));
                    if (string.IsNullOrEmpty(name)) continue;
                    string key = ImportantStatusKey(name);
                    if (key == null) continue;
                    state[key] = true;
                    if (matched.Count < 24) matched.Add(name);
                }
            }
            catch { }
            if (state.Count == 0 && matched.Count == 0) return new Dictionary<string, object> { { "important", state }, { "matchedBuffs", matched } };
            return new Dictionary<string, object> { { "important", state }, { "matchedBuffs", matched } };
        }

        public static Dictionary<string, object> QuestSummary(object journal, object player)
        {
            if (journal == null || player == null) return null;
            object quest = LightTelemetry.Read(journal, "TrackedQuest", "trackedQuest", "ActiveQuest");
            if (quest == null) quest = ReflectionUtil.InvokeNoArgs(journal, "FindActiveQuest");
            if (quest == null) return null;

            var d = new Dictionary<string, object>();
            Put(d, "questCode", LightTelemetry.Read(quest, "QuestCode"));
            Put(d, "state", LightTelemetry.Read(quest, "CurrentState"));
            Put(d, "phase", LightTelemetry.Read(quest, "CurrentPhase"));
            Put(d, "active", LightTelemetry.Read(quest, "Active"));
            Put(d, "tracked", LightTelemetry.Read(quest, "Tracked", "tracked"));
            Put(d, "activeObjectives", LightTelemetry.Read(quest, "ActiveObjectives"));
            Put(d, "poiName", ReflectionUtil.InvokeNoArgs(quest, "GetPOIName"));
            object position = LightTelemetry.Read(quest, "Position", "position");
            Put(d, "position", LightTelemetry.VectorSummary(position));
            double distance;
            if (TryDistanceToVector(player, position, out distance)) d["distanceMeters"] = Math.Round(distance, 1);

            object questClass = LightTelemetry.Read(quest, "QuestClass", "questClass");
            if (questClass != null)
            {
                var qc = new Dictionary<string, object>();
                Put(qc, "type", ReflectionUtil.TypeName(questClass));
                Put(qc, "id", LightTelemetry.Read(questClass, "ID", "Id", "id", "Name", "name"));
                Put(qc, "name", LightTelemetry.Read(questClass, "Name", "name", "LocalizationKey"));
                if (qc.Count > 0) d["class"] = qc;
            }

            IEnumerable objectives = LightTelemetry.Read(quest, "Objectives") as IEnumerable;
            if (objectives != null)
            {
                var list = new List<object>();
                try
                {
                    foreach (object objective in objectives)
                    {
                        if (objective == null) continue;
                        var od = new Dictionary<string, object>();
                        od["type"] = ReflectionUtil.TypeName(objective);
                        Put(od, "complete", LightTelemetry.Read(objective, "Complete", "IsComplete", "complete"));
                        Put(od, "current", LightTelemetry.Read(objective, "Current", "CurrentValue", "current"));
                        Put(od, "target", LightTelemetry.Read(objective, "Target", "MaxCount", "Count", "target"));
                        Put(od, "description", ReflectionUtil.InvokeNoArgs(objective, "GetDescription", "GetText"));
                        list.Add(od);
                        if (list.Count >= 12) break;
                    }
                }
                catch { }
                if (list.Count > 0) d["objectives"] = list;
            }
            return d;
        }

        public static bool TryDistance(object a, object b, bool horizontalOnly, out double distance)
        {
            distance = 0.0;
            object pa = LightTelemetry.Read(a, "position", "Position");
            object pb = LightTelemetry.Read(b, "position", "Position");
            return TryDistanceVectors(pa, pb, horizontalOnly, out distance);
        }

        private static bool TryDistanceToVector(object player, object targetPosition, out double distance)
        {
            return TryDistanceVectors(LightTelemetry.Read(player, "position", "Position"), targetPosition, false, out distance);
        }

        private static bool TryDistanceVectors(object a, object b, bool horizontalOnly, out double distance)
        {
            distance = 0.0;
            double ax, ay, az, bx, by, bz;
            if (!TryVector(a, out ax, out ay, out az) || !TryVector(b, out bx, out by, out bz)) return false;
            double dx = bx - ax, dy = horizontalOnly ? 0.0 : by - ay, dz = bz - az;
            distance = Math.Sqrt(dx * dx + dy * dy + dz * dz);
            return true;
        }

        private static bool TryFindRangedAction(object itemClass, object holdingData, out object rangedAction, out object rangedData)
        {
            rangedAction = null;
            rangedData = null;
            if (itemClass == null || holdingData == null) return false;
            var actions = ToList(LightTelemetry.Read(itemClass, "Actions", "actions") as IEnumerable);
            var actionData = ToList(LightTelemetry.Read(holdingData, "actionData", "ActionData") as IEnumerable);
            for (int i = 0; i < actions.Count; i++)
            {
                object action = actions[i];
                string type = ReflectionUtil.TypeName(action) ?? string.Empty;
                if (type.IndexOf("ItemActionRanged", StringComparison.OrdinalIgnoreCase) < 0) continue;
                rangedAction = action;
                if (i < actionData.Count) rangedData = actionData[i];
                break;
            }
            if (rangedAction == null)
            {
                foreach (object data in actionData)
                {
                    string type = ReflectionUtil.TypeName(data) ?? string.Empty;
                    if (type.IndexOf("ItemActionDataRanged", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        rangedData = data;
                        break;
                    }
                }
            }
            return rangedAction != null;
        }

        private static string SelectedAmmoName(object rangedAction, object itemValue)
        {
            object namesObject = LightTelemetry.Read(rangedAction, "MagazineItemNames");
            IEnumerable names = namesObject as IEnumerable;
            if (names == null) return null;
            var list = new List<string>();
            try { foreach (object x in names) if (x != null) list.Add(Convert.ToString(x)); } catch { }
            if (list.Count == 0) return null;
            int index = 0;
            try { index = Convert.ToInt32(LightTelemetry.Read(itemValue, "SelectedAmmoTypeIndex")); } catch { index = 0; }
            if (index < 0 || index >= list.Count) index = 0;
            return list[index];
        }

        private static int CountItemByName(object inventory, object bag, string itemName)
        {
            int total = 0;
            total += CountStacks(LightTelemetry.Read(inventory, "slots"), itemName, true);
            object bagSlots = ReflectionUtil.InvokeNoArgs(bag, "GetSlots");
            if (bagSlots == null) bagSlots = LightTelemetry.Read(bag, "items");
            total += CountStacks(bagSlots, itemName, false);
            return total;
        }

        private static int CountStacks(object collection, string itemName, bool inventoryData)
        {
            IEnumerable enumerable = collection as IEnumerable;
            if (enumerable == null) return 0;
            int total = 0;
            try
            {
                foreach (object entry in enumerable)
                {
                    if (entry == null) continue;
                    object stack = inventoryData ? LightTelemetry.Read(entry, "itemStack", "ItemStack") : entry;
                    if (stack == null) continue;
                    object value = LightTelemetry.Read(stack, "itemValue", "ItemValue");
                    object cls = LightTelemetry.Read(value, "ItemClass", "ItemClassOrMissing");
                    string name = Convert.ToString(LightTelemetry.Read(cls, "pName", "Name"));
                    if (!string.Equals(name, itemName, StringComparison.OrdinalIgnoreCase)) continue;
                    try { total += Convert.ToInt32(LightTelemetry.Read(stack, "count", "Count")); } catch { }
                }
            }
            catch { }
            return total;
        }

        private static string ImportantStatusKey(string buffName)
        {
            string n = buffName.ToLowerInvariant();
            if (n.Contains("bleed")) return "bleeding";
            if (n.Contains("infect")) return "infected";
            if (n.Contains("broken") || n.Contains("fracture")) return "brokenBone";
            if (n.Contains("sprain")) return "sprained";
            if (IsStunnedBuff(n)) return "stunned";
            if (n.Contains("burn")) return "burning";
            if (n.Contains("encumber")) return "encumbered";
            if (n.Contains("concussion")) return "concussion";
            if (n.Contains("lacerat")) return "laceration";
            if (n.Contains("abrasion")) return "abrasion";
            if (n.Contains("dysent")) return "dysentery";
            if (n.Contains("dehydrat")) return "dehydrated";
            if (n.Contains("starv") || n.Contains("hungry")) return "hungry";
            if (n.Contains("heatstroke") || n.Contains("overheat")) return "hot";
            if (n.Contains("hypother") || n.Contains("freez") || n.Contains("cold")) return "cold";
            return null;
        }


        private static bool IsStunnedBuff(string normalizedBuffName)
        {
            // Keep this deliberately conservative. Broad substring matching caused
            // buffPowerAttackStaminaStunt to be misreported as a player stun.
            if (normalizedBuffName == "buffstunned" || normalizedBuffName == "buffstun") return true;
            if (normalizedBuffName == "buffinjurystunned1" || normalizedBuffName == "buffinjurystunned2") return true;
            return false;
        }

        private static bool IsPoweredTool(object itemClass, string ammoName)
        {
            string ammo = (ammoName ?? string.Empty).Replace("_", string.Empty).ToLowerInvariant();
            if (ammo.Contains("gascan")) return true;
            string name = Convert.ToString(LightTelemetry.Read(itemClass, "pName", "Name", "localizedName", "LocalizedName")) ?? string.Empty;
            name = name.Replace("_", string.Empty).Replace(" ", string.Empty).ToLowerInvariant();
            return name.Contains("auger") || name.Contains("chainsaw");
        }

        private static bool IsAirBlock(object blockValue)
        {
            object air = LightTelemetry.Read(blockValue, "isair", "IsAir");
            return air is bool && (bool)air;
        }

        private static bool LooksLikeVehicle(object entity)
        {
            string type = ReflectionUtil.TypeName(entity) ?? string.Empty;
            return type.IndexOf("Vehicle", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   type.IndexOf("Bicycle", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   type.IndexOf("Minibike", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   type.IndexOf("Motorcycle", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   type.IndexOf("Jeep", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   type.IndexOf("Gyro", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string VehicleSubtype(object vehicle)
        {
            string t = ReflectionUtil.TypeName(vehicle) ?? string.Empty;
            if (t.IndexOf("Bicycle", StringComparison.OrdinalIgnoreCase) >= 0) return "Bicycle";
            if (t.IndexOf("Minibike", StringComparison.OrdinalIgnoreCase) >= 0) return "Minibike";
            if (t.IndexOf("Motorcycle", StringComparison.OrdinalIgnoreCase) >= 0) return "Motorcycle";
            if (t.IndexOf("Jeep", StringComparison.OrdinalIgnoreCase) >= 0) return "Jeep";
            if (t.IndexOf("Gyro", StringComparison.OrdinalIgnoreCase) >= 0) return "Gyrocopter";
            return "Generic";
        }

        private static object FirstBool(object target, params string[] names)
        {
            foreach (string name in names)
            {
                object v = LightTelemetry.Read(target, name);
                if (v is bool && (bool)v) return true;
            }
            foreach (string name in names)
            {
                object v = LightTelemetry.Read(target, name);
                if (v is bool) return false;
            }
            return null;
        }

        private static List<object> ToList(IEnumerable values)
        {
            var list = new List<object>();
            if (values == null) return list;
            try { foreach (object x in values) list.Add(x); } catch { }
            return list;
        }

        private static bool TryMagnitude(object vector, out double magnitude)
        {
            magnitude = 0.0;
            double x, y, z;
            if (!TryVector(vector, out x, out y, out z)) return false;
            magnitude = Math.Sqrt(x * x + y * y + z * z);
            return true;
        }

        private static bool TryVector(object vector, out double x, out double y, out double z)
        {
            x = y = z = 0.0;
            if (vector == null) return false;
            try
            {
                object ox = LightTelemetry.Read(vector, "x", "X");
                object oy = LightTelemetry.Read(vector, "y", "Y");
                object oz = LightTelemetry.Read(vector, "z", "Z");
                if (ox == null || oy == null || oz == null) return false;
                x = Convert.ToDouble(ox); y = Convert.ToDouble(oy); z = Convert.ToDouble(oz);
                return true;
            }
            catch { return false; }
        }

        private static bool TryDouble(object value, out double result)
        {
            result = 0.0;
            if (value == null) return false;
            try { result = Convert.ToDouble(value); return true; } catch { return false; }
        }

        private static void Put(Dictionary<string, object> dict, string key, object value)
        {
            if (value != null) dict[key] = value;
        }
    }
}

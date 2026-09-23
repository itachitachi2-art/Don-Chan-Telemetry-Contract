using System;
using System.Collections.Generic;
using System.Reflection;

namespace DonChan.TelemetryProbe
{
    internal static class EventNormalizer
    {
        private static readonly object RateSync = new object();
        private static readonly Dictionary<string, DateTime> LastEmitted = new Dictionary<string, DateTime>(StringComparer.Ordinal);

        public static bool ShouldEmitDelegate(string eventName, object[] args)
        {
            if (eventName != null && eventName.EndsWith("GameStats.OnChangedDelegates", StringComparison.Ordinal)) return false;
            return true;
        }

        public static bool ShouldEmitHarmony(string eventName, object[] args)
        {
            if (eventName == "buff.add" || eventName == "buff.remove")
            {
                string buff = Arg(args, 0) as string;
                if (!string.IsNullOrEmpty(buff) && buff.IndexOf("buffStatusCheck", StringComparison.OrdinalIgnoreCase) >= 0) return false;
                return AllowEvery(eventName + "|" + (buff ?? string.Empty), 1.0);
            }
            return true;
        }

        private static bool AllowEvery(string key, double seconds)
        {
            DateTime now = DateTime.UtcNow;
            lock (RateSync)
            {
                DateTime last;
                if (LastEmitted.TryGetValue(key, out last) && (now - last).TotalSeconds < seconds) return false;
                LastEmitted[key] = now;
                return true;
            }
        }

        public static Dictionary<string, object> NormalizeDelegate(string eventName, string scope, object[] args)
        {
            var root = Base(eventName, "delegate", scope);
            var data = new Dictionary<string, object>();
            root["data"] = data;

            if (Ends(eventName, "HarvestItem"))
            {
                if (Arg(args, 0) != null) data["tool"] = LightTelemetry.ItemValueSummary(Arg(args, 0), null);
                if (Arg(args, 1) != null) data["harvested"] = LightTelemetry.ItemStackSummary(Arg(args, 1));
                if (Arg(args, 2) != null) data["block"] = LightTelemetry.BlockValueSummary(Arg(args, 2));
            }
            else if (Ends(eventName, "BlockDestroy") || Ends(eventName, "BlockPlace") || Ends(eventName, "BlockChange") || Ends(eventName, "BlockUpgrade") || Ends(eventName, "BlockPickup"))
            {
                object a0 = Arg(args, 0);
                if (a0 != null)
                {
                    string n = ReflectionUtil.TypeName(a0) ?? string.Empty;
                    data["block"] = n == "BlockValue" ? (object)LightTelemetry.BlockValueSummary(a0) : LightTelemetry.BlockSummary(a0);
                }
                if (Arg(args, 1) != null) data["position"] = LightTelemetry.VectorSummary(Arg(args, 1));
                AddCompactArgs(data, args, 2, 4);
            }
            else if (Ends(eventName, "AddItem") || Ends(eventName, "CraftItem") || Ends(eventName, "RepairItem") || Ends(eventName, "ScrapItem") || Ends(eventName, "SellItems") || Ends(eventName, "BuyItems"))
            {
                object a0 = Arg(args, 0);
                if (a0 != null)
                {
                    string n = ReflectionUtil.TypeName(a0) ?? string.Empty;
                    data["item"] = n == "ItemStack" ? (object)LightTelemetry.ItemStackSummary(a0) : LightTelemetry.ItemValueSummary(a0, null);
                }
                AddCompactArgs(data, args, 1, 4);
            }
            else if (Ends(eventName, "UseItem") || Ends(eventName, "WearItem") || Ends(eventName, "HoldItem") || Ends(eventName, "AssembleItem") || Ends(eventName, "ExchangeFromItem"))
            {
                object a0 = Arg(args, 0);
                if (a0 != null) data["item"] = LightTelemetry.CompactSummary(a0);
                AddCompactArgs(data, args, 1, 4);
            }
            else if (Ends(eventName, "EntityKill") || eventName.IndexOf("GameEntityKilled", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                if (Arg(args, 0) != null) data["actor"] = LightTelemetry.EntitySummary(Arg(args, 0));
                if (Arg(args, 1) != null) data["target"] = LightTelemetry.EntitySummary(Arg(args, 1));
                AddCompactArgs(data, args, 2, 4);
            }
            else if (eventName.IndexOf("EntityLoaded", StringComparison.OrdinalIgnoreCase) >= 0 || eventName.IndexOf("EntitySpawned", StringComparison.OrdinalIgnoreCase) >= 0 || eventName.IndexOf("EntityUnloaded", StringComparison.OrdinalIgnoreCase) >= 0 || eventName.IndexOf("EntityDespawned", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                if (Arg(args, 0) != null) data["entity"] = LightTelemetry.EntitySummary(Arg(args, 0));
                AddCompactArgs(data, args, 1, 3);
            }
            else if (Ends(eventName, "BiomeEnter"))
            {
                if (Arg(args, 0) != null) data["biome"] = LightTelemetry.CompactSummary(Arg(args, 0));
                AddCompactArgs(data, args, 1, 3);
            }
            else if (eventName.IndexOf("Toolbelt", StringComparison.OrdinalIgnoreCase) >= 0 || eventName.IndexOf("Backpack", StringComparison.OrdinalIgnoreCase) >= 0 || eventName.IndexOf("Equipment", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                data["changed"] = true;
                data["source"] = scope;
            }
            else
            {
                AddCompactArgs(data, args, 0, 8);
            }

            return root;
        }

        public static Dictionary<string, object> NormalizeHarmony(string eventName, MethodBase method, object instance, object[] args)
        {
            var root = Base(eventName, "harmony", null);
            if (eventName.StartsWith("combat.", StringComparison.Ordinal) && DynamicPatchRegistrar.DamageActionId != null)
                root["damageActionId"] = DynamicPatchRegistrar.DamageActionId;
            if (method != null) root["method"] = method.DeclaringType.FullName + "." + method.Name;
            var data = new Dictionary<string, object>();
            root["data"] = data;

            if (eventName == "combat.hit")
            {
                data["attackMode"] = CombatEventPolicy.NamedArgument(method, args, "_attackMode");
                data["attackerEntityId"] = CombatEventPolicy.NamedArgument(method, args, "_attackerEntityId");
                object hitInfo = FindByTypeSuffix(args, "AttackHitInfo");
                if (hitInfo != null)
                {
                    Put(data, "blockHit", LightTelemetry.Read(hitInfo, "bBlockHit"));
                    Put(data, "harvestTool", LightTelemetry.Read(hitInfo, "bHarvestTool"));
                    Put(data, "killed", LightTelemetry.Read(hitInfo, "bKilled"));
                    Put(data, "damage", LightTelemetry.Read(hitInfo, "damageGiven", "damage"));
                    Put(data, "damageMax", LightTelemetry.Read(hitInfo, "damageMax"));
                    Put(data, "damagePerHit", LightTelemetry.Read(hitInfo, "damagePerHit"));
                    Put(data, "material", LightTelemetry.Read(hitInfo, "materialCategory"));
                    Put(data, "critical", LightTelemetry.Read(hitInfo, "isCriticalHit"));
                    object entity = LightTelemetry.Read(hitInfo, "entityHit");
                    if (entity != null) data["entity"] = LightTelemetry.EntitySummary(entity);
                    object block = LightTelemetry.Read(hitInfo, "blockBeingDamaged");
                    if (block != null) data["block"] = LightTelemetry.BlockValueSummary(block);
                    object pos = LightTelemetry.Read(hitInfo, "raycastHitPosition");
                    if (pos != null) data["position"] = LightTelemetry.VectorSummary(pos);
                }
                AddScalarArgs(data, args, 0, 12);
            }
            else if (eventName == "combat.damage")
            {
                if (instance != null) data["target"] = LightTelemetry.EntitySummary(instance);
                object source = Arg(args, 0);
                if (source != null)
                {
                    var src = new Dictionary<string, object>();
                    Put(src, "damageSource", LightTelemetry.Read(source, "damageSource"));
                    Put(src, "damageType", LightTelemetry.Read(source, "damageType"));
                    Put(src, "bodyParts", LightTelemetry.Read(source, "bodyParts"));
                    Put(src, "ownerEntityId", LightTelemetry.Read(source, "ownerEntityId"));
                    Put(src, "creatorEntityId", LightTelemetry.Read(source, "CreatorEntityId"));
                    object item = LightTelemetry.Read(source, "AttackingItem");
                    if (item != null) src["item"] = LightTelemetry.ItemValueSummary(item, null);
                    data["source"] = src;
                }
                Put(data, "amount", Arg(args, 1));
                AddScalarArgs(data, args, 2, 5);
            }
            else if (eventName == "combat.kill" || eventName == "combat.response")
            {
                if (instance != null) data["target"] = LightTelemetry.EntitySummary(instance);
                object response = Arg(args, 0);
                if (response != null)
                {
                    var r = new Dictionary<string, object>();
                    Put(r, "strength", LightTelemetry.Read(response, "Strength"));
                    Put(r, "modifiedStrength", LightTelemetry.Read(response, "ModStrength"));
                    Put(r, "fatal", LightTelemetry.Read(response, "Fatal"));
                    Put(r, "critical", LightTelemetry.Read(response, "Critical"));
                    Put(r, "dismember", LightTelemetry.Read(response, "Dismember"));
                    Put(r, "bodyPart", LightTelemetry.Read(response, "HitBodyPart"));
                    Put(r, "direction", LightTelemetry.Read(response, "HitDirection"));
                    object responseSource = LightTelemetry.Read(response, "Source");
                    if (responseSource != null) {
                        r["source"] = new Dictionary<string, object> {
                            { "ownerEntityId", LightTelemetry.Read(responseSource, "ownerEntityId") },
                            { "creatorEntityId", LightTelemetry.Read(responseSource, "CreatorEntityId") },
                            { "damageType", LightTelemetry.Read(responseSource, "damageType") },
                            { "damageSource", LightTelemetry.Read(responseSource, "damageSource") }
                        };
                    }
                    data["response"] = r;
                }
            }
            else if (eventName == "combat.death")
            {
                if (instance != null) data["target"] = LightTelemetry.EntitySummary(instance);
            }
            else if (eventName == "buff.add" || eventName == "buff.remove")
            {
                Put(data, "buff", Arg(args, 0));
                object owner = LightTelemetry.Read(instance, "Entity", "entity", "m_entity");
                if (owner != null) data["entity"] = LightTelemetry.EntitySummary(owner);
                AddScalarArgs(data, args, 1, 6);
            }
            else
            {
                if (instance != null) data["instance"] = LightTelemetry.CompactSummary(instance);
                AddCompactArgs(data, args, 0, 8);
            }
            return root;
        }

        private static Dictionary<string, object> Base(string eventName, string kind, string scope)
        {
            var root = new Dictionary<string, object>();
            root["utc"] = DateTime.UtcNow;
            root["kind"] = kind;
            root["event"] = eventName;
            if (!string.IsNullOrEmpty(scope)) root["scope"] = scope;
            return root;
        }

        private static object FindByTypeSuffix(object[] args, string suffix)
        {
            if (args == null) return null;
            for (int i = 0; i < args.Length; i++)
            {
                object value = args[i];
                if (value == null) continue;
                string n = ReflectionUtil.TypeName(value) ?? string.Empty;
                if (n.EndsWith(suffix, StringComparison.Ordinal) || n.IndexOf("+" + suffix, StringComparison.Ordinal) >= 0) return value;
            }
            return null;
        }

        private static void AddCompactArgs(Dictionary<string, object> data, object[] args, int start, int maxCount)
        {
            if (args == null || start >= args.Length) return;
            var list = new List<object>();
            int end = Math.Min(args.Length, start + maxCount);
            for (int i = start; i < end; i++) list.Add(LightTelemetry.CompactSummary(args[i]));
            if (list.Count > 0) data["args"] = list;
        }

        private static void AddScalarArgs(Dictionary<string, object> data, object[] args, int start, int maxCount)
        {
            if (args == null || start >= args.Length) return;
            var list = new List<object>();
            int end = Math.Min(args.Length, start + maxCount);
            for (int i = start; i < end; i++)
            {
                object value = args[i];
                if (value == null) { list.Add(null); continue; }
                Type t = value.GetType();
                if (t.IsPrimitive || t.IsEnum || value is string || value is decimal) list.Add(value is Enum ? value.ToString() : value);
            }
            if (list.Count > 0) data["scalarArgs"] = list;
        }

        private static object Arg(object[] args, int index)
        {
            return args != null && index >= 0 && index < args.Length ? args[index] : null;
        }

        private static bool Ends(string value, string suffix)
        {
            return value != null && value.EndsWith(suffix, StringComparison.Ordinal);
        }

        private static void Put(Dictionary<string, object> dict, string key, object value)
        {
            if (value != null) dict[key] = value;
        }
    }
}


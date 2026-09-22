using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace DonChan.TelemetryProbe
{
    /// <summary>
    /// High-confidence player action hooks adapted from DonChanActionTheater 0.5.0.
    /// These hooks complement low-level QuestEventManager/combat telemetry by recording
    /// when an action actually completed or fired, plus a stable semantic subtype.
    /// </summary>
    internal static class ActionSemanticTelemetry
    {
        private static ProbeConfig _config;
        private static bool _patched;

        internal static void Initialize(Harmony harmony, ProbeConfig config)
        {
            _config = config;
            if (_patched || config == null || !config.EnableSemanticActionEvents) return;

            try
            {
                harmony.PatchAll(typeof(ActionSemanticTelemetry).Assembly);
                _patched = true;
                ProbeLog.Capability(new Dictionary<string, object>
                {
                    { "utc", DateTime.UtcNow },
                    { "capability", "semantic_action_hooks" },
                    { "name", "DonChanActionTheater-compatible" },
                    { "ok", true },
                    { "detail", "consume/melee/ranged/reload/craft/vehicle hooks registered" }
                });
            }
            catch (Exception ex)
            {
                ProbeLog.Capability(new Dictionary<string, object>
                {
                    { "utc", DateTime.UtcNow },
                    { "capability", "semantic_action_hooks" },
                    { "name", "DonChanActionTheater-compatible" },
                    { "ok", false },
                    { "detail", ex.GetType().Name + ":" + ex.Message }
                });
                ProbeLog.Warn("Semantic action hook registration failed: " + ex.Message);
            }
        }

        internal static void Emit(string eventName, string topic, string subtype, string itemName, ItemValue itemValue, object extra)
        {
            if (_config == null || !_config.EnableNormalizedEvents || !_config.EnableSemanticActionEvents) return;

            var data = new Dictionary<string, object>();
            if (!string.IsNullOrEmpty(topic)) data["topic"] = topic;
            if (!string.IsNullOrEmpty(subtype)) data["subtype"] = subtype;
            if (!string.IsNullOrEmpty(itemName)) data["itemName"] = itemName;
            if (itemValue != null) data["item"] = LightTelemetry.ItemValueSummary(itemValue, null);
            if (extra != null) data["extra"] = extra;

            ProbeLog.Event(new Dictionary<string, object>
            {
                { "utc", DateTime.UtcNow },
                { "kind", "semantic" },
                { "event", eventName },
                { "source", "DonChanTelemetryProbe" },
                { "data", data }
            });
        }

        internal static Dictionary<string, object> VehicleExtra(EntityVehicle vehicle)
        {
            var d = new Dictionary<string, object>();
            if (vehicle == null) return d;
            d["vehicleType"] = vehicle.GetType().Name;
            d["vehicle"] = LightTelemetry.EntitySummary(vehicle);
            return d;
        }
    }

    internal static class SemanticActionClassifier
    {
        internal static string ItemName(ItemActionData data)
        {
            if (data == null || data.invData == null || data.invData.item == null) return "generic";
            return data.invData.item.GetItemName() ?? "generic";
        }

        internal static ItemValue ItemValue(ItemActionData data)
        {
            if (data == null || data.invData == null) return null;
            return data.invData.itemValue;
        }

        internal static bool IsLocalObject(object actionData)
        {
            object inv = LightTelemetry.Read(actionData, "invData");
            object holder = LightTelemetry.Read(inv, "holdingEntity");
            return holder is EntityPlayerLocal;
        }

        internal static string ItemNameObject(object actionData)
        {
            object inv = LightTelemetry.Read(actionData, "invData");
            object itemClass = LightTelemetry.Read(inv, "item");
            object name = ReflectionUtil.InvokeNoArgs(itemClass, "GetItemName");
            if (name == null) name = LightTelemetry.Read(itemClass, "pName", "Name");
            return name == null ? "generic" : Convert.ToString(name);
        }

        internal static ItemValue ItemValueObject(object actionData)
        {
            object inv = LightTelemetry.Read(actionData, "invData");
            return LightTelemetry.Read(inv, "itemValue") as ItemValue;
        }

        internal static bool IsLocal(ItemActionData data)
        {
            return data != null && data.invData != null && data.invData.holdingEntity is EntityPlayerLocal;
        }

        internal static string ConsumableSubtype(string itemName)
        {
            string n = Normalize(itemName);
            if (ContainsAny(n, "resourcebrokenglass", "brokenglass")) return "Hazard";
            if (n.StartsWith("food")) return "Food";
            if (n.StartsWith("drink")) return "Drink";
            if (ContainsAny(n, "bandage", "firstaid", "medical", "medkit", "painkiller", "antibiotic", "vitamin", "splint", "cast", "steroid", "aloe", "heal", "healthbar"))
                return "Medicine";
            if (n.StartsWith("drug")) return "Boost";
            return "Generic";
        }

        internal static string MeleeSubtype(string itemName)
        {
            string n = Normalize(itemName);
            if (ContainsAny(n, "chainsaw")) return "Chainsaw";
            if (ContainsAny(n, "knife", "machete", "blade")) return "Knife";
            if (ContainsAny(n, "spear", "javelin")) return "Spear";
            if (ContainsAny(n, "sledge")) return "Sledge";
            if (ContainsAny(n, "baton", "stun")) return "Baton";
            if (ContainsAny(n, "shovel", "spade")) return "Shovel";
            if (ContainsAny(n, "pickaxe")) return "Pickaxe";
            if (ContainsAny(n, "axe")) return "Axe";
            if (ContainsAny(n, "knuckle", "fist", "hand")) return "Fist";
            if (ContainsAny(n, "club", "bat")) return "Club";
            return "Generic";
        }

        internal static bool IsUtilityTool(string itemName)
        {
            string n = Normalize(itemName);
            return n.StartsWith("meleetool") && !n.Contains("chainsaw") && !n.Contains("auger");
        }

        internal static string ToolSubtype(string itemName)
        {
            string n = Normalize(itemName);
            if (n.Contains("impactdriver")) return "ImpactDriver";
            if (n.Contains("ratchet")) return "Ratchet";
            if (n.Contains("wrench")) return "Wrench";
            if (n.Contains("clawhammer")) return "Hammer";
            if (n.Contains("nailgun")) return "Nailgun";
            if (n.Contains("pickaxe")) return "Pickaxe";
            if (n.Contains("shovel")) return "Shovel";
            if (n.Contains("axe")) return "Axe";
            if (n.Contains("torch")) return "Torch";
            return "Generic";
        }

        internal static bool IsDeployableBot(string itemName)
        {
            string n = Normalize(itemName);
            return n.Contains("gunbott1junksledge") || n.Contains("gunbott2junkturret");
        }

        internal static string DeployableSubtype(string itemName)
        {
            string n = Normalize(itemName);
            if (n.Contains("junksledge")) return "RoboticSledge";
            if (n.Contains("junkturret")) return "RoboticTurret";
            return "Generic";
        }

        internal static string RangedSubtype(string itemName)
        {
            string n = Normalize(itemName);
            if (ContainsAny(n, "desertvulture", "deserteagle", "deagle")) return "DesertVulture";
            if (ContainsAny(n, "magnum", "revolver")) return "Magnum";
            if (ContainsAny(n, "crossbow")) return "Crossbow";
            if (ContainsAny(n, "bow")) return "Bow";
            if (ContainsAny(n, "shotgun")) return "Shotgun";
            if (ContainsAny(n, "smg", "submachine")) return "SMG";
            if (ContainsAny(n, "rifle", "ak47", "m60", "machinegun", "tacticalar")) return "Rifle";
            if (ContainsAny(n, "rocket", "launcher")) return "Launcher";
            if (ContainsAny(n, "nailgun")) return "Nailgun";
            if (ContainsAny(n, "pistol")) return "Pistol";
            return "Generic";
        }

        internal static bool IsChainsaw(string itemName)
        {
            return Normalize(itemName).Contains("chainsaw");
        }

        internal static bool IsPoweredTool(string itemName, ItemValue itemValue)
        {
            string n = Normalize(itemName);
            if (ContainsAny(n, "auger", "chainsaw")) return true;

            object itemClass = itemValue == null ? null : LightTelemetry.Read(itemValue, "ItemClass", "ItemClassOrMissing");
            object displayType = LightTelemetry.Read(itemClass, "DisplayType");
            return displayType != null && string.Equals(Convert.ToString(displayType), "motorTool", StringComparison.OrdinalIgnoreCase);
        }

        internal static string PoweredToolSubtype(string itemName)
        {
            string n = Normalize(itemName);
            if (n.Contains("auger")) return "Auger";
            if (n.Contains("chainsaw")) return "Chainsaw";
            return "Generic";
        }

        internal static string VehicleSubtype(EntityVehicle vehicle)
        {
            if (vehicle is EntityBicycle) return "Bicycle";
            if (vehicle is EntityMinibike) return "Minibike";
            if (vehicle is EntityMotorcycle) return "Motorcycle";
            if (vehicle is EntityVJeep) return "Jeep";
            if (vehicle is EntityVGyroCopter) return "Gyrocopter";
            return "Generic";
        }

        private static string Normalize(string value)
        {
            return string.IsNullOrEmpty(value) ? "" : value.Replace("_", "").Replace(" ", "").ToLowerInvariant();
        }

        private static bool ContainsAny(string value, params string[] words)
        {
            for (int i = 0; i < words.Length; i++) if (value.Contains(words[i])) return true;
            return false;
        }
    }

    [HarmonyPatch(typeof(ItemActionEat), "consume")]
    internal static class SemanticConsumableCompletePatch
    {
        private static void Postfix(ItemActionData _actionData)
        {
            if (!SemanticActionClassifier.IsLocal(_actionData)) return;
            string item = SemanticActionClassifier.ItemName(_actionData);
            string subtype = SemanticActionClassifier.ConsumableSubtype(item);
            ActionSemanticTelemetry.Emit("action.consume.complete", "Consumable", subtype, item, SemanticActionClassifier.ItemValue(_actionData), null);
        }
    }

    [HarmonyPatch(typeof(ItemActionMelee), "ExecuteAction")]
    internal static class SemanticMeleeActionPatch
    {
        private static void Postfix(ItemActionData _actionData, bool _bReleased)
        {
            SemanticMeleeTrigger.TryTrigger(_actionData, _bReleased);
        }
    }

    [HarmonyPatch(typeof(ItemActionDynamicMelee), "ExecuteAction")]
    internal static class SemanticDynamicMeleeActionPatch
    {
        private static void Postfix(ItemActionData _actionData, bool _bReleased)
        {
            SemanticMeleeTrigger.TryTrigger(_actionData, _bReleased);
        }
    }

    internal static class SemanticMeleeTrigger
    {
        internal static void TryTrigger(ItemActionData actionData, bool released)
        {
            if (released || !SemanticActionClassifier.IsLocal(actionData)) return;
            string item = SemanticActionClassifier.ItemName(actionData);
            ItemValue itemValue = SemanticActionClassifier.ItemValue(actionData);
            if (SemanticActionClassifier.IsUtilityTool(item))
            {
                ActionSemanticTelemetry.Emit("action.tool.use", "Tool", SemanticActionClassifier.ToolSubtype(item), item, itemValue, null);
                return;
            }
            ActionSemanticTelemetry.Emit("action.melee", "Melee", SemanticActionClassifier.MeleeSubtype(item), item, itemValue, null);
        }
    }

    [HarmonyPatch(typeof(ItemActionRanged), "onHoldingEntityFired")]
    internal static class SemanticRangedFiredPatch
    {
        private static void Postfix(ItemActionData _actionData)
        {
            if (!SemanticActionClassifier.IsLocal(_actionData)) return;
            string item = SemanticActionClassifier.ItemName(_actionData);
            ItemValue itemValue = SemanticActionClassifier.ItemValue(_actionData);

            if (SemanticActionClassifier.IsPoweredTool(item, itemValue))
            {
                ActionSemanticTelemetry.Emit("action.poweredTool.use", "PoweredTool", SemanticActionClassifier.PoweredToolSubtype(item), item, itemValue, null);
                return;
            }

            if (SemanticActionClassifier.IsDeployableBot(item))
            {
                ActionSemanticTelemetry.Emit("action.deployable.fire", "Deployable", SemanticActionClassifier.DeployableSubtype(item), item, itemValue, null);
                return;
            }

            ActionSemanticTelemetry.Emit("action.ranged.fire", "Ranged", SemanticActionClassifier.RangedSubtype(item), item, itemValue, null);
        }
    }

    internal static class SemanticReloadTracker
    {
        private sealed class ReferenceComparer : IEqualityComparer<object>
        {
            internal static readonly ReferenceComparer Instance = new ReferenceComparer();
            public new bool Equals(object x, object y) { return ReferenceEquals(x, y); }
            public int GetHashCode(object obj) { return RuntimeHelpers.GetHashCode(obj); }
        }

        private sealed class ReloadInfo
        {
            internal string ItemName;
            internal ItemValue ItemValue;
            internal bool PoweredTool;
            internal string Topic;
            internal string Subtype;
        }

        private static readonly Dictionary<object, ReloadInfo> Active =
            new Dictionary<object, ReloadInfo>(ReferenceComparer.Instance);

        internal static void Begin(object data)
        {
            if (data == null || !SemanticActionClassifier.IsLocalObject(data)) return;

            string item = SemanticActionClassifier.ItemNameObject(data);
            ItemValue itemValue = SemanticActionClassifier.ItemValueObject(data);
            bool powered = SemanticActionClassifier.IsPoweredTool(item, itemValue);
            bool deployable = SemanticActionClassifier.IsDeployableBot(item);
            string topic = powered ? "PoweredTool" : (deployable ? "Deployable" : "Ranged");
            string subtype = powered
                ? SemanticActionClassifier.PoweredToolSubtype(item)
                : (deployable ? SemanticActionClassifier.DeployableSubtype(item) : SemanticActionClassifier.RangedSubtype(item));

            if (Active.ContainsKey(data)) return;
            Active[data] = new ReloadInfo
            {
                ItemName = item,
                ItemValue = itemValue,
                PoweredTool = powered,
                Topic = topic,
                Subtype = subtype
            };

            ActionSemanticTelemetry.Emit(
                powered ? "action.refuel.request" : "action.reload.request",
                topic,
                subtype,
                item,
                itemValue,
                null);
        }

        internal static void Complete(object data)
        {
            ReloadInfo info;
            if (data == null || !Active.TryGetValue(data, out info)) return;
            Active.Remove(data);

            ActionSemanticTelemetry.Emit(
                info.PoweredTool ? "action.refuel.complete" : "action.reload.complete",
                info.Topic,
                info.Subtype,
                info.ItemName,
                info.ItemValue,
                null);
        }

        internal static void Cancel(object data)
        {
            ReloadInfo info;
            if (data == null || !Active.TryGetValue(data, out info)) return;
            Active.Remove(data);

            ActionSemanticTelemetry.Emit(
                info.PoweredTool ? "action.refuel.cancel" : "action.reload.cancel",
                info.Topic,
                info.Subtype,
                info.ItemName,
                info.ItemValue,
                null);
        }
    }

    [HarmonyPatch(typeof(ItemActionRanged), "requestReload")]
    internal static class SemanticReloadRequestedPatch
    {
        private static void Postfix(object _adr)
        {
            SemanticReloadTracker.Begin(_adr);
        }
    }

    [HarmonyPatch(typeof(ItemActionRanged), "CompleteReload")]
    internal static class SemanticReloadCompletePatch
    {
        private static void Postfix(object _adr)
        {
            SemanticReloadTracker.Complete(_adr);
        }
    }

    [HarmonyPatch(typeof(ItemActionRanged), "CancelReload")]
    internal static class SemanticReloadCancelledPatch
    {
        private static void Postfix(object _data)
        {
            SemanticReloadTracker.Cancel(_data);
        }
    }

    [HarmonyPatch(typeof(XUiC_RecipeStack), "outputStack")]
    internal static class SemanticCraftCompletePatch
    {
        private static void Postfix(bool __result)
        {
            if (!__result) return;
            ActionSemanticTelemetry.Emit("action.craft.complete", "Craft", "Generic", "craft", null, null);
        }
    }

    [HarmonyPatch(typeof(EntityVehicle), "EnterVehicle")]
    internal static class SemanticVehicleEnteredPatch
    {
        private static void Postfix(EntityVehicle __instance, EntityAlive _entity)
        {
            if (__instance == null || !(_entity is EntityPlayerLocal)) return;
            if (_entity.AttachedToEntity != __instance) return;
            string subtype = SemanticActionClassifier.VehicleSubtype(__instance);
            ActionSemanticTelemetry.Emit("action.vehicle.enter", "Vehicle", subtype, __instance.GetType().Name, null, ActionSemanticTelemetry.VehicleExtra(__instance));
        }
    }
}

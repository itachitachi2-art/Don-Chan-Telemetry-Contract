using System;
using System.Collections.Generic;

namespace DonChan.TelemetryProbe
{
    internal sealed class SnapshotRoots
    {
        public object GameManager;
        public object World;
        public object Player;
        public object Inventory;
        public object HoldingItem;
        public object Bag;
        public object Equipment;
        public object Buffs;
        public object Progression;
        public object Stats;
        public object QuestJournal;
    }

    internal static class SnapshotCollector
    {
        private static string _lastRootSignature;

        public static SnapshotRoots ResolveRoots()
        {
            var roots = new SnapshotRoots();
            Type gmType = ReflectionUtil.FindType("GameManager");
            if (gmType == null) return roots;

            roots.GameManager = ReflectionUtil.ReadMember(gmType, "Instance", "instance");
            roots.World = ReflectionUtil.ReadMember(roots.GameManager, "World", "world");
            roots.Player = ReflectionUtil.InvokeNoArgs(roots.World, "GetPrimaryPlayer", "GetPrimaryPlayerLocal");
            if (roots.Player == null) roots.Player = ReflectionUtil.ReadMember(roots.World, "PrimaryPlayer", "primaryPlayer");

            roots.Inventory = ReflectionUtil.ReadMember(roots.Player, "Inventory", "inventory");
            roots.HoldingItem = ReflectionUtil.ReadMember(roots.Inventory, "holdingItemItemValue", "HoldingItemItemValue", "holdingItem", "HoldingItem");
            if (roots.HoldingItem == null) roots.HoldingItem = ReflectionUtil.InvokeNoArgs(roots.Inventory, "GetHoldingItemItemValue", "GetHoldingItem");
            roots.Bag = ReflectionUtil.ReadMember(roots.Player, "Bag", "bag");
            roots.Equipment = ReflectionUtil.ReadMember(roots.Player, "Equipment", "equipment");
            roots.Buffs = ReflectionUtil.ReadMember(roots.Player, "Buffs", "buffs");
            roots.Progression = ReflectionUtil.ReadMember(roots.Player, "Progression", "progression");
            roots.Stats = ReflectionUtil.ReadMember(roots.Player, "Stats", "stats", "EntityStats", "entityStats");
            roots.QuestJournal = ReflectionUtil.ReadMember(roots.Player, "QuestJournal", "questJournal", "Journal", "journal");
            return roots;
        }

        public static void CaptureLight(ProbeConfig config, SnapshotRoots roots)
        {
            if (roots == null) roots = ResolveRoots();
            var data = LightTelemetry.BuildSnapshot(roots);
            ProbeLog.PrepareTelemetryRecord(data);
            ProbeLog.WriteJson("snapshots/latest.json", data);
            ProbeLog.Snapshot(data);
            ReportRootsIfChanged(roots);
        }

        public static void CaptureAudit(ProbeConfig config, SnapshotRoots roots)
        {
            if (roots == null) roots = ResolveRoots();
            var rootTypes = RootTypes(roots);
            var data = new Dictionary<string, object>
            {
                { "utc", DateTime.UtcNow },
                { "rootTypes", rootTypes },
                { "gameManager", ReflectionUtil.Inspect(roots.GameManager, 1, 48, 8) },
                { "world", ReflectionUtil.Inspect(roots.World, 1, 64, 8) },
                { "player", ReflectionUtil.Inspect(roots.Player, config.SnapshotDepth, config.SnapshotMaxMembers, config.SnapshotMaxCollectionItems) },
                { "inventory", ReflectionUtil.Inspect(roots.Inventory, config.SnapshotDepth, config.SnapshotMaxMembers, config.SnapshotMaxCollectionItems) },
                { "holdingItem", ReflectionUtil.Inspect(roots.HoldingItem, config.SnapshotDepth, config.SnapshotMaxMembers, config.SnapshotMaxCollectionItems) },
                { "bag", ReflectionUtil.Inspect(roots.Bag, config.SnapshotDepth, config.SnapshotMaxMembers, config.SnapshotMaxCollectionItems) },
                { "equipment", ReflectionUtil.Inspect(roots.Equipment, config.SnapshotDepth, config.SnapshotMaxMembers, config.SnapshotMaxCollectionItems) },
                { "buffs", ReflectionUtil.Inspect(roots.Buffs, config.SnapshotDepth, config.SnapshotMaxMembers, config.SnapshotMaxCollectionItems) },
                { "progression", ReflectionUtil.Inspect(roots.Progression, config.SnapshotDepth, config.SnapshotMaxMembers, config.SnapshotMaxCollectionItems) },
                { "stats", ReflectionUtil.Inspect(roots.Stats, config.SnapshotDepth, config.SnapshotMaxMembers, config.SnapshotMaxCollectionItems) },
                { "questJournal", ReflectionUtil.Inspect(roots.QuestJournal, config.SnapshotDepth, config.SnapshotMaxMembers, config.SnapshotMaxCollectionItems) }
            };
            ProbeLog.PrepareTelemetryRecord(data);
            ProbeLog.WriteJson("audit/snapshots/latest.json", data);
            ProbeLog.AuditSnapshot(data);
            ReportRootsIfChanged(roots);
        }

        private static Dictionary<string, object> RootTypes(SnapshotRoots roots)
        {
            var d = new Dictionary<string, object>();
            d["gameManager"] = ReflectionUtil.TypeName(roots.GameManager);
            d["world"] = ReflectionUtil.TypeName(roots.World);
            d["player"] = ReflectionUtil.TypeName(roots.Player);
            d["inventory"] = ReflectionUtil.TypeName(roots.Inventory);
            d["holdingItem"] = ReflectionUtil.TypeName(roots.HoldingItem);
            d["bag"] = ReflectionUtil.TypeName(roots.Bag);
            d["equipment"] = ReflectionUtil.TypeName(roots.Equipment);
            d["buffs"] = ReflectionUtil.TypeName(roots.Buffs);
            d["progression"] = ReflectionUtil.TypeName(roots.Progression);
            d["stats"] = ReflectionUtil.TypeName(roots.Stats);
            d["questJournal"] = ReflectionUtil.TypeName(roots.QuestJournal);
            return d;
        }

        private static void ReportRootsIfChanged(SnapshotRoots roots)
        {
            var types = RootTypes(roots);
            string signature = JsonUtil.Serialize(types);
            if (string.Equals(signature, _lastRootSignature, StringComparison.Ordinal)) return;
            _lastRootSignature = signature;
            ProbeLog.Capability(new Dictionary<string, object>
            {
                { "utc", DateTime.UtcNow }, { "capability", "runtime_roots" }, { "ok", roots.Player != null }, { "roots", types }
            });
        }
    }
}

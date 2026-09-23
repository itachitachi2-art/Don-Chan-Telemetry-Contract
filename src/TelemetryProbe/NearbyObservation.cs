using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using DonChan.Shared;

namespace DonChan.TelemetryProbe
{
    // Raw observation contract. Classification and hostility interpretation are separate.
    internal static class NearbyObservation
    {
        private const int MaxRecords = 128;
        public static Dictionary<string, object> Collect(object world, object player)
        {
            var records = new List<object>();
            var d = new Dictionary<string, object> {
                { "schemaVersion", 1 }, { "rangeMeters", 15 }, { "distanceMetric", "horizontal_xz" },
                { "classificationStatus", "unknown" }, { "classifierVersion", NearbyTargetClassifier.Version }, { "scanComplete", false },
                { "recordsComplete", false }, { "records", records }
            };
            double px, pz;
            if (player == null || !Position(player, out px, out pz)) { d["failure"] = "player_position_unavailable"; return d; }
            object raw;
            if (!Read(world, new[] { "EntityAlives", "entityAlives" }, out raw) || !(raw is IEnumerable))
            { d["failure"] = "entity_list_unavailable"; return d; }
            object playerId;
            Read(player, new[] { "entityId", "EntityId" }, out playerId);
            int scanned = 0, unknown = 0, omitted = 0, unclassified = 0, targets = 0;
            try
            {
                foreach (object entity in (IEnumerable)raw)
                {
                    if (entity == null || object.ReferenceEquals(entity, player)) continue;
                    scanned++;
                    double x, z;
                    if (!Position(entity, out x, out z)) { unknown++; continue; }
                    double dx = x - px, dz = z - pz;
                    double distance = Math.Sqrt(dx * dx + dz * dz);
                    if (double.IsInfinity(distance) || double.IsNaN(distance)) { unknown++; continue; }
                    if (distance > 15.0) continue;
                    bool? dead = Dead(entity);
                    if (dead == true) continue;
                    if (!dead.HasValue) unknown++;
                    string className = NearbyTargetClassifier.ResolveClassName(entity);
                    string group = NearbyTargetClassifier.Classify(entity.GetType(), className);
                    if (group == "unknown") unclassified++;
                    if (dead == false && NearbyTargetClassifier.IsTarget(group)) targets++;
                    if (records.Count >= MaxRecords) { omitted++; continue; }
                    var r = new Dictionary<string, object> {
                        { "type", entity.GetType().FullName }, { "horizontalDistance", Math.Round(distance, 3) },
                        { "lifeState", dead.HasValue ? "alive" : "unknown" },
                        { "targetState", "unknown" }, { "group", group }, { "className", className }
                    };
                    object id, cls;
                    if (Read(entity, new[] { "entityId", "EntityId" }, out id) && id != null) r["entityId"] = id;
                    if (Read(entity, new[] { "entityClass", "EntityClass" }, out cls) && cls != null &&
                        (cls.GetType().IsPrimitive || cls is string)) r["entityClass"] = cls;
                    var hierarchy = new List<string>();
                    for (Type t = entity.GetType(); t != null && hierarchy.Count < 12; t = t.BaseType) hierarchy.Add(t.FullName);
                    r["typeHierarchy"] = hierarchy;
                    object target;
                    if (Read(entity, new[] { "attackTarget", "attackTargetClient" }, out target))
                    {
                        r["targetRead"] = "reference";
                        if (target == null) r["targetState"] = "none_observed";
                        else if (object.ReferenceEquals(target, player)) r["targetState"] = "player_reference";
                        else
                        {
                            object targetId;
                            if (Read(target, new[] { "entityId", "EntityId" }, out targetId) && targetId != null)
                            {
                                r["targetEntityId"] = targetId;
                                if (playerId != null) r["targetState"] = SameId(targetId, playerId) ? "player_reference" : "other_reference";
                            }
                        }
                    }
                    else
                    {
                        object targetId;
                        if (Read(entity, new[] { "attackTargetEntityId", "AttackTargetEntityId" }, out targetId) && targetId != null)
                        {
                            r["targetRead"] = "id";
                            r["targetEntityId"] = targetId;
                            // Sentinel IDs are not interpreted as a known absence.
                            if (playerId != null && SameId(targetId, playerId)) r["targetState"] = "player_reference";
                        }
                    }
                    records.Add(r);
                }
                d["scanComplete"] = true;
            }
            catch { d["failure"] = "enumeration_failed"; }
            d["scanned"] = scanned;
            d["classifiedAliveTargets"] = targets;
            d["unclassifiedRecords"] = unclassified;
            d["classificationStatus"] = (bool)d["scanComplete"] && unknown == 0 && unclassified == 0 ? "complete" : "partial";
            d["unknownReadings"] = unknown;
            d["omittedRecords"] = omitted;
            d["recordsComplete"] = (bool)d["scanComplete"] && unknown == 0 && omitted == 0;
            return d;
        }
        private static bool SameId(object a, object b) { return Convert.ToString(a, System.Globalization.CultureInfo.InvariantCulture) == Convert.ToString(b, System.Globalization.CultureInfo.InvariantCulture); }
        private static bool? Dead(object entity)
        {
            try
            {
                MethodInfo method = entity.GetType().GetMethod("IsDead", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
                if (method != null && method.ReturnType == typeof(bool)) return (bool)method.Invoke(entity, null);
            }
            catch { return null; }
            return null;
        }
        private static bool Position(object entity, out double x, out double z)
        {
            x = z = 0;
            object pos, ox, oz;
            if (!Read(entity, new[] { "position", "Position" }, out pos) || pos == null ||
                !Read(pos, new[] { "x", "X" }, out ox) || !Read(pos, new[] { "z", "Z" }, out oz) || ox == null || oz == null) return false;
            try { x = Convert.ToDouble(ox); z = Convert.ToDouble(oz); return !double.IsNaN(x) && !double.IsNaN(z) && !double.IsInfinity(x) && !double.IsInfinity(z); }
            catch { return false; }
        }
        private static bool Read(object target, string[] names, out object value)
        {
            value = null;
            if (target == null) return false;
            foreach (string name in names)
            {
                for (Type t = target.GetType(); t != null; t = t.BaseType)
                {
                    const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
                    try
                    {
                        FieldInfo f = t.GetField(name, flags);
                        if (f != null) { value = f.GetValue(target); return true; }
                        PropertyInfo p = t.GetProperty(name, flags);
                        if (p != null && p.GetIndexParameters().Length == 0) { value = p.GetValue(target, null); return true; }
                    }
                    catch { return false; }
                }
            }
            return false;
        }
    }
}

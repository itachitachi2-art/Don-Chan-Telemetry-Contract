using System;
using System.Collections;
using System.Reflection;

namespace DonChan.Shared
{
    internal static class NearbyTargetClassifier
    {
        internal const string Version = "six-groups-v1";
        // Contract identifiers, not display-name/free-text matching.
        internal static string Classify(Type runtimeType, string className)
        {
            switch (className)
            {
                case "animalZombieDog": return "zombie_dog";
                case "animalZombieBear": return "zombie_bear";
                case "animalZombieVulture":
                case "animalZombieVultureRadiated": return "vulture";
                case "animalDireWolf": return "dire_wolf";
                case "animalBossGrace": return "grace";
            }
            // Check the specific dog type before its possible zombie ancestor.
            if (HasAncestor(runtimeType, "EntityZombieDog")) return "zombie_dog";
            if (HasAncestor(runtimeType, "EntityVulture")) return "vulture";
            if (HasAncestor(runtimeType, "EntityZombie")) return "humanoid_zombie";
            switch (className)
            {
                case "animalBear": case "animalWolf": case "animalBoar":
                case "animalMountainLion": case "animalRabbit": case "animalChicken":
                case "animalStag": case "animalDoe": return "excluded";
            }
            return "unknown";
        }
        internal static bool IsTarget(string group) { return group != "unknown" && group != "excluded"; }
        private static bool HasAncestor(Type type, string name)
        {
            for (Type t = type; t != null; t = t.BaseType)
                if (t.FullName == name) return true;
            return false;
        }
        internal static string ResolveClassName(object entity)
        {
            if (entity == null) return null;
            try
            {
                object id = Member(entity, "entityClass");
                if (id == null) return null;
                Type definitionType = null;
                for (Type t = entity.GetType(); t != null && definitionType == null; t = t.BaseType)
                    definitionType = t.Assembly.GetType("EntityClass");
                if (definitionType == null) return null;
                FieldInfo field = definitionType.GetField("list", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                if (field == null) return null;
                IDictionary registry = field.GetValue(null) as IDictionary;
                if (registry == null || !registry.Contains(id)) return null;
                return Member(registry[id], "entityClassName") as string;
            }
            catch { return null; }
        }
        private static object Member(object obj, string name)
        {
            if (obj == null) return null;
            for (Type t = obj.GetType(); t != null; t = t.BaseType)
            {
                const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly;
                FieldInfo f = t.GetField(name, flags);
                if (f != null) return f.GetValue(obj);
                PropertyInfo p = t.GetProperty(name, flags);
                if (p != null && p.GetIndexParameters().Length == 0) return p.GetValue(obj, null);
            }
            return null;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace DonChan.TelemetryProbe
{
    internal static class AssemblyCatalog
    {
        private static readonly string[] Keywords =
        {
            "Entity", "Player", "World", "Inventory", "Bag", "Equipment", "Item", "Block", "Chunk",
            "Quest", "Challenge", "Progression", "Buff", "Stat", "Vehicle", "Trader", "Craft", "Recipe",
            "Workstation", "Weather", "Biome", "GamePref", "GameStat", "GameEvent", "Damage", "Loot", "XUiM_"
        };

        public static void Dump(ProbeConfig config)
        {
            Assembly asm = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => string.Equals(a.GetName().Name, "Assembly-CSharp", StringComparison.OrdinalIgnoreCase));
            if (asm == null)
            {
                ProbeLog.Capability(Capability("assembly_catalog", false, "Assembly-CSharp not loaded"));
                return;
            }

            Type[] types = SafeTypes(asm).OrderBy(t => t.FullName).ToArray();
            if (config.DumpAssemblyCatalogOnStart)
            {
                var index = types.Select(t => new Dictionary<string, object>
                {
                    { "fullName", t.FullName }, { "name", t.Name }, { "baseType", t.BaseType == null ? null : t.BaseType.FullName },
                    { "isPublic", t.IsPublic || t.IsNestedPublic }, { "isEnum", t.IsEnum }, { "isInterface", t.IsInterface }
                }).ToList<object>();
                ProbeLog.WriteJson("catalog/assembly-types.json", new Dictionary<string, object>
                {
                    { "generatedUtc", DateTime.UtcNow }, { "assembly", asm.FullName }, { "typeCount", types.Length }, { "types", index }
                });
            }

            if (config.DumpRelevantMembersOnStart)
            {
                var relevant = types.Where(IsRelevant).Select(t => DescribeType(t, config.CatalogMaxMethodsPerType)).ToList<object>();
                ProbeLog.WriteJson("catalog/relevant-members.json", new Dictionary<string, object>
                {
                    { "generatedUtc", DateTime.UtcNow }, { "typeCount", relevant.Count }, { "types", relevant }
                });
            }

            ProbeLog.Capability(Capability("assembly_catalog", true, "types=" + types.Length));
        }

        private static bool IsRelevant(Type type)
        {
            string n = type.FullName ?? type.Name;
            return Keywords.Any(k => n.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static object DescribeType(Type type, int maxMethods)
        {
            var fields = Safe(() => type.GetFields(ReflectionUtil.All)).Select(f => new Dictionary<string, object>
            {
                { "name", f.Name }, { "type", Friendly(f.FieldType) }, { "static", f.IsStatic }, { "public", f.IsPublic },
                { "delegate", typeof(Delegate).IsAssignableFrom(f.FieldType) }
            }).ToList<object>();

            var props = Safe(() => type.GetProperties(ReflectionUtil.All)).Select(p => new Dictionary<string, object>
            {
                { "name", p.Name }, { "type", Friendly(p.PropertyType) }, { "read", p.CanRead }, { "write", p.CanWrite },
                { "indexer", p.GetIndexParameters().Length > 0 }
            }).ToList<object>();

            var events = Safe(() => type.GetEvents(ReflectionUtil.All)).Select(e => new Dictionary<string, object>
            {
                { "name", e.Name }, { "handlerType", Friendly(e.EventHandlerType) }
            }).ToList<object>();

            var methods = Safe(() => type.GetMethods(ReflectionUtil.All)).Where(m => !m.IsSpecialName).Take(maxMethods).Select(m => new Dictionary<string, object>
            {
                { "name", m.Name }, { "returnType", Friendly(m.ReturnType) }, { "static", m.IsStatic }, { "public", m.IsPublic },
                { "parameters", m.GetParameters().Select(p => (object)new Dictionary<string, object>{{"name", p.Name},{"type", Friendly(p.ParameterType)}}).ToList() }
            }).ToList<object>();

            return new Dictionary<string, object>
            {
                { "fullName", type.FullName }, { "baseType", type.BaseType == null ? null : type.BaseType.FullName },
                { "fields", fields }, { "properties", props }, { "events", events }, { "methods", methods }
            };
        }

        private static Type[] SafeTypes(Assembly asm)
        {
            try { return asm.GetTypes(); }
            catch (ReflectionTypeLoadException ex) { return ex.Types.Where(t => t != null).ToArray(); }
        }

        private static T[] Safe<T>(Func<T[]> getter)
        {
            try { return getter(); } catch { return new T[0]; }
        }

        private static string Friendly(Type t)
        {
            if (t == null) return null;
            if (!t.IsGenericType) return t.FullName ?? t.Name;
            string root = t.GetGenericTypeDefinition().FullName;
            root = root == null ? t.Name : root.Substring(0, root.IndexOf('`'));
            return root + "<" + string.Join(",", t.GetGenericArguments().Select(Friendly).ToArray()) + ">";
        }

        private static Dictionary<string, object> Capability(string name, bool ok, string detail)
        {
            return new Dictionary<string, object> { { "utc", DateTime.UtcNow }, { "capability", name }, { "ok", ok }, { "detail", detail } };
        }
    }
}

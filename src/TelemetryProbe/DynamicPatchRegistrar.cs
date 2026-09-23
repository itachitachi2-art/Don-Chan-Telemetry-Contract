using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace DonChan.TelemetryProbe
{
    internal static class DynamicPatchRegistrar
    {
        private static ProbeConfig _config;
        private static Harmony _harmony;

        private static readonly Tuple<string, string, string>[] Candidates =
        {
            Tuple.Create("EntityAlive", "DamageEntity", "combat.damage"),
            Tuple.Create("EntityAlive", "Kill", "combat.kill"),
            Tuple.Create("EntityAlive", "ProcessDamageResponseLocal", "combat.response"),
            Tuple.Create("EntityAlive", "OnEntityDeath", "combat.death"),
            Tuple.Create("ItemActionAttack", "Hit", "combat.hit"),
            Tuple.Create("EntityBuffs", "AddBuff", "buff.add"),
            Tuple.Create("EntityBuffs", "RemoveBuff", "buff.remove")
        };

        public static void Initialize(ProbeConfig config)
        {
            _config = config;
            _harmony = new Harmony("itachi.donchan.telemetryprobe");

            PatchHeartbeat();
            ActionSemanticTelemetry.Initialize(_harmony, _config);
            if (!config.EnableHarmonyEventFallbacks) return;
            foreach (var c in Candidates) PatchAll(c.Item1, c.Item2, c.Item3);
        }

        private static void PatchHeartbeat()
        {
            Type type = ReflectionUtil.FindType("GameManager");
            if (type == null) { Capability("patch", "GameManager.Update", false, "type-not-found"); return; }
            MethodInfo method = type.GetMethods(ReflectionUtil.AllInstance).FirstOrDefault(m => m.Name == "Update" && m.GetParameters().Length == 0);
            if (method == null) { Capability("patch", "GameManager.Update", false, "method-not-found"); return; }
            try
            {
                var postfix = new HarmonyMethod(typeof(DynamicPatchRegistrar).GetMethod("HeartbeatPostfix", BindingFlags.Static | BindingFlags.NonPublic));
                _harmony.Patch(method, postfix: postfix);
                Capability("patch", "GameManager.Update", true, method.ToString());
            }
            catch (Exception ex) { Capability("patch", "GameManager.Update", false, ex.GetType().Name + ":" + ex.Message); }
        }

        private static void PatchAll(string typeName, string methodName, string eventName)
        {
            Type type = ReflectionUtil.FindType(typeName);
            if (type == null) { Capability("patch", typeName + "." + methodName, false, "type-not-found"); return; }
            MethodInfo[] methods;
            try { methods = type.GetMethods(ReflectionUtil.All).Where(m => m.Name == methodName && !m.ContainsGenericParameters && !m.IsAbstract).ToArray(); }
            catch (Exception ex) { Capability("patch", typeName + "." + methodName, false, ex.Message); return; }

            if (methods.Length == 0) { Capability("patch", typeName + "." + methodName, false, "method-not-found"); return; }
            foreach (MethodInfo method in methods)
            {
                try
                {
                    var postfix = new HarmonyMethod(typeof(DynamicPatchRegistrar).GetMethod("GenericEventPostfix", BindingFlags.Static | BindingFlags.NonPublic));
                    var prefix = eventName == "combat.damage" ? new HarmonyMethod(typeof(DynamicPatchRegistrar).GetMethod("DamagePrefix", BindingFlags.Static | BindingFlags.NonPublic)) : null;
                    var finalizer = eventName == "combat.damage" ? new HarmonyMethod(typeof(DynamicPatchRegistrar).GetMethod("DamageFinalizer", BindingFlags.Static | BindingFlags.NonPublic)) : null;
                    _harmony.Patch(method, prefix: prefix, postfix: postfix, finalizer: finalizer);
                    PatchLabels[method] = eventName;
                    Capability("patch", typeName + "." + method, true, eventName);
                }
                catch (Exception ex) { Capability("patch", typeName + "." + method, false, ex.GetType().Name + ":" + ex.Message); }
            }
        }

        internal static string DamageActionId { get { return DamageActionScope.Current; } }
        private static void DamagePrefix(out DamageActionScope __state) {
            __state = DamageActionScope.Begin(ProbeLog.SessionId);
        }
        private static Exception DamageFinalizer(Exception __exception, DamageActionScope __state) {
            return DamageActionScope.End(__state, __exception);
        }
        private static readonly Dictionary<MethodBase, string> PatchLabels = new Dictionary<MethodBase, string>();

        private static void HeartbeatPostfix()
        {
            TelemetryRuntime.Tick();
        }

        private static void GenericEventPostfix(MethodBase __originalMethod, object __instance, object[] __args)
        {
            try
            {
                string label;
                if (!PatchLabels.TryGetValue(__originalMethod, out label)) label = __originalMethod.DeclaringType.FullName + "." + __originalMethod.Name;

                if (_config.EnableCombatValidation && label.StartsWith("combat.", StringComparison.Ordinal))
                    CombatValidation.Capture(label, __originalMethod, __instance, __args);

                if (_config.EnableNormalizedEvents && CombatEventPolicy.IsRealEvent(label, __originalMethod, __args) && (!_config.SuppressNoisyEvents || EventNormalizer.ShouldEmitHarmony(label, __args)))
                    ProbeLog.Event(EventNormalizer.NormalizeHarmony(label, __originalMethod, __instance, __args));

                if (_config.AuditMode && _config.EnableRawAuditEvents)
                {
                    var args = new List<object>();
                    int count = Math.Min(__args == null ? 0 : __args.Length, 16);
                    for (int i = 0; i < count; i++) args.Add(ReflectionUtil.Inspect(__args[i], _config.EventArgDepth, _config.EventArgMaxMembers, _config.EventArgMaxCollectionItems));
                    ProbeLog.AuditEvent(new Dictionary<string, object>
                    {
                        { "utc", DateTime.UtcNow }, { "kind", "harmony-postfix" }, { "event", label },
                        { "method", __originalMethod.DeclaringType.FullName + "." + __originalMethod.Name },
                        { "instance", ReflectionUtil.Inspect(__instance, 0, 4, 2) }, { "args", args }
                    });
                }
            }
            catch (Exception ex) { ProbeLog.Warn("Harmony event sink failed: " + ex.Message); }
        }

        private static void Capability(string kind, string name, bool ok, string detail)
        {
            ProbeLog.Capability(new Dictionary<string, object>
            {
                { "utc", DateTime.UtcNow }, { "capability", kind }, { "name", name }, { "ok", ok }, { "detail", detail }
            });
        }
    }
}

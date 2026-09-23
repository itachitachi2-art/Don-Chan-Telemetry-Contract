using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
namespace DonChan.TelemetryProbe
{
    internal static class CombatValidation
    {
        public const string Version = "combat-validation-20260923-2";
        public static bool Enabled;
        public static int Step;
        private static readonly string[] Labels = {
            "ready", "baseline", "humanoid", "zombie_dog", "zombie_bear", "vulture",
            "dire_wolf", "grace", "melee_a_torso", "melee_a_head", "melee_b_torso",
            "melee_b_head", "gun_torso", "gun_head", "distant_unalerted", "height_and_target", "end"
        };
        public static void Initialize(bool enabled)
        {
            Enabled = enabled;
            if (!enabled) return;
            var assemblies = new List<object>();
            foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
                if (a.GetName().Name == "Assembly-CSharp" || a == typeof(CombatValidation).Assembly || a.GetName().Name == "0Harmony")
                    assemblies.Add(new Dictionary<string, object> { { "name", a.FullName }, { "mvid", a.ManifestModule.ModuleVersionId.ToString() } });
            var parts = new Dictionary<string, object>();
            Type t = ReflectionUtil.FindType("EnumBodyPartHit");
            if (t != null && t.IsEnum) foreach (object v in Enum.GetValues(t)) parts[v.ToString()] = Convert.ToInt64(v);
            ProbeLog.WriteJson("validation.json", new Dictionary<string, object> {
                { "version", Version }, { "sessionId", ProbeLog.SessionId }, { "assemblies", assemblies },
                { "bodyPartEnum", parts }, { "steps", Labels } });
            ProbeLog.Info("VALIDATION ready. F9 = next step; F10 = repeat marker. See TEST-STEPS.md.");
        }
        public static void Tick()
        {
            if (!Enabled) return;
            try {
                bool next = Input.GetKeyDown(KeyCode.F9);
                bool repeat = Input.GetKeyDown(KeyCode.F10);
                if (!next && !repeat) return;
                var roots = SnapshotCollector.ResolveRoots();
                if (roots.Player == null) { ProbeLog.Warn("Validation marker ignored: no local player."); return; }
                if (next && Step < Labels.Length - 1) Step++;
                ProbeLog.Event(new Dictionary<string, object> { { "event", "test.marker" }, { "utc", DateTime.UtcNow },
                    { "step", Step }, { "label", Labels[Step] }, { "repeat", !next }, { "player", LightTelemetry.EntitySummary(roots.Player) } });
                ProbeLog.Info("VALIDATION STEP " + Step + " / " + Labels[Step]);
                SnapshotCollector.CaptureLight(null, roots);
            } catch (Exception ex) { ProbeLog.Warn("Validation marker failed: " + ex.Message); }
        }
        private static object Fields(object value, string[] names)
        {
            if (value == null) return null;
            var d = new Dictionary<string, object>();
            d["type"] = value.GetType().FullName;
            foreach (string n in names) {
                object v = LightTelemetry.Read(value, n);
                d[n] = v is Enum ? v.ToString() : v;
            }
            return d;
        }
        private static object Source(object value)
        {
            return Fields(value, new [] { "damageSource", "damageType", "bodyParts", "ownerEntityId", "CreatorEntityId", "bTrapKillXP", "BuffClass" });
        }
        public static void Capture(string label, MethodBase method, object instance, object[] args)
        {
            var values = new List<object>();
            var pars = method.GetParameters();
            for (int i = 0; args != null && i < args.Length; i++) {
                object a = args[i]; object value = null;
                if (a != null) {
                    Type t = a.GetType();
                    if (t.IsPrimitive || a is string || t.IsEnum) value = t.IsEnum ? a.ToString() : a;
                    else if (t.Name == "DamageResponse") {
                        var r = (Dictionary<string, object>)Fields(a, new [] { "Strength", "ModStrength", "Fatal", "Critical", "HitBodyPart", "Dismember" });
                        r["source"] = Source(LightTelemetry.Read(a, "Source")); value = r;
                    } else if (t.Name.StartsWith("DamageSource", StringComparison.Ordinal)) value = Source(a);
                    else if (t.Name == "AttackHitInfo") value = Fields(a, new [] { "damageGiven", "bKilled", "bBlockHit", "isCriticalHit" });
                    else value = new Dictionary<string, object> { { "type", t.FullName } };
                }
                values.Add(new Dictionary<string, object> { { "name", i < pars.Length ? pars[i].Name : "?" }, { "value", value } });
            }
            ProbeLog.Event(new Dictionary<string, object> { { "event", "validation.combat" }, { "utc", DateTime.UtcNow },
                { "sourceEvent", label }, { "signature", method.DeclaringType.FullName + "." + method },
                { "instance", instance == null ? null : LightTelemetry.EntitySummary(instance) }, { "arguments", values } });
        }
    }
}

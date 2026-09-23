using System;
using System.Reflection;
namespace DonChan.TelemetryProbe {
    internal static class CombatEventPolicy {
        internal static object NamedArgument(MethodBase method, object[] args, string name) {
            if (method == null || args == null) return null;
            var ps = method.GetParameters();
            for (int i=0; i<ps.Length && i<args.Length; i++) if (ps[i].Name == name) return args[i];
            return null;
        }
        internal static bool IsRealEvent(string label, MethodBase method, object[] args) {
            if (label != "combat.hit") return true;
            object mode = NamedArgument(method, args, "_attackMode");
            return mode != null && (mode.ToString() == "RealNoHarvesting" || mode.ToString() == "RealAndHarvesting");
        }
    }
}

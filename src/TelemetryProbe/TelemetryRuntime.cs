using System;
using System.Collections.Generic;

namespace DonChan.TelemetryProbe
{
    internal static class TelemetryRuntime
    {
        private static bool _initialized;
        private static ProbeConfig _config;
        private static DateTime _nextSnapshotUtc;
        private static DateTime _nextAuditSnapshotUtc;
        private static DateTime _nextSubscriptionUtc;
        private static bool _catalogDumped;
        private static readonly object Sync = new object();

        public static void Initialize(Mod mod)
        {
            lock (Sync)
            {
                if (_initialized) return;
                ProbeLog.Initialize(mod.Path);
                _config = ProbeConfig.Load(mod.Path);
                _nextSnapshotUtc = DateTime.UtcNow;
                _nextAuditSnapshotUtc = DateTime.UtcNow;
                _nextSubscriptionUtc = DateTime.UtcNow;

                ProbeLog.WriteJson("session.json", new Dictionary<string, object>
                {
                    { "schemaVersion", ProbeLog.SchemaVersion },
                    { "sessionId", ProbeLog.SessionId },
                    { "startedUtc", DateTime.UtcNow },
                    { "modName", mod.Name },
                    { "modPath", mod.Path },
                    { "mode", _config.AuditMode ? "audit" : "light" },
                    { "assemblyCSharp", FindAssemblyVersion("Assembly-CSharp") },
                    { "probeAssembly", typeof(TelemetryRuntime).Assembly.FullName }
                });

                if (_config.EnableEventSubscriptions) EventSubscriber.Initialize(_config);
                DynamicPatchRegistrar.Initialize(_config);
                _initialized = true;
                ProbeLog.Info("Initialized in " + (_config.AuditMode ? "AUDIT" : "LIGHT") + " mode. Telemetry=" + ProbeLog.LogDirectory);
            }
        }

        public static void Tick()
        {
            if (!_initialized) return;
            DateTime now = DateTime.UtcNow;
            try
            {
                if (!_catalogDumped)
                {
                    _catalogDumped = true;
                    if (_config.AuditMode) AssemblyCatalog.Dump(_config);
                    else ProbeLog.Capability(new Dictionary<string, object>
                    {
                        { "utc", now }, { "capability", "assembly_catalog" }, { "ok", true }, { "detail", "skipped-in-light-mode" }
                    });
                }

                SnapshotRoots roots = null;
                if (_config.EnableEventSubscriptions && now >= _nextSubscriptionUtc)
                {
                    _nextSubscriptionUtc = now.AddSeconds(Math.Max(0.5, _config.InstanceSubscriptionIntervalSeconds));
                    roots = SnapshotCollector.ResolveRoots();
                    EventSubscriber.RefreshInstanceSubscriptions(roots);
                }

                if (_config.EnableLightSnapshots && now >= _nextSnapshotUtc)
                {
                    _nextSnapshotUtc = now.AddSeconds(Math.Max(0.25, _config.SnapshotIntervalSeconds));
                    if (roots == null) roots = SnapshotCollector.ResolveRoots();
                    SnapshotCollector.CaptureLight(_config, roots);
                }

                if (_config.AuditMode && _config.EnableAuditSnapshots && now >= _nextAuditSnapshotUtc)
                {
                    _nextAuditSnapshotUtc = now.AddSeconds(Math.Max(1.0, _config.AuditSnapshotIntervalSeconds));
                    if (roots == null) roots = SnapshotCollector.ResolveRoots();
                    SnapshotCollector.CaptureAudit(_config, roots);
                }
            }
            catch (Exception ex)
            {
                ProbeLog.Error("Tick failed: " + ex);
                ProbeLog.Capability(new Dictionary<string, object> { { "utc", now }, { "capability", "tick" }, { "ok", false }, { "detail", ex.ToString() } });
            }
        }

        private static string FindAssemblyVersion(string name)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                if (string.Equals(asm.GetName().Name, name, StringComparison.OrdinalIgnoreCase)) return asm.FullName;
            return null;
        }
    }
}

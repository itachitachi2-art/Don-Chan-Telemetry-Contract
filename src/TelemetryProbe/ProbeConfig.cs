using System;
using System.Globalization;
using System.IO;
using System.Xml.Linq;

namespace DonChan.TelemetryProbe
{
    internal sealed class ProbeConfig
    {
        public bool AuditMode = false;
        public bool EnableLightSnapshots = true;
        public bool EnableNormalizedEvents = true;
        public bool EnableSemanticActionEvents = true;
        public bool EnableRawAuditEvents = true;
        public bool EnableAuditSnapshots = true;
        public bool SuppressNoisyEvents = true;

        public double SnapshotIntervalSeconds = 1.0;
        public double AuditSnapshotIntervalSeconds = 5.0;
        public double InstanceSubscriptionIntervalSeconds = 2.0;

        public bool DumpAssemblyCatalogOnStart = true;
        public bool DumpRelevantMembersOnStart = true;
        public bool EnableEventSubscriptions = true;
        public bool EnableHarmonyEventFallbacks = true;

        public int SnapshotDepth = 2;
        public int SnapshotMaxMembers = 96;
        public int SnapshotMaxCollectionItems = 24;
        public int EventArgDepth = 1;
        public int EventArgMaxMembers = 24;
        public int EventArgMaxCollectionItems = 8;
        public int CatalogMaxMethodsPerType = 300;

        public static ProbeConfig Load(string modPath)
        {
            var config = new ProbeConfig();
            string path = Path.Combine(modPath, "TelemetryConfig", "telemetryprobe.xml");
            if (!File.Exists(path)) return config;

            try
            {
                var root = XDocument.Load(path).Root;
                if (root == null) return config;
                config.AuditMode = Bool(root, "AuditMode", config.AuditMode);
                config.EnableLightSnapshots = Bool(root, "EnableLightSnapshots", config.EnableLightSnapshots);
                config.EnableNormalizedEvents = Bool(root, "EnableNormalizedEvents", config.EnableNormalizedEvents);
                config.EnableSemanticActionEvents = Bool(root, "EnableSemanticActionEvents", config.EnableSemanticActionEvents);
                config.EnableRawAuditEvents = Bool(root, "EnableRawAuditEvents", config.EnableRawAuditEvents);
                config.EnableAuditSnapshots = Bool(root, "EnableAuditSnapshots", config.EnableAuditSnapshots);
                config.SuppressNoisyEvents = Bool(root, "SuppressNoisyEvents", config.SuppressNoisyEvents);
                config.SnapshotIntervalSeconds = Double(root, "SnapshotIntervalSeconds", config.SnapshotIntervalSeconds);
                config.AuditSnapshotIntervalSeconds = Double(root, "AuditSnapshotIntervalSeconds", config.AuditSnapshotIntervalSeconds);
                config.InstanceSubscriptionIntervalSeconds = Double(root, "InstanceSubscriptionIntervalSeconds", config.InstanceSubscriptionIntervalSeconds);
                config.DumpAssemblyCatalogOnStart = Bool(root, "DumpAssemblyCatalogOnStart", config.DumpAssemblyCatalogOnStart);
                config.DumpRelevantMembersOnStart = Bool(root, "DumpRelevantMembersOnStart", config.DumpRelevantMembersOnStart);
                config.EnableEventSubscriptions = Bool(root, "EnableEventSubscriptions", config.EnableEventSubscriptions);
                config.EnableHarmonyEventFallbacks = Bool(root, "EnableHarmonyEventFallbacks", config.EnableHarmonyEventFallbacks);
                config.SnapshotDepth = Int(root, "SnapshotDepth", config.SnapshotDepth);
                config.SnapshotMaxMembers = Int(root, "SnapshotMaxMembers", config.SnapshotMaxMembers);
                config.SnapshotMaxCollectionItems = Int(root, "SnapshotMaxCollectionItems", config.SnapshotMaxCollectionItems);
                config.EventArgDepth = Int(root, "EventArgDepth", config.EventArgDepth);
                config.EventArgMaxMembers = Int(root, "EventArgMaxMembers", config.EventArgMaxMembers);
                config.EventArgMaxCollectionItems = Int(root, "EventArgMaxCollectionItems", config.EventArgMaxCollectionItems);
                config.CatalogMaxMethodsPerType = Int(root, "CatalogMaxMethodsPerType", config.CatalogMaxMethodsPerType);
            }
            catch (Exception ex)
            {
                Log.Warning("[DonChanTelemetryProbe] Config load failed; defaults are used: " + ex.Message);
            }
            return config;
        }

        private static bool Bool(XElement root, string name, bool fallback)
        {
            bool value;
            var e = root.Element(name);
            return e != null && bool.TryParse(e.Value, out value) ? value : fallback;
        }

        private static int Int(XElement root, string name, int fallback)
        {
            int value;
            var e = root.Element(name);
            return e != null && int.TryParse(e.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out value) ? value : fallback;
        }

        private static double Double(XElement root, string name, double fallback)
        {
            double value;
            var e = root.Element(name);
            return e != null && double.TryParse(e.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out value) ? value : fallback;
        }
    }
}

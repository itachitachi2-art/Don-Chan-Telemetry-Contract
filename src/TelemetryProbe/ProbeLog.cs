using System;
using System.IO;
using System.Text;

namespace DonChan.TelemetryProbe
{
    internal static class ProbeLog
    {
        private static readonly object Sync = new object();
        private static string _logDir;
        private static string _eventPath;
        private static string _capabilityPath;
        private static string _snapshotPath;
        private static string _auditEventPath;
        private static string _auditSnapshotPath;
        private static string _sessionId;
        private static long _sequence;

        public const int SchemaVersion = 1;
        public static string LogDirectory { get { return _logDir; } }
        public static string SessionId { get { return _sessionId; } }

        public static void Initialize(string modPath)
        {
            _sessionId = Guid.NewGuid().ToString("N");
            _sequence = 0;
            _logDir = Path.Combine(modPath, "Telemetry");
            Directory.CreateDirectory(_logDir);
            Directory.CreateDirectory(Path.Combine(_logDir, "catalog"));
            Directory.CreateDirectory(Path.Combine(_logDir, "snapshots"));
            Directory.CreateDirectory(Path.Combine(_logDir, "audit"));
            Directory.CreateDirectory(Path.Combine(_logDir, "audit", "snapshots"));
            _eventPath = Path.Combine(_logDir, "events.jsonl");
            _capabilityPath = Path.Combine(_logDir, "capabilities.jsonl");
            _snapshotPath = Path.Combine(_logDir, "snapshots.jsonl");
            _auditEventPath = Path.Combine(_logDir, "audit", "events.raw.jsonl");
            _auditSnapshotPath = Path.Combine(_logDir, "audit", "snapshots.jsonl");
        }

        public static void Info(string message) { Log.Out("[DonChanTelemetryProbe] " + message); }
        public static void Warn(string message) { Log.Warning("[DonChanTelemetryProbe] " + message); }
        public static void Error(string message) { Log.Error("[DonChanTelemetryProbe] " + message); }

        public static void Event(object payload) { AppendTelemetryJsonLine(_eventPath, payload); }
        public static void AuditEvent(object payload) { AppendTelemetryJsonLine(_auditEventPath, payload); }
        public static void Capability(object payload) { AppendJsonLine(_capabilityPath, payload); }
        public static void Snapshot(object payload) { AppendTelemetryJsonLine(_snapshotPath, payload); }
        public static void AuditSnapshot(object payload) { AppendTelemetryJsonLine(_auditSnapshotPath, payload); }

        public static void PrepareTelemetryRecord(object payload)
        {
            lock (Sync) StampTelemetryRecordUnsafe(payload);
        }

        public static void WriteJson(string relativePath, object payload)
        {
            string full = Path.Combine(_logDir, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(full));
            string tmp = full + ".tmp";
            lock (Sync)
            {
                File.WriteAllText(tmp, JsonUtil.Serialize(payload), new UTF8Encoding(false));
                if (File.Exists(full)) File.Delete(full);
                File.Move(tmp, full);
            }
        }

        private static void AppendTelemetryJsonLine(string path, object payload)
        {
            if (string.IsNullOrEmpty(path)) return;
            lock (Sync)
            {
                StampTelemetryRecordUnsafe(payload);
                string line = JsonUtil.Serialize(payload) + Environment.NewLine;
                File.AppendAllText(path, line, new UTF8Encoding(false));
            }
        }

        private static void StampTelemetryRecordUnsafe(object payload)
        {
            var record = payload as System.Collections.Generic.IDictionary<string, object>;
            if (record == null) return;
            if (!record.ContainsKey("schemaVersion")) record["schemaVersion"] = SchemaVersion;
            if (!record.ContainsKey("sessionId")) record["sessionId"] = _sessionId;
            if (!record.ContainsKey("sequence")) record["sequence"] = ++_sequence;
            if (!record.ContainsKey("observedAt"))
            {
                object observed = null;
                if (record.ContainsKey("utc")) observed = record["utc"];
                record["observedAt"] = observed ?? (object)DateTime.UtcNow;
            }
        }

        private static void AppendJsonLine(string path, object payload)
        {
            if (string.IsNullOrEmpty(path)) return;
            string line = JsonUtil.Serialize(payload) + Environment.NewLine;
            lock (Sync)
            {
                File.AppendAllText(path, line, new UTF8Encoding(false));
            }
        }
    }
}

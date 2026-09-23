using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace DonChan.ZombieRadar
{
    internal sealed class RadarConfig
    {
        internal bool Enabled = true;
        internal KeyCode ToggleKey = KeyCode.F7;
        internal KeyCode PlacementKey = KeyCode.F8;
        internal float RangeMeters = 15f;
        internal float ScanIntervalSeconds = 0.10f;
        internal float DeathMarkerSeconds = 2.50f;
        internal float RadarSize = 420f;
        internal float PositionX = -450f;
        internal float PositionY = 30f;
        internal bool AnchorRight = true;
        internal float GridMeters = 5f;
        internal float ZombieDotSize = 18f;
        internal float SelfDotSize = 8f;
        internal bool RotateWithPlayer = true;
        internal bool ShowLabels = true;
        internal float SweepCycleSeconds = 2.40f;
        internal float SweepTrailDegrees = 34f;
        internal float BlipPersistenceSeconds = 1.50f;

        internal static RadarConfig Load(string modPath)
        {
            RadarConfig cfg = new RadarConfig();
            string path = Path.Combine(modPath, "Settings", "DonChanZombieRadar.cfg");
            if (!File.Exists(path)) return cfg;

            Dictionary<string, string> values = ReadValues(path);

            cfg.Enabled = GetBool(values, "Enabled", cfg.Enabled);
            cfg.ToggleKey = GetKey(values, "ToggleKey", cfg.ToggleKey);
            cfg.PlacementKey = GetKey(values, "PlacementKey", cfg.PlacementKey);
            cfg.RangeMeters = GetFloat(values, "RangeMeters", cfg.RangeMeters, 3f, 100f);
            cfg.ScanIntervalSeconds = GetFloat(values, "ScanIntervalSeconds", cfg.ScanIntervalSeconds, 0.02f, 2f);
            cfg.DeathMarkerSeconds = GetFloat(values, "DeathMarkerSeconds", cfg.DeathMarkerSeconds, 0.1f, 30f);
            cfg.RadarSize = GetFloat(values, "RadarSize", cfg.RadarSize, 120f, 800f);
            cfg.PositionX = GetFloat(values, "PositionX", cfg.PositionX, -4000f, 4000f);
            cfg.PositionY = GetFloat(values, "PositionY", cfg.PositionY, -4000f, 4000f);
            cfg.AnchorRight = GetBool(values, "AnchorRight", cfg.AnchorRight);
            cfg.GridMeters = GetFloat(values, "GridMeters", cfg.GridMeters, 1f, cfg.RangeMeters);
            cfg.ZombieDotSize = GetFloat(values, "ZombieDotSize", cfg.ZombieDotSize, 3f, 30f);
            cfg.SelfDotSize = GetFloat(values, "SelfDotSize", cfg.SelfDotSize, 3f, 30f);
            cfg.RotateWithPlayer = GetBool(values, "RotateWithPlayer", cfg.RotateWithPlayer);
            cfg.ShowLabels = GetBool(values, "ShowLabels", cfg.ShowLabels);
            cfg.SweepCycleSeconds = GetFloat(values, "SweepCycleSeconds", cfg.SweepCycleSeconds, 0.5f, 10f);
            cfg.SweepTrailDegrees = GetFloat(values, "SweepTrailDegrees", cfg.SweepTrailDegrees, 0f, 120f);
            cfg.BlipPersistenceSeconds = GetFloat(values, "BlipPersistenceSeconds", cfg.BlipPersistenceSeconds, 0.1f, 10f);
            LoadLayout(cfg);
            return cfg;
        }

        internal static void SaveLayout(RadarConfig cfg)
        {
            if (cfg == null) return;
            string path = GetLayoutPath();
            string dir = Path.GetDirectoryName(path);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            string[] lines = new string[]
            {
                "# Don-Chan Zombie Radar user layout. Preserved across mod reinstalls.",
                "AnchorRight=" + cfg.AnchorRight.ToString().ToLowerInvariant(),
                "PositionX=" + cfg.PositionX.ToString("0.##", CultureInfo.InvariantCulture),
                "PositionY=" + cfg.PositionY.ToString("0.##", CultureInfo.InvariantCulture)
            };
            File.WriteAllLines(path, lines);
        }

        private static void LoadLayout(RadarConfig cfg)
        {
            string path = GetLayoutPath();
            if (!File.Exists(path)) return;
            Dictionary<string, string> values = ReadValues(path);
            cfg.AnchorRight = GetBool(values, "AnchorRight", cfg.AnchorRight);
            cfg.PositionX = GetFloat(values, "PositionX", cfg.PositionX, -4000f, 4000f);
            cfg.PositionY = GetFloat(values, "PositionY", cfg.PositionY, -4000f, 4000f);
        }

        private static string GetLayoutPath()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appData, "7DaysToDie", "DonChanZombieRadar", "layout.cfg");
        }

        private static Dictionary<string, string> ReadValues(string path)
        {
            Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string[] lines = File.ReadAllLines(path);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;
                int eq = line.IndexOf('=');
                if (eq <= 0) continue;
                values[line.Substring(0, eq).Trim()] = line.Substring(eq + 1).Trim();
            }
            return values;
        }

        private static bool GetBool(Dictionary<string, string> values, string key, bool fallback)
        {
            string raw; bool parsed;
            return values.TryGetValue(key, out raw) && bool.TryParse(raw, out parsed) ? parsed : fallback;
        }

        private static float GetFloat(Dictionary<string, string> values, string key, float fallback, float min, float max)
        {
            string raw; float parsed;
            if (!values.TryGetValue(key, out raw) || !float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed)) return fallback;
            return Mathf.Clamp(parsed, min, max);
        }

        private static KeyCode GetKey(Dictionary<string, string> values, string key, KeyCode fallback)
        {
            string raw; KeyCode parsed;
            return values.TryGetValue(key, out raw) && Enum.TryParse<KeyCode>(raw, true, out parsed) ? parsed : fallback;
        }
    }
}

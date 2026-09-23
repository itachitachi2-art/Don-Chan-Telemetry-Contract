using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DonChan.ZombieRadar
{
    public sealed class RadarRuntime : MonoBehaviour
    {
        private sealed class Blip
        {
            internal int EntityId;
            internal Vector2 Relative;
        }

        private sealed class DeathMarker
        {
            internal int EntityId;
            internal Vector3 WorldPosition;
            internal float CreatedAt;
        }

        private static RadarRuntime _instance;
        private static RadarConfig _config;
        private static readonly List<DeathMarker> PendingDeaths = new List<DeathMarker>();
        private static readonly object DeathLock = new object();

        private readonly List<Blip> _blips = new List<Blip>();
        private readonly List<DeathMarker> _deaths = new List<DeathMarker>();
        private readonly Dictionary<int, float> _blipPingTimes = new Dictionary<int, float>();
        private Texture2D _pixel;
        private Texture2D _disc;
        private Texture2D _markerDisc;
        private float _nextScan;
        private float _sweepAngleDegrees;
        private float _previousSweepAngleDegrees;
        private bool _sweepInitialized;
        private Vector3 _playerPosition;
        private Vector3 _playerForward = Vector3.forward;
        private Vector3 _playerRight = Vector3.right;
        private bool _hasPlayer;
        private int _zombieCount;
        private bool _placementMode;
        private bool _dragging;
        private Vector2 _dragOffset;
        private CursorLockMode _previousCursorLock;
        private bool _previousCursorVisible;

        internal static void Initialize(Mod mod)
        {
            _config = RadarConfig.Load(mod.Path);
            GameObject go = new GameObject("DonChanZombieRadar.Runtime");
            UnityEngine.Object.DontDestroyOnLoad(go);
            _instance = go.AddComponent<RadarRuntime>();
            Log.Out("[DonChanZombieRadar] Range=" + _config.RangeMeters.ToString("F1") + "m, Toggle=" + _config.ToggleKey + ", Placement=" + _config.PlacementKey);
        }

        internal static void RecordDeath(EntityAlive entity)
        {
            if (entity == null || !IsZombie(entity)) return;
            DeathMarker marker = new DeathMarker();
            marker.EntityId = entity.entityId;
            marker.WorldPosition = entity.position;
            marker.CreatedAt = Time.realtimeSinceStartup;
            if (_instance != null) _instance._blipPingTimes.Remove(marker.EntityId);
            lock (DeathLock)
            {
                for (int i = PendingDeaths.Count - 1; i >= 0; i--)
                    if (PendingDeaths[i].EntityId == marker.EntityId) PendingDeaths.RemoveAt(i);
                PendingDeaths.Add(marker);
            }
        }

        private static bool IsZombie(EntityAlive entity)
        {
            if (entity == null) return false;
            Type t = entity.GetType();
            while (t != null)
            {
                if (t.Name.IndexOf("Zombie", StringComparison.OrdinalIgnoreCase) >= 0) return true;
                t = t.BaseType;
            }
            return false;
        }

        private void Awake()
        {
            _pixel = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            _pixel.SetPixel(0, 0, Color.white);
            _pixel.Apply();

            const int texSize = 256;
            _disc = new Texture2D(texSize, texSize, TextureFormat.RGBA32, false);
            Color32[] pixels = new Color32[texSize * texSize];
            float c = (texSize - 1) * 0.5f;
            float r = c;
            for (int y = 0; y < texSize; y++)
            {
                for (int x = 0; x < texSize; x++)
                {
                    float dx = x - c;
                    float dy = y - c;
                    float d = Mathf.Sqrt(dx * dx + dy * dy) / r;
                    if (d <= 1f)
                    {
                        byte alpha = (byte)(d > 0.94f ? 205 : 160);
                        pixels[y * texSize + x] = new Color32(4, 33, 21, alpha);
                    }
                    else pixels[y * texSize + x] = new Color32(0, 0, 0, 0);
                }
            }
            _disc.SetPixels32(pixels);
            _disc.Apply();

            const int markerSize = 48;
            _markerDisc = new Texture2D(markerSize, markerSize, TextureFormat.RGBA32, false);
            Color32[] markerPixels = new Color32[markerSize * markerSize];
            float mc = (markerSize - 1) * 0.5f;
            float mr = mc;
            for (int y = 0; y < markerSize; y++)
            {
                for (int x = 0; x < markerSize; x++)
                {
                    float dx = x - mc;
                    float dy = y - mc;
                    float d = Mathf.Sqrt(dx * dx + dy * dy) / mr;
                    if (d <= 1f)
                    {
                        float falloff = 1f - d;
                        byte alpha = (byte)Mathf.Clamp(Mathf.RoundToInt(255f * falloff * falloff), 0, 255);
                        markerPixels[y * markerSize + x] = new Color32(255, 255, 255, alpha);
                    }
                    else markerPixels[y * markerSize + x] = new Color32(255, 255, 255, 0);
                }
            }
            _markerDisc.SetPixels32(markerPixels);
            _markerDisc.Apply();
        }

        private void Update()
        {
            if (_config == null) return;
            if (Input.GetKeyDown(_config.ToggleKey)) _config.Enabled = !_config.Enabled;
            if (Input.GetKeyDown(_config.PlacementKey)) TogglePlacementMode();
            DrainDeaths();
            RemoveExpiredDeaths();
            if (!_config.Enabled) return;

            float now = Time.realtimeSinceStartup;
            if (now >= _nextScan)
            {
                _nextScan = now + _config.ScanIntervalSeconds;
                Scan();
            }
            UpdateSweepAndPings(now);
        }

        private void Scan()
        {
            _blips.Clear();
            _zombieCount = 0;
            _hasPlayer = false;

            try
            {
                if (GameManager.Instance == null || GameManager.Instance.World == null) return;
                EntityPlayerLocal player = GameManager.Instance.World.GetPrimaryPlayer() as EntityPlayerLocal;
                if (player == null) return;

                _hasPlayer = true;
                _playerPosition = player.position;
                Vector3 forward = player.transform.forward;
                forward.y = 0f;
                if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
                else forward.Normalize();
                _playerForward = forward;
                _playerRight = new Vector3(forward.z, 0f, -forward.x);

                IEnumerable list = ReflectionHelper.ReadEnumerable(GameManager.Instance.World, "EntityAlives", "entityAlives");
                if (list == null) return;

                float rangeSq = _config.RangeMeters * _config.RangeMeters;
                foreach (object obj in list)
                {
                    EntityAlive entity = obj as EntityAlive;
                    if (entity == null || entity == player || entity.IsDead() || !IsZombie(entity)) continue;
                    Vector3 delta = entity.position - _playerPosition;
                    float distSq2D = delta.x * delta.x + delta.z * delta.z;
                    if (distSq2D > rangeSq) continue;

                    Blip b = new Blip();
                    b.EntityId = entity.entityId;
                    if (_config.RotateWithPlayer)
                    {
                        b.Relative = new Vector2(Vector3.Dot(delta, _playerRight), Vector3.Dot(delta, _playerForward));
                    }
                    else b.Relative = new Vector2(delta.x, delta.z);
                    _blips.Add(b);
                    _zombieCount++;
                }

                if (_blipPingTimes.Count > 0)
                {
                    List<int> stale = new List<int>();
                    foreach (KeyValuePair<int, float> pair in _blipPingTimes)
                    {
                        bool found = false;
                        for (int i = 0; i < _blips.Count; i++)
                        {
                            if (_blips[i].EntityId == pair.Key)
                            {
                                found = true;
                                break;
                            }
                        }
                        if (!found) stale.Add(pair.Key);
                    }
                    for (int i = 0; i < stale.Count; i++) _blipPingTimes.Remove(stale[i]);
                }
            }
            catch (Exception ex)
            {
                Log.Warning("[DonChanZombieRadar] Scan failed: " + ex.Message);
            }
        }

        private void UpdateSweepAndPings(float now)
        {
            float cycle = Mathf.Max(0.5f, _config.SweepCycleSeconds);
            float current = Mathf.Repeat((now / cycle) * 360f, 360f);
            _sweepAngleDegrees = current;

            if (!_sweepInitialized)
            {
                _previousSweepAngleDegrees = current;
                _sweepInitialized = true;
                return;
            }

            float swept = Mathf.Repeat(current - _previousSweepAngleDegrees, 360f);
            if (swept > 0.001f)
            {
                for (int i = 0; i < _blips.Count; i++)
                {
                    Blip blip = _blips[i];
                    float target = NormalizeDegrees(Mathf.Atan2(blip.Relative.x, blip.Relative.y) * Mathf.Rad2Deg);
                    float fromPrevious = Mathf.Repeat(target - _previousSweepAngleDegrees, 360f);
                    if (fromPrevious <= swept + 1.5f)
                        _blipPingTimes[blip.EntityId] = now;
                }
            }

            _previousSweepAngleDegrees = current;
        }

        private static float NormalizeDegrees(float angle)
        {
            return Mathf.Repeat(angle, 360f);
        }

        private void DrainDeaths()
        {
            lock (DeathLock)
            {
                if (PendingDeaths.Count == 0) return;
                for (int i = 0; i < PendingDeaths.Count; i++)
                {
                    DeathMarker incoming = PendingDeaths[i];
                    bool replaced = false;
                    for (int j = 0; j < _deaths.Count; j++)
                    {
                        if (_deaths[j].EntityId != incoming.EntityId) continue;
                        _deaths[j] = incoming;
                        replaced = true;
                        break;
                    }
                    if (!replaced) _deaths.Add(incoming);
                }
                PendingDeaths.Clear();
            }
        }

        private void RemoveExpiredDeaths()
        {
            float now = Time.realtimeSinceStartup;
            for (int i = _deaths.Count - 1; i >= 0; i--)
                if (now - _deaths[i].CreatedAt > _config.DeathMarkerSeconds) _deaths.RemoveAt(i);
        }

        private void OnGUI()
        {
            if (_config == null || !_config.Enabled || !_hasPlayer) return;

            float size = _config.RadarSize;
            float x = _config.AnchorRight ? Screen.width + _config.PositionX : _config.PositionX;
            float y = _config.PositionY;
            x = Mathf.Clamp(x, 0f, Mathf.Max(0f, Screen.width - size));
            y = Mathf.Clamp(y, 0f, Mathf.Max(0f, Screen.height - size));
            Rect radar = new Rect(x, y, size, size);

            if (_placementMode)
            {
                HandlePlacementInput(radar);
                x = _config.AnchorRight ? Screen.width + _config.PositionX : _config.PositionX;
                y = _config.PositionY;
                x = Mathf.Clamp(x, 0f, Mathf.Max(0f, Screen.width - size));
                y = Mathf.Clamp(y, 0f, Mathf.Max(0f, Screen.height - size));
                radar = new Rect(x, y, size, size);
            }

            if (Event.current.type != EventType.Repaint) return;

            Vector2 center = new Vector2(radar.x + size * 0.5f, radar.y + size * 0.5f);
            float radius = size * 0.46f;

            GUI.color = Color.white;
            GUI.DrawTexture(radar, _disc, ScaleMode.StretchToFill, true);

            Color grid = new Color(0.18f, 0.95f, 0.48f, 0.55f);
            Color ring = new Color(0.24f, 1f, 0.56f, 0.78f);
            DrawGrid(center, radius, grid);
            DrawCircle(center, radius, ring, 2f, 80);

            float step = _config.GridMeters;
            for (float r = step; r < _config.RangeMeters; r += step)
                DrawCircle(center, radius * (r / _config.RangeMeters), new Color(0.18f, 0.95f, 0.48f, 0.34f), 1f, 64);

            DrawSweep(center, radius);
            DrawSelf(center);
            DrawZombies(center, radius);
            DrawDeaths(center, radius);

            if (_config.ShowLabels)
            {
                GUIStyle info = new GUIStyle(GUI.skin.label);
                info.alignment = TextAnchor.LowerCenter;
                info.fontSize = 11;
                info.normal.textColor = new Color(0.65f, 1f, 0.78f, 0.95f);
                GUI.Label(new Rect(radar.x, radar.y + size - 27f, size, 22f), _config.RangeMeters.ToString("F0") + "m   Z:" + _zombieCount, info);
            }

            if (_placementMode)
            {
                DrawPlacementOverlay(radar);
            }

            GUI.color = Color.white;
        }

        private void TogglePlacementMode()
        {
            _placementMode = !_placementMode;
            _dragging = false;
            if (_placementMode)
            {
                _previousCursorLock = Cursor.lockState;
                _previousCursorVisible = Cursor.visible;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                Log.Out("[DonChanZombieRadar] Placement mode ON. Drag radar with left mouse; press " + _config.PlacementKey + " to save.");
            }
            else
            {
                RadarConfig.SaveLayout(_config);
                Cursor.lockState = _previousCursorLock;
                Cursor.visible = _previousCursorVisible;
                Log.Out("[DonChanZombieRadar] Placement saved. X=" + _config.PositionX.ToString("F0") + ", Y=" + _config.PositionY.ToString("F0"));
            }
        }

        private void HandlePlacementInput(Rect radar)
        {
            Event e = Event.current;
            if (e == null) return;

            if (e.type == EventType.MouseDown && e.button == 0 && radar.Contains(e.mousePosition))
            {
                _dragging = true;
                _dragOffset = e.mousePosition - new Vector2(radar.x, radar.y);
                e.Use();
                return;
            }

            if (e.type == EventType.MouseDrag && e.button == 0 && _dragging)
            {
                Vector2 topLeft = e.mousePosition - _dragOffset;
                float maxX = Mathf.Max(0f, Screen.width - _config.RadarSize);
                float maxY = Mathf.Max(0f, Screen.height - _config.RadarSize);
                topLeft.x = Mathf.Clamp(topLeft.x, 0f, maxX);
                topLeft.y = Mathf.Clamp(topLeft.y, 0f, maxY);
                _config.PositionX = _config.AnchorRight ? topLeft.x - Screen.width : topLeft.x;
                _config.PositionY = topLeft.y;
                e.Use();
                return;
            }

            if (e.type == EventType.MouseUp && e.button == 0 && _dragging)
            {
                _dragging = false;
                RadarConfig.SaveLayout(_config);
                e.Use();
            }
        }

        private void DrawPlacementOverlay(Rect radar)
        {
            GUI.color = new Color(1f, 0.85f, 0.15f, 0.9f);
            DrawLine(new Vector2(radar.x, radar.y), new Vector2(radar.xMax, radar.y), GUI.color, 2f);
            DrawLine(new Vector2(radar.xMax, radar.y), new Vector2(radar.xMax, radar.yMax), GUI.color, 2f);
            DrawLine(new Vector2(radar.xMax, radar.yMax), new Vector2(radar.x, radar.yMax), GUI.color, 2f);
            DrawLine(new Vector2(radar.x, radar.yMax), new Vector2(radar.x, radar.y), GUI.color, 2f);

            GUIStyle style = new GUIStyle(GUI.skin.label);
            style.alignment = TextAnchor.MiddleCenter;
            style.fontStyle = FontStyle.Bold;
            style.fontSize = 13;
            style.normal.textColor = new Color(1f, 0.92f, 0.35f, 1f);
            GUI.Label(new Rect(radar.x, radar.yMax + 3f, radar.width, 24f), "DRAG MODE  |  " + _config.PlacementKey + " SAVE", style);
        }

        private void DrawGrid(Vector2 center, float radius, Color color)
        {
            float meterScale = radius / _config.RangeMeters;
            float stepPx = _config.GridMeters * meterScale;
            for (float offset = stepPx; offset < radius; offset += stepPx)
            {
                float half = Mathf.Sqrt(Mathf.Max(0f, radius * radius - offset * offset));
                DrawLine(new Vector2(center.x + offset, center.y - half), new Vector2(center.x + offset, center.y + half), color, 1f);
                DrawLine(new Vector2(center.x - offset, center.y - half), new Vector2(center.x - offset, center.y + half), color, 1f);
                DrawLine(new Vector2(center.x - half, center.y + offset), new Vector2(center.x + half, center.y + offset), color, 1f);
                DrawLine(new Vector2(center.x - half, center.y - offset), new Vector2(center.x + half, center.y - offset), color, 1f);
            }
            DrawLine(new Vector2(center.x - radius, center.y), new Vector2(center.x + radius, center.y), new Color(color.r, color.g, color.b, 0.35f), 1f);
            DrawLine(new Vector2(center.x, center.y - radius), new Vector2(center.x, center.y + radius), new Color(color.r, color.g, color.b, 0.35f), 1f);
        }

        private void DrawSelf(Vector2 center)
        {
            float s = _config.SelfDotSize;
            GUI.color = new Color(0.45f, 1f, 0.63f, 1f);
            GUI.DrawTexture(new Rect(center.x - s * 0.5f, center.y - s * 0.5f, s, s), _pixel);
            DrawLine(new Vector2(center.x, center.y - s), new Vector2(center.x, center.y - s - 9f), new Color(0.65f, 1f, 0.75f, 1f), 2f);
        }

        private void DrawSweep(Vector2 center, float radius)
        {
            float trail = Mathf.Clamp(_config.SweepTrailDegrees, 0f, 120f);
            const int trailLines = 18;
            for (int i = trailLines; i >= 1; i--)
            {
                float t = i / (float)trailLines;
                float angle = _sweepAngleDegrees - trail * t;
                float alpha = (1f - t) * 0.085f;
                Vector2 end = SweepEndpoint(center, radius, angle);
                DrawLine(center, end, new Color(0.28f, 1f, 0.58f, alpha), 1.0f);
            }

            Vector2 mainEnd = SweepEndpoint(center, radius, _sweepAngleDegrees);
            DrawLine(center, mainEnd, new Color(0.55f, 1f, 0.72f, 0.52f), 1.8f);

            if (_markerDisc != null)
            {
                float head = 10f;
                Color old = GUI.color;
                GUI.color = new Color(0.65f, 1f, 0.78f, 0.48f);
                GUI.DrawTexture(new Rect(mainEnd.x - head * 0.5f, mainEnd.y - head * 0.5f, head, head), _markerDisc);
                GUI.color = old;
            }
        }

        private static Vector2 SweepEndpoint(Vector2 center, float radius, float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            return new Vector2(center.x + Mathf.Sin(radians) * radius, center.y - Mathf.Cos(radians) * radius);
        }

        private void DrawZombies(Vector2 center, float radius)
        {
            float scale = radius / _config.RangeMeters;
            float dot = _config.ZombieDotSize;
            float now = Time.realtimeSinceStartup;
            for (int i = 0; i < _blips.Count; i++)
            {
                float pingAt;
                if (!_blipPingTimes.TryGetValue(_blips[i].EntityId, out pingAt)) continue;

                float age = now - pingAt;
                if (age < 0f || age > _config.BlipPersistenceSeconds) continue;
                float alpha = 1f - Mathf.Clamp01(age / _config.BlipPersistenceSeconds);
                float pulse = 1f + 0.28f * (1f - Mathf.Clamp01(age / 0.20f));

                Vector2 r = _blips[i].Relative;
                float sx = center.x + r.x * scale;
                float sy = center.y - r.y * scale;
                float drawSize = dot * pulse;

                Color old = GUI.color;
                if (_markerDisc != null)
                {
                    GUI.color = new Color(1f, 0.04f, 0.04f, alpha * 0.35f);
                    float glow = drawSize * 1.65f;
                    GUI.DrawTexture(new Rect(sx - glow * 0.5f, sy - glow * 0.5f, glow, glow), _markerDisc);
                    GUI.color = new Color(1f, 0.08f, 0.08f, alpha);
                    GUI.DrawTexture(new Rect(sx - drawSize * 0.5f, sy - drawSize * 0.5f, drawSize, drawSize), _markerDisc);
                }
                else
                {
                    GUI.color = new Color(1f, 0.12f, 0.12f, alpha);
                    GUI.DrawTexture(new Rect(sx - drawSize * 0.5f, sy - drawSize * 0.5f, drawSize, drawSize), _pixel);
                }
                GUI.color = old;
            }
        }

        private void DrawDeaths(Vector2 center, float radius)
        {
            float scale = radius / _config.RangeMeters;
            float now = Time.realtimeSinceStartup;
            for (int i = 0; i < _deaths.Count; i++)
            {
                DeathMarker d = _deaths[i];
                Vector3 delta = d.WorldPosition - _playerPosition;
                float distSq2D = delta.x * delta.x + delta.z * delta.z;
                if (distSq2D > _config.RangeMeters * _config.RangeMeters) continue;

                Vector2 local;
                if (_config.RotateWithPlayer)
                    local = new Vector2(Vector3.Dot(delta, _playerRight), Vector3.Dot(delta, _playerForward));
                else local = new Vector2(delta.x, delta.z);

                float sx = center.x + local.x * scale;
                float sy = center.y - local.y * scale;
                float alpha = 1f - Mathf.Clamp01((now - d.CreatedAt) / _config.DeathMarkerSeconds);
                Color c = new Color(1f, 0.2f, 0.2f, alpha);
                float arm = 12f;
                DrawLine(new Vector2(sx - arm, sy - arm), new Vector2(sx + arm, sy + arm), c, 4f);
                DrawLine(new Vector2(sx - arm, sy + arm), new Vector2(sx + arm, sy - arm), c, 4f);
            }
        }

        private void DrawCircle(Vector2 center, float radius, Color color, float width, int segments)
        {
            Vector2 prev = new Vector2(center.x + radius, center.y);
            for (int i = 1; i <= segments; i++)
            {
                float a = (Mathf.PI * 2f * i) / segments;
                Vector2 next = new Vector2(center.x + Mathf.Cos(a) * radius, center.y + Mathf.Sin(a) * radius);
                DrawLine(prev, next, color, width);
                prev = next;
            }
        }

        private void DrawLine(Vector2 a, Vector2 b, Color color, float width)
        {
            Matrix4x4 old = GUI.matrix;
            Color oldColor = GUI.color;
            float angle = Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg;
            float length = Vector2.Distance(a, b);
            GUIUtility.RotateAroundPivot(angle, a);
            GUI.color = color;
            GUI.DrawTexture(new Rect(a.x, a.y - width * 0.5f, length, width), _pixel);
            GUI.matrix = old;
            GUI.color = oldColor;
        }
    }
}

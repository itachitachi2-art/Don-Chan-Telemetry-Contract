using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace DonChan.TelemetryProbe
{
    // Contains serialized LIGHT records only. No Unity objects cross the thread boundary.
    internal sealed class TelemetryFeed
    {
        private sealed class Entry { internal long Sequence; internal string Json; internal int Bytes; }
        private readonly object _sync = new object();
        private readonly Queue<Entry> _events = new Queue<Entry>();
        private readonly string _session;
        private readonly int _capacity;
        private int _bytes;
        private long _latest;
        private long _droppedThrough;
        private long _snapshotSequence;
        private string _snapshot = "null";
        internal const int MaxRecordBytes = 262144;
        private const int MaxBufferBytes = 8388608;

        internal TelemetryFeed(string session, int capacity)
        {
            _session = session;
            _capacity = Math.Max(1, capacity);
        }

        internal void Publish(long sequence, string json, bool snapshot)
        {
            int bytes = Encoding.UTF8.GetByteCount(json);
            lock (_sync)
            {
                _latest = Math.Max(_latest, sequence);
                if (snapshot)
                {
                    if (sequence > _snapshotSequence)
                    {
                        _snapshotSequence = sequence;
                        _snapshot = bytes <= MaxRecordBytes ? json : "null";
                    }
                    return;
                }
                if (bytes > MaxRecordBytes)
                {
                    // Clear earlier events so every advertised loss is before retained events.
                    _events.Clear(); _bytes = 0;
                    _droppedThrough = sequence;
                    return;
                }
                _events.Enqueue(new Entry { Sequence = sequence, Json = json, Bytes = bytes });
                _bytes += bytes;
                while (_events.Count > _capacity || _bytes > MaxBufferBytes)
                {
                    var removed = _events.Dequeue();
                    _bytes -= removed.Bytes;
                    _droppedThrough = removed.Sequence;
                }
            }
        }

        internal string Read(string session, long after)
        {
            lock (_sync)
            {
                // A new consumer starts at live state; old sessions are not replayed as current events.
                bool reset = session != _session || after > _latest;
                bool gap = !reset && after < _droppedThrough;
                long cursor = reset ? _latest : Math.Max(after, _droppedThrough);
                var records = new List<string>();
                int bytes = 0;
                bool more = false;
                if (!reset)
                {
                    foreach (var entry in _events)
                    {
                        if (entry.Sequence <= cursor) continue;
                        if (records.Count >= 256 || bytes + entry.Bytes > 524288) { more = true; break; }
                        records.Add(entry.Json); bytes += entry.Bytes; cursor = entry.Sequence;
                    }
                    if (!more) cursor = _latest;
                }
                return "{\"transportVersion\":1,\"sessionId\":" + JsonUtil.Serialize(_session)
                    + ",\"nextSequence\":" + cursor.ToString(CultureInfo.InvariantCulture)
                    + ",\"reset\":" + (reset ? "true" : "false")
                    + ",\"gap\":" + (gap ? "true" : "false")
                    + ",\"hasMore\":" + (more ? "true" : "false")
                    + ",\"snapshot\":" + _snapshot
                    + ",\"events\":[" + string.Join(",", records.ToArray()) + "]}";
            }
        }
    }
}

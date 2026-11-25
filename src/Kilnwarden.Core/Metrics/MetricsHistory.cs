// SPDX-License-Identifier: MIT
using System.Buffers.Binary;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Kilnwarden.Metrics;

/// <summary>A metric the panel keeps (DESIGN §5 Metrics), from Kiln.Admin 30.9's metrics topic (admin-protocol.md).</summary>
public sealed record MetricField(string Key, string Title, string Unit, bool Max = false);

/// <summary>The metrics and their history: fixed rings, nothing grows. Live: every push (about 2 s), 300 kept (10 min,
/// memory only). Minute-scale: 10 s averages, 8640 kept (24 h). Long: 5 min averages, 8640 kept (30 d). The two
/// averaged tiers are saved to one fixed-size binary file. Values are floats, NaN = not reported.</summary>
public sealed class MetricsHistory
{
    public static readonly IReadOnlyList<MetricField> Fields =
    [
        new("players", "Players", "count"),
        new("stepsPerS", "Steps per second", "1/s"),
        new("frameAvgMs", "Frame time (avg)", "ms"),
        new("frameP95Ms", "Frame time (p95)", "ms"),
        new("frameMaxMs", "Frame time (max)", "ms", Max: true),
        new("tickAvgMs", "Tick time (avg)", "ms"),
        new("tickP95Ms", "Tick time (p95)", "ms"),
        new("workingSetBytes", "Memory (working set)", "bytes"),
        new("managedHeapBytes", "Managed heap", "bytes"),
        new("cpuPercent", "CPU", "%"),
        new("gcPauseMs", "GC pauses", "ms"),
        // not in Kiln.Admin's metrics topic: the panel's server watch (mods and save_status ops) fills them in
        new("modFaults", "Mod faults (total)", "count", Max: true),
        new("saveAgeS", "Since the last world save", "s", Max: true),
    ];

    /// <summary>What DESIGN §5 lists and Kiln.Admin does not report: key -> (title, why).</summary>
    public static readonly IReadOnlyDictionary<string, string> UnavailableTitles = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["saveMs"] = "World save time",
    };

    /// <summary>What DESIGN §5 lists and Kiln.Admin does not report.</summary>
    public static readonly IReadOnlyDictionary<string, string> Unavailable = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["saveMs"] = "how long a world save takes: Kiln.Admin tells only whoever asked for that save (\"Since the last world save\" is shown instead)",
    };

    public const int LiveCapacity = 300, TierCapacity = 8640, MaxPoints = 600;
    public const long MidStepMs = 10_000, LongStepMs = 300_000;
    private const uint Magic = 0x4D41574B; // "KWAM"
    private const int Version = 1;

    public static int FieldCount => Fields.Count;

    public static int IndexOf(string key)
    {
        for (var i = 0; i < Fields.Count; i++)
            if (Fields[i].Key == key) return i;
        return -1;
    }

    private readonly object _gate = new();
    private readonly Ring _live = new(LiveCapacity);
    private readonly Ring _mid = new(TierCapacity);
    private readonly Ring _long = new(TierCapacity);
    private readonly Bucket _midBucket = new(MidStepMs);
    private readonly Bucket _longBucket = new(LongStepMs);
    private readonly bool[] _seen = new bool[Fields.Count];

    /// <summary>The newest sample (null before the first).</summary>
    public (long At, float[] Values)? Latest
    {
        get
        {
            lock (_gate) return _live.Count == 0 ? null : _live.Last();
        }
    }

    /// <summary>Whether a field has had a value since the start (a field Kiln never sends stays false).</summary>
    public bool Seen(int field)
    {
        lock (_gate) return _seen[field];
    }

    public void Add(long atMs, float[] values)
    {
        if (values.Length != FieldCount) throw new ArgumentException("one value per field");
        lock (_gate)
        {
            if (_live.Count > 0 && atMs <= _live.Last().At) return; // out of order or repeated
            for (var i = 0; i < values.Length; i++) _seen[i] |= !float.IsNaN(values[i]);
            _live.Add(atMs, values);
            if (_midBucket.Add(atMs, values) is { } m) _mid.Add(m.At, m.Values);
            if (_longBucket.Add(atMs, values) is { } l) _long.Add(l.At, l.Values);
        }
    }

    /// <summary>Points of a range: "10m" (live), "1h", "6h", "24h" (10 s tier), "7d", "30d" (5 min tier), at most
    /// &lt;see cref="MaxPoints"/&gt; (groups averaged). Null for an unknown range.</summary>
    public JsonObject? Series(string range, long nowMs)
    {
        (Ring ring, long spanMs, long stepMs)? pick = range switch
        {
            "10m" => (_live, 600_000L, 2_000L),
            "1h" => (_mid, 3_600_000L, MidStepMs),
            "6h" => (_mid, 21_600_000L, MidStepMs),
            "24h" => (_mid, 86_400_000L, MidStepMs),
            "7d" => (_long, 604_800_000L, LongStepMs),
            "30d" => (_long, 2_592_000_000L, LongStepMs),
            _ => null,
        };
        if (pick is not { } p) return null;
        List<(long At, float[] Values)> pts;
        lock (_gate) pts = p.ring.Since(nowMs - p.spanMs);
        var group = Math.Max(1, (pts.Count + MaxPoints - 1) / MaxPoints);
        var t = new JsonArray();
        var series = Fields.Select(_ => new JsonArray()).ToArray();
        for (var i = 0; i < pts.Count; i += group)
        {
            var n = Math.Min(group, pts.Count - i);
            t.Add(pts[i + n - 1].At);
            for (var f = 0; f < FieldCount; f++)
            {
                double sum = 0, max = double.NaN;
                var count = 0;
                for (var k = 0; k < n; k++)
                {
                    var v = pts[i + k].Values[f];
                    if (float.IsNaN(v)) continue;
                    sum += v;
                    count++;
                    max = double.IsNaN(max) ? v : Math.Max(max, v);
                }
                series[f].Add(count == 0 ? null : JsonValue.Create(Math.Round(Fields[f].Max ? max : sum / count, 3)));
            }
        }
        var s = new JsonObject();
        for (var f = 0; f < FieldCount; f++) s[Fields[f].Key] = series[f];
        return new JsonObject { ["range"] = range, ["stepS"] = p.stepMs * group / 1000.0, ["t"] = t, ["series"] = s };
    }

    /// <summary>The two averaged tiers as one fixed-layout file (little-endian): magic, version, field count, then per
    /// tier its count and (at: i64, values: f32 x fields) oldest first.</summary>
    public byte[] Save()
    {
        lock (_gate)
        {
            var rec = 8 + 4 * FieldCount;
            var buf = new byte[12 + 2 * (4 + TierCapacity * rec)];
            var o = 0;
            BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(o), Magic); o += 4;
            BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(o), Version); o += 4;
            BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(o), FieldCount); o += 4;
            foreach (var ring in new[] { _mid, _long })
            {
                var pts = ring.Since(long.MinValue);
                BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(o), pts.Count); o += 4;
                foreach (var (at, values) in pts)
                {
                    BinaryPrimitives.WriteInt64LittleEndian(buf.AsSpan(o), at); o += 8;
                    foreach (var v in values) { BinaryPrimitives.WriteSingleLittleEndian(buf.AsSpan(o), v); o += 4; }
                }
            }
            return buf[..o];
        }
    }

    /// <summary>Loads what &lt;see cref="Save"/&gt; wrote; false (and nothing loaded) for any other content.</summary>
    public bool Load(ReadOnlySpan<byte> data)
    {
        try
        {
            var o = 0;
            if (data.Length < 12 || BinaryPrimitives.ReadUInt32LittleEndian(data) != Magic) return false;
            // an older file has fewer fields (they are only ever added at the end): the missing ones read as not reported
            var stored = BinaryPrimitives.ReadInt32LittleEndian(data[8..]);
            if (BinaryPrimitives.ReadInt32LittleEndian(data[4..]) != Version || stored < 1 || stored > FieldCount) return false;
            o = 12;
            var rec = 8 + 4 * stored;
            var tiers = new List<(long, float[])>[2];
            for (var t = 0; t < 2; t++)
            {
                var n = BinaryPrimitives.ReadInt32LittleEndian(data[o..]); o += 4;
                if (n < 0 || n > TierCapacity || data.Length - o < (long)n * rec) return false;
                tiers[t] = new List<(long, float[])>(n);
                for (var i = 0; i < n; i++)
                {
                    var at = BinaryPrimitives.ReadInt64LittleEndian(data[o..]); o += 8;
                    var v = new float[FieldCount];
                    for (var f = 0; f < FieldCount; f++)
                    {
                        if (f >= stored) { v[f] = float.NaN; continue; }
                        v[f] = BinaryPrimitives.ReadSingleLittleEndian(data[o..]);
                        o += 4;
                    }
                    tiers[t].Add((at, v));
                }
            }
            lock (_gate)
            {
                foreach (var (at, v) in tiers[0]) _mid.Add(at, v);
                foreach (var (at, v) in tiers[1]) _long.Add(at, v);
            }
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }

    private sealed class Ring(int capacity)
    {
        private readonly long[] _at = new long[capacity];
        private readonly float[][] _v = new float[capacity][];
        private int _head, _count;

        public int Count => _count;

        public void Add(long at, float[] values)
        {
            if (_count > 0 && at <= Last().At) return;
            _at[_head] = at;
            _v[_head] = (float[])values.Clone();
            _head = (_head + 1) % capacity;
            if (_count < capacity) _count++;
        }

        public (long At, float[] Values) Last()
        {
            var i = (_head - 1 + capacity) % capacity;
            return (_at[i], _v[i]);
        }

        public List<(long At, float[] Values)> Since(long fromMs)
        {
            var list = new List<(long, float[])>(_count);
            var start = (_head - _count + capacity) % capacity;
            for (var k = 0; k < _count; k++)
            {
                var i = (start + k) % capacity;
                if (_at[i] >= fromMs) list.Add((_at[i], _v[i]));
            }
            return list;
        }
    }

    /// <summary>Averages samples per step (max for the max fields); emits the finished bucket when a sample of the next
    /// one arrives, stamped at the bucket's end.</summary>
    private sealed class Bucket(long stepMs)
    {
        private long _key = long.MinValue;
        private readonly double[] _sum = new double[FieldCount];
        private readonly int[] _n = new int[FieldCount];
        private readonly float[] _max = new float[FieldCount];

        public (long At, float[] Values)? Add(long at, float[] values)
        {
            var key = at / stepMs;
            (long, float[])? done = null;
            if (key != _key)
            {
                if (_key != long.MinValue) done = Flush();
                _key = key;
                Array.Clear(_sum);
                Array.Clear(_n);
                Array.Fill(_max, float.NaN);
            }
            for (var f = 0; f < values.Length; f++)
            {
                var v = values[f];
                if (float.IsNaN(v)) continue;
                _sum[f] += v;
                _n[f]++;
                _max[f] = float.IsNaN(_max[f]) ? v : Math.Max(_max[f], v);
            }
            return done;
        }

        private (long, float[]) Flush()
        {
            var v = new float[FieldCount];
            for (var f = 0; f < FieldCount; f++) v[f] = _n[f] == 0 ? float.NaN : Fields[f].Max ? _max[f] : (float)(_sum[f] / _n[f]);
            return ((_key + 1) * stepMs, v);
        }
    }

    /// <summary>A metrics push's data object (admin-protocol.md, metrics) as one sample; null when it has no "at".</summary>
    public static (long At, float[] Values)? Parse(JsonNode? data)
    {
        if (data is not JsonObject d || Num(d["at"]) is not { } at) return null;
        float F(JsonNode? n) => Num(n) is { } v ? (float)v : float.NaN;
        var mem = d["memory"] as JsonObject;
        var frame = d["frameMs"] as JsonObject;
        var tick = d["tickMs"] as JsonObject;
        var v = new[]
        {
            F(d["players"]), F(d["stepsPerS"]), F(frame?["avg"]), F(frame?["p95"]), F(frame?["max"]), F(tick?["avg"]), F(tick?["p95"]),
            F(mem?["workingSet"]), F(mem?["managedHeap"]), F((d["cpu"] as JsonObject)?["percent"]), F(d["gcPauseMs"]),
            float.NaN, float.NaN, // modFaults, saveAgeS: the server watch's (MetricsComponent.Add)
        };
        return ((long)at, v);
    }

    private static double? Num(JsonNode? n) => Extensions.JsonSchemaLite.Num(n) is { } d && double.IsFinite(d) ? d : null;
}

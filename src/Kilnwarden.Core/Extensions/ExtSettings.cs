// SPDX-License-Identifier: MIT
using System.Text.Json;
using System.Text.Json.Nodes;
using Kilnwarden.Api;
using Kilnwarden.Hosting;
using Kilnwarden.Storage;

namespace Kilnwarden.Extensions;

internal interface IExtSettings : IDisposable
{
    /// <summary>Saves a value the panel's form sent (already checked against the schema).</summary>
    void Apply(JsonObject value);
}

/// <summary>An extension module's typed settings: ext/&lt;module&gt;.json (atomic writes, polled every 2 s, the last good
/// value kept when the file goes bad), validated against the module's schema. The file is read as JSON and the mod's
/// type &lt;typeparamref name="T"/&gt; is converted by &lt;see cref="SettingsMapper"/&gt; (no serializer cache, so nothing keeps
/// the mod's collectible types alive after it unloads).</summary>
internal sealed class ExtSettings<T> : IWebAdminSettings<T>, IExtSettings where T : class, new()
{
    private static readonly JsonDocumentOptions ReadOptions = new() { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true, MaxDepth = 32 };
    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

    private readonly DataStore _data;
    private readonly string _file;
    private readonly IWebAdminLog _log;
    private readonly JsonObject? _schema;
    private readonly object _gate = new();
    private bool _disposed;
    private ITimer? _timer;
    private (long, DateTime) _seen;
    private T _current;

    public ExtSettings(DataStore data, string file, IWebAdminLog log, JsonObject? schema, TimeProvider time)
    {
        _data = data;
        _file = file;
        _log = log;
        _schema = schema;
        SettingsMapper.Check(typeof(T));
        _current = new T();
        lock (_gate)
        {
            if (!data.Exists(file)) Write(SettingsMapper.ToJson(_current));
            else if (Read() is { } first && Convert(first) is { } t) _current = t;
            _seen = Stamp();
        }
        _timer = time.CreateTimer(_ => Poll(), null, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2));
    }

    public T Current => Volatile.Read(ref _current);

    public event Action<T>? Changed;

    private JsonObject? Read()
    {
        try
        {
            var o = JsonNode.Parse(_data.ReadBytes(_file) ?? "{}"u8.ToArray(), documentOptions: ReadOptions) as JsonObject;
            if (o is null) _log.Warn($"{_file}: not a JSON object, keeping the last good settings");
            else if (_schema is not null && JsonSchemaLite.Validate(_schema, o) is { Count: > 0 } p)
            {
                _log.Warn($"{_file}: not reloaded, keeping the last good settings: {string.Join("; ", p.Take(5))}");
                return null;
            }
            return o;
        }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException)
        {
            _log.Warn($"{_file}: unreadable, keeping the last good settings: {e.Message}");
            return null;
        }
    }

    private T? Convert(JsonObject o)
    {
        try
        {
            return SettingsMapper.FromJson<T>(o);
        }
        catch (Exception e) when (e is FormatException or OverflowException or ArgumentException)
        {
            _log.Warn($"{_file}: does not fit the module's settings type, keeping the last good value: {e.Message}");
            return null;
        }
    }

    // owner-only: modules often keep API keys in their settings (review I3)
    private void Write(JsonObject o) => _data.WriteBytes(_file, JsonSerializer.SerializeToUtf8Bytes(o, WriteOptions), secret: true);

    private (long, DateTime) Stamp()
    {
        var fi = new FileInfo(_data.PathOf(_file));
        return fi.Exists ? (fi.Length, fi.LastWriteTimeUtc) : (-1, default);
    }

    /// <summary>Re-reads the file when it changed (the timer; tests call it).</summary>
    internal bool Poll()
    {
        T t;
        lock (_gate)
        {
            if (_disposed) return false;
            var stamp = Stamp();
            if (stamp == _seen) return false;
            _seen = stamp;
            if (Read() is not { } o || Convert(o) is not { } c) return false;
            t = c;
            Volatile.Write(ref _current, t);
        }
        Raise(t);
        return true;
    }

    public void Apply(JsonObject value)
    {
        T t;
        lock (_gate)
        {
            if (_schema is not null && JsonSchemaLite.Validate(_schema, value) is { Count: > 0 } p) throw new ArgumentException(string.Join("; ", p.Take(10)));
            t = Convert(value) ?? throw new ArgumentException("the settings do not fit the module's settings type");
            Write(value);
            _seen = Stamp();
            Volatile.Write(ref _current, t);
        }
        Raise(t);
    }

    private void Raise(T t)
    {
        try
        {
            Changed?.Invoke(t);
        }
        catch (Exception e)
        {
            _log.Error($"{_file}: the module's settings listener failed", e);
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _timer?.Dispose();
            _timer = null;
            Changed = null;
            _disposed = true;
        }
    }
}

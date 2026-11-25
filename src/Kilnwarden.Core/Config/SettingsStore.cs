// SPDX-License-Identifier: MIT
using System.Text.Json;
using Kilnwarden.Hosting;
using Kilnwarden.Storage;

namespace Kilnwarden.Config;

/// <summary>A JSON settings file with defaults, validation, atomic saves and hot reload. The file is polled (size and
/// write time, every 2 s: FileSystemWatcher is unreliable under Wine); a valid change replaces <see cref="Current"/> and
/// raises <see cref="Changed"/>, an invalid one is logged and the last good settings stay. A missing file is written with
/// the defaults so the owner can find it. Treat <see cref="Current"/> as read-only: change settings with
/// <see cref="Save"/>.</summary>
public sealed class SettingsStore<T> : IDisposable where T : class, new()
{
    private readonly DataStore _data;
    private readonly string _file;
    private readonly Func<T, IReadOnlyList<string>> _validate;
    private readonly IWebAdminLog _log;
    private readonly object _gate = new();
    private ITimer? _timer;
    private (long Length, DateTime Written) _seen;
    private T _current;

    public SettingsStore(DataStore data, string file, IWebAdminLog log, Func<T, IReadOnlyList<string>>? validate = null)
    {
        _data = data;
        _file = file;
        _log = log;
        _validate = validate ?? (_ => []);
        _current = new T();
        LoadInitial();
    }

    public string FilePath => _data.PathOf(_file);
    public T Current => Volatile.Read(ref _current);

    /// <summary>The problems of the last reload that was refused (empty when the file is good).</summary>
    public IReadOnlyList<string> Problems { get; private set; } = [];

    /// <summary>(old, new) after a reload or a save; on the poll timer's thread or the saver's.</summary>
    public event Action<T, T>? Changed;

    /// <summary>Starts polling the file every <paramref name="interval"/> (default 2 s).</summary>
    public void Watch(TimeProvider time, TimeSpan? interval = null)
    {
        var every = interval ?? TimeSpan.FromSeconds(2);
        lock (_gate)
        {
            if (_disposed) return;
            _timer ??= time.CreateTimer(_ => Poll(), null, every, every);
        }
    }

    /// <summary>Validates, writes atomically, then replaces <see cref="Current"/>. Throws ArgumentException with the
    /// problems when <paramref name="value"/> is invalid.</summary>
    public void Save(T value)
    {
        var copy = Clone(value);
        var problems = _validate(copy);
        if (problems.Count > 0) throw new ArgumentException(string.Join("; ", problems));
        T old;
        lock (_gate)
        {
            _data.WriteJson(_file, copy);
            _seen = Stamp();
            old = _current;
            Volatile.Write(ref _current, copy);
            Problems = [];
        }
        Raise(old, copy);
    }

    /// <summary>Reads the file now if it changed (the poll timer's body; tests call it directly). True when
    /// <see cref="Current"/> changed.</summary>
    public bool Poll()
    {
        T old, next;
        lock (_gate)
        {
            var stamp = Stamp();
            if (stamp == _seen) return false;
            _seen = stamp;
            if (!TryRead(out var read, out var problems))
            {
                Problems = problems;
                _log.Warn($"{_file}: not reloaded, keeping the last good settings: {string.Join("; ", problems)}");
                return false;
            }
            Problems = [];
            old = _current;
            next = read!;
            Volatile.Write(ref _current, next);
        }
        _log.Info($"{_file}: reloaded");
        Raise(old, next);
        return true;
    }

    private void LoadInitial()
    {
        lock (_gate)
        {
            _seen = Stamp();
            if (!_data.Exists(_file))
            {
                try
                {
                    _data.WriteJson(_file, _current);
                    _seen = Stamp();
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                {
                    _log.Warn($"{_file}: cannot write the defaults: {e.Message}");
                }
                return;
            }
            if (TryRead(out var read, out var problems)) _current = read!;
            else
            {
                Problems = problems;
                _log.Error($"{_file}: unreadable, using the defaults until it is fixed: {string.Join("; ", problems)}");
            }
        }
    }

    private bool TryRead(out T? value, out IReadOnlyList<string> problems)
    {
        value = null;
        try
        {
            value = _data.ReadJson<T>(_file) ?? new T();
        }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            problems = [e.Message];
            return false;
        }
        try
        {
            problems = _validate(value);
        }
        catch (Exception e)
        {
            // a validator bug must not take the poll timer (and the server process) down
            problems = ["the settings could not be checked: " + e.Message];
        }
        return problems.Count == 0;
    }

    private (long, DateTime) Stamp()
    {
        var fi = new FileInfo(FilePath);
        return fi.Exists ? (fi.Length, fi.LastWriteTimeUtc) : (-1, default);
    }

    private void Raise(T old, T next)
    {
        try
        {
            Changed?.Invoke(old, next);
        }
        catch (Exception e)
        {
            _log.Error($"{_file}: a settings listener failed", e);
        }
    }

    private static T Clone(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.SerializeToUtf8Bytes(value, DataStore.Json), DataStore.Json)!;

    private bool _disposed;

    public void Dispose()
    {
        lock (_gate)
        {
            _disposed = true;
            _timer?.Dispose();
            _timer = null;
        }
    }
}

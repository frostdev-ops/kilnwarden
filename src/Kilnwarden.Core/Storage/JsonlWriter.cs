// SPDX-License-Identifier: MIT
using System.Text;
using System.Text.Json;

namespace Kilnwarden.Storage;

/// <summary>An append-only JSON Lines file: one object per line, flushed per line, safe from any thread. When the file
/// passes <c>rotateBytes</c> it becomes &lt;name&gt;.1.jsonl (the older ones shift up, at most <c>keep</c> are kept).</summary>
public sealed class JsonlWriter : IDisposable
{
    private readonly string _path;
    private readonly long _rotateBytes;
    private readonly int _keep;
    private readonly object _gate = new();
    private FileStream? _file;

    internal JsonlWriter(string path, long rotateBytes, int keep)
    {
        _path = path;
        _rotateBytes = Math.Max(4096, rotateBytes);
        _keep = Math.Max(1, keep);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    }

    public string FilePath => _path;

    public void Append<T>(T value)
    {
        var line = JsonSerializer.SerializeToUtf8Bytes(value, Compact);
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _file ??= Open();
            if (_file.Length > 0 && _file.Length + line.Length + 1 > _rotateBytes)
            {
                _file.Dispose();
                Rotate();
                _file = Open();
            }
            _file.Write(line);
            _file.WriteByte((byte)'\n');
            _file.Flush();
        }
    }

    /// <summary>The paths of the current file and its rotated ones, newest first (for reading back: audit filter, export).</summary>
    public IReadOnlyList<string> Files()
    {
        var list = new List<string>();
        if (File.Exists(_path)) list.Add(_path);
        for (var i = 1; i <= _keep; i++)
            if (File.Exists(Rotated(i))) list.Add(Rotated(i));
        return list;
    }

    private FileStream Open() => new(_path, FileMode.Append, FileAccess.Write, FileShare.Read);

    private string Rotated(int i) => Path.ChangeExtension(_path, null) + $".{i}" + Path.GetExtension(_path);

    private void Rotate()
    {
        File.Delete(Rotated(_keep));
        for (var i = _keep - 1; i >= 1; i--)
            if (File.Exists(Rotated(i))) File.Move(Rotated(i), Rotated(i + 1));
        File.Move(_path, Rotated(1));
    }

    private bool _disposed;

    public void Dispose()
    {
        lock (_gate)
        {
            _disposed = true;
            _file?.Dispose();
            _file = null;
        }
    }

    private static readonly JsonSerializerOptions Compact = new(DataStore.Json) { WriteIndented = false };

    /// <summary>Reads the lines of one file as JSON elements, skipping unreadable lines (a torn last line after a crash).</summary>
    public static IEnumerable<JsonElement> ReadLines(string path)
    {
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(fs, Encoding.UTF8);
        while (reader.ReadLine() is { } line)
        {
            if (line.Length == 0) continue;
            JsonElement el;
            try
            {
                using var doc = JsonDocument.Parse(line);
                el = doc.RootElement.Clone();
            }
            catch (JsonException)
            {
                continue;
            }
            yield return el;
        }
    }
}

// SPDX-License-Identifier: MIT
using System.Collections;

namespace Kilnwarden.Http;

/// <summary>Header fields in arrival order; names compare case-insensitively (ASCII). Small (a request has ~10), so a
/// list beats a dictionary.</summary>
public sealed class HttpHeaders : IEnumerable<KeyValuePair<string, string>>
{
    private readonly List<KeyValuePair<string, string>> _items = [];

    public int Count => _items.Count;

    /// <summary>The first value of <paramref name="name"/>, or null.</summary>
    public string? this[string name] => Get(name);

    public string? Get(string name)
    {
        foreach (var kv in _items)
            if (string.Equals(kv.Key, name, StringComparison.OrdinalIgnoreCase)) return kv.Value;
        return null;
    }

    public int CountOf(string name)
    {
        var n = 0;
        foreach (var kv in _items)
            if (string.Equals(kv.Key, name, StringComparison.OrdinalIgnoreCase)) n++;
        return n;
    }

    public IEnumerable<string> GetAll(string name)
    {
        foreach (var kv in _items)
            if (string.Equals(kv.Key, name, StringComparison.OrdinalIgnoreCase)) yield return kv.Value;
    }

    public bool Contains(string name) => Get(name) is not null;

    public void Add(string name, string value) => _items.Add(new(name, value));

    /// <summary>Replaces every field of <paramref name="name"/> with one.</summary>
    public void Set(string name, string value)
    {
        Remove(name);
        _items.Add(new(name, value));
    }

    /// <summary>Adds the field unless one of that name is there.</summary>
    public void TryAdd(string name, string value)
    {
        if (!Contains(name)) _items.Add(new(name, value));
    }

    public int Remove(string name) => _items.RemoveAll(kv => string.Equals(kv.Key, name, StringComparison.OrdinalIgnoreCase));

    /// <summary>Whether a comma-separated header (Connection, Upgrade...) holds <paramref name="token"/>.</summary>
    public bool HasToken(string name, string token)
    {
        foreach (var v in GetAll(name))
            foreach (var part in v.Split(','))
                if (part.Trim().Equals(token, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    public IEnumerator<KeyValuePair<string, string>> GetEnumerator() => _items.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

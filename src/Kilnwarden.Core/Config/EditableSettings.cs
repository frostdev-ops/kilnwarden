// SPDX-License-Identifier: MIT
using System.Text.Json;
using System.Text.Json.Nodes;
using Kilnwarden.Hosting;

namespace Kilnwarden.Config;

/// <summary>A component's settings file the Panel settings page edits (GET/PUT /api/core/settings/files): read as JSON,
/// saved through its own <see cref="SettingsStore{T}"/> (validated, atomic, hot-reloaded).</summary>
public sealed class EditableFile(string id, string title, string file, Func<JsonNode?> get, Action<JsonNode> put, Func<IReadOnlyList<string>> problems)
{
    public string Id { get; } = id;
    public string Title { get; } = title;
    public string File { get; } = file;
    public JsonNode? Value => get();
    public IReadOnlyList<string> Problems => problems();

    /// <summary>Validates and saves; ArgumentException (with the problems) when the value is refused.</summary>
    public void Save(JsonNode value) => put(value);

    public static EditableFile For<T>(string id, string title, SettingsStore<T> store) where T : class, new() => new(id, title,
        Path.GetFileName(store.FilePath),
        () => JsonSerializer.SerializeToNode(store.Current, Storage.DataStore.Json),
        node =>
        {
            T? next;
            try
            {
                next = node.Deserialize<T>(Http.JsonBody.Options);
            }
            catch (Exception e) when (e is JsonException or NotSupportedException or InvalidOperationException or FormatException)
            {
                throw new ArgumentException("the settings do not have the expected shape: " + e.Message);
            }
            store.Save(next ?? throw new ArgumentException("the settings are empty"));
        },
        () => store.Problems);
}

/// <summary>The editable files of this host, in registration order (a service; made on first use).</summary>
public sealed class EditableSettings
{
    private readonly List<EditableFile> _files = [];

    public static EditableSettings Of(WebAdminServices services)
    {
        if (services.TryGet<EditableSettings>() is { } e) return e;
        var made = new EditableSettings();
        services.Add(made);
        return made;
    }

    public void Add(EditableFile f)
    {
        lock (_files)
        {
            if (_files.Any(x => x.Id == f.Id)) throw new InvalidOperationException($"settings file '{f.Id}' is registered twice");
            _files.Add(f);
        }
    }

    public IReadOnlyList<EditableFile> All
    {
        get
        {
            lock (_files) return [.. _files];
        }
    }

    public EditableFile? Find(string id) => All.FirstOrDefault(f => f.Id == id);
}

// SPDX-License-Identifier: MIT
using System.Collections.Concurrent;

namespace Kilnwarden.Hosting;

/// <summary>A typed bag of shared services, filled during Configure (the KAP client, the account store, ...) and read by
/// handlers through <see cref="Routing.RequestContext.Services"/>. One instance per service type.</summary>
public sealed class WebAdminServices
{
    private readonly ConcurrentDictionary<Type, object> _items = new();

    public void Add<T>(T instance) where T : class
    {
        ArgumentNullException.ThrowIfNull(instance);
        if (!_items.TryAdd(typeof(T), instance)) throw new InvalidOperationException($"service {typeof(T).Name} is already registered");
    }

    public T Get<T>() where T : class =>
        TryGet<T>() ?? throw new InvalidOperationException($"service {typeof(T).Name} is not registered");

    public T? TryGet<T>() where T : class => _items.TryGetValue(typeof(T), out var v) ? (T)v : null;
}

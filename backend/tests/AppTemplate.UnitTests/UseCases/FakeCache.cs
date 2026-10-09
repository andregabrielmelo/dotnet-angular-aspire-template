using AppTemplate.UseCases.Caching;

namespace AppTemplate.UnitTests.UseCases;

/// <summary>
/// In-memory <see cref="ICache"/> following the same contract as the real one: runs the factory
/// on a miss and doesn't store <c>null</c>. Records removals so tests can assert invalidation.
/// </summary>
public sealed class FakeCache : ICache
{
    private readonly Dictionary<string, object?> _entries = [];

    public List<string> RemovedKeys { get; } = [];

    public void Seed<T>(string key, T value) => _entries[key] = value;

    public bool Contains(string key) => _entries.ContainsKey(key);

    public async ValueTask<T> GetOrCreateAsync<T>(
        string key,
        Func<CancellationToken, ValueTask<T>> factory,
        CancellationToken cancellationToken = default
    )
    {
        if (_entries.TryGetValue(key, out var cached))
            return (T)cached!;

        var value = await factory(cancellationToken);
        if (value is not null)
            _entries[key] = value;
        return value;
    }

    public ValueTask RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        _entries.Remove(key);
        RemovedKeys.Add(key);
        return ValueTask.CompletedTask;
    }
}

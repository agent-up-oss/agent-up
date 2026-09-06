using AgentUp.Server.Features.Database.Interfaces;

namespace AgentUp.Server.Features.Database.Providers;

public sealed class DatabaseAdapterRegistry
{
    private readonly IReadOnlyDictionary<string, IDatabaseAdapter> _adapters;

    public DatabaseAdapterRegistry(IEnumerable<IDatabaseAdapter> adapters)
        => _adapters = adapters.ToDictionary(adapter => adapter.Engine, StringComparer.OrdinalIgnoreCase);

    public IDatabaseAdapter Resolve(string engine)
        => _adapters.TryGetValue(engine, out var adapter)
            ? adapter
            : throw new InvalidOperationException($"Database engine '{engine}' is not supported.");
}

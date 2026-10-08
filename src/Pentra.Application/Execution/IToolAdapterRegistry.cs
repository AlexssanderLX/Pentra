using Pentra.Domain.Abstractions;

namespace Pentra.Application.Execution;

/// <summary>Looks up the adapter responsible for a tool slug.</summary>
public interface IToolAdapterRegistry
{
    bool TryGet(string slug, out IToolAdapter adapter);

    IReadOnlyCollection<IToolAdapter> All { get; }
}

public sealed class ToolAdapterRegistry : IToolAdapterRegistry
{
    private readonly IReadOnlyDictionary<string, IToolAdapter> _bySlug;

    public ToolAdapterRegistry(IEnumerable<IToolAdapter> adapters)
    {
        _bySlug = adapters.ToDictionary(a => a.Slug, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyCollection<IToolAdapter> All => _bySlug.Values.ToList();

    public bool TryGet(string slug, out IToolAdapter adapter)
    {
        if (slug is not null && _bySlug.TryGetValue(slug, out var found))
        {
            adapter = found;
            return true;
        }

        adapter = null!;
        return false;
    }
}

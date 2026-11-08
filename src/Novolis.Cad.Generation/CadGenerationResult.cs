using Novolis.Cad.Hierarchy;
using Novolis.Cad.Primitives;

namespace Novolis.Cad.Generation;

/// <summary>Outputs of a successful (or partial) generation run.</summary>
public sealed class CadGenerationResult
{
    public required CadDocument Document { get; init; }

    public CadHierarchyRegistry? Hierarchy { get; init; }

    public IReadOnlyList<string> Diagnostics { get; init; } = [];

    public bool Ok { get; init; } = true;

    public string? OutputDirectory { get; init; }

    public IReadOnlyDictionary<string, string>? Metadata { get; init; }
}

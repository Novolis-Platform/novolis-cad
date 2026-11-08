using Novolis.Cad.Primitives;

namespace Novolis.Cad.Generation;

/// <summary>Inputs for a model generator. Whole-document replace is the default policy.</summary>
public sealed class CadGenerationRequest
{
    /// <summary>Optional existing document (generators may ignore and replace).</summary>
    public CadDocument? ExistingDocument { get; init; }

    /// <summary>Generator-specific options (lock paths, bay mode, …).</summary>
    public IReadOnlyDictionary<string, string>? Options { get; init; }

    /// <summary>When true, replace the entire document rather than merging.</summary>
    public bool ReplaceDocument { get; init; } = true;
}

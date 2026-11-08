namespace Novolis.Cad.Hierarchy;

/// <summary>Non-physical reference / datum / guide object.</summary>
public sealed record CadHierarchyReference(
    string Id,
    string DisplayName,
    AssemblyId? ParentAssemblyId = null,
    Guid? EntityId = null,
    IReadOnlyDictionary<string, string>? Metadata = null);

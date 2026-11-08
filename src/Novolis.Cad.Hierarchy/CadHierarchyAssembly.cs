namespace Novolis.Cad.Hierarchy;

/// <summary>Engineering or organizational assembly. Parent link is authoritative.</summary>
public sealed record CadHierarchyAssembly(
    AssemblyId Id,
    string DisplayName,
    AssemblyId? ParentAssemblyId = null,
    IReadOnlyList<PartId>? MemberPartIds = null,
    IReadOnlyDictionary<string, string>? Metadata = null);

namespace Novolis.Cad.Hierarchy;

/// <summary>Physical part with one authoritative parent assembly and optional CAD entity links.</summary>
public sealed record CadHierarchyPart(
    PartId Id,
    AssemblyId Assembly,
    string DisplayName,
    Guid? EntityId = null,
    IReadOnlyDictionary<string, string>? Metadata = null);

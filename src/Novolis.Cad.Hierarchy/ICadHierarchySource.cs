namespace Novolis.Cad.Hierarchy;

/// <summary>Read API for hierarchy walkers (visibility, drawing, explorer).</summary>
public interface ICadHierarchySource
{
    string RootId { get; }

    bool TryGetAssembly(AssemblyId id, out CadHierarchyAssembly assembly);

    bool TryGetPart(PartId id, out CadHierarchyPart part);

    bool TryGetReference(string id, out CadHierarchyReference reference);

    IEnumerable<CadHierarchyAssembly> ChildAssemblies(AssemblyId parent);

    IEnumerable<CadHierarchyPart> PartsOf(AssemblyId assembly);

    IReadOnlyCollection<Guid> CollectEntityIds(AssemblyId assembly);

    IReadOnlyCollection<Guid> CollectEntityIds(PartId part);

    string? ParentOf(string nodeId);
}

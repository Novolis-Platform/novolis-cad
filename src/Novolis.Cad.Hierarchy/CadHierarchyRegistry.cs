namespace Novolis.Cad.Hierarchy;

/// <summary>
/// In-memory semantic ownership registry for one document or generation context.
/// Not process-global — callers own the instance (multi-document safe).
/// </summary>
public sealed class CadHierarchyRegistry : ICadHierarchySource
{
    private readonly Dictionary<string, CadHierarchyAssembly> _assemblies = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, CadHierarchyPart> _parts = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, CadHierarchyReference> _references = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<Guid>> _reps = new(StringComparer.OrdinalIgnoreCase);

    public string RootId { get; set; } = "ROOT";

    public IReadOnlyCollection<CadHierarchyAssembly> Assemblies => _assemblies.Values;
    public IReadOnlyCollection<CadHierarchyPart> Parts => _parts.Values;
    public IReadOnlyCollection<CadHierarchyReference> References => _references.Values;

    public void Clear()
    {
        _assemblies.Clear();
        _parts.Clear();
        _references.Clear();
        _reps.Clear();
    }

    public CadHierarchyAssembly RegisterAssembly(CadHierarchyAssembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        if (_assemblies.TryGetValue(assembly.Id.Value, out var existing))
        {
            var members = existing.MemberPartIds?.ToList() ?? [];
            if (assembly.MemberPartIds is { Count: > 0 } incoming)
            {
                foreach (var m in incoming)
                {
                    if (!members.Any(x => string.Equals(x.Value, m.Value, StringComparison.OrdinalIgnoreCase)))
                        members.Add(m);
                }
            }

            var merged = existing with
            {
                DisplayName = string.IsNullOrWhiteSpace(assembly.DisplayName) ? existing.DisplayName : assembly.DisplayName,
                ParentAssemblyId = assembly.ParentAssemblyId ?? existing.ParentAssemblyId,
                MemberPartIds = members,
                Metadata = MergeMeta(existing.Metadata, assembly.Metadata),
            };
            _assemblies[assembly.Id.Value] = merged;
            return merged;
        }

        _assemblies[assembly.Id.Value] = assembly;
        return assembly;
    }

    public CadHierarchyPart RegisterPart(CadHierarchyPart part)
    {
        ArgumentNullException.ThrowIfNull(part);
        if (_parts.TryGetValue(part.Id.Value, out var existing))
        {
            if (existing.EntityId is { } oldId && part.EntityId is { } newId && oldId != newId)
                RememberRepresentation(part.Id.Value, newId);

            var merged = existing with
            {
                EntityId = existing.EntityId ?? part.EntityId,
                DisplayName = string.IsNullOrWhiteSpace(part.DisplayName) ? existing.DisplayName : part.DisplayName,
                Assembly = part.Assembly,
                Metadata = MergeMeta(existing.Metadata, part.Metadata),
            };
            _parts[part.Id.Value] = merged;
            if (merged.EntityId is { } primary)
                RememberRepresentation(part.Id.Value, primary);
            EnsurePartOnAssembly(merged);
            return merged;
        }

        _parts[part.Id.Value] = part;
        if (part.EntityId is { } eid)
            RememberRepresentation(part.Id.Value, eid);
        EnsurePartOnAssembly(part);
        return part;
    }

    public CadHierarchyReference RegisterReference(CadHierarchyReference reference)
    {
        ArgumentNullException.ThrowIfNull(reference);
        _references[reference.Id] = reference;
        if (reference.EntityId is { } eid)
            RememberRepresentation(reference.Id, eid);
        return reference;
    }

    public void AttachEntity(string nodeId, Guid entityId) => RememberRepresentation(nodeId, entityId);

    public bool TryGetAssembly(AssemblyId id, out CadHierarchyAssembly assembly) =>
        _assemblies.TryGetValue(id.Value, out assembly!);

    public bool TryGetPart(PartId id, out CadHierarchyPart part) =>
        _parts.TryGetValue(id.Value, out part!);

    public bool TryGetReference(string id, out CadHierarchyReference reference) =>
        _references.TryGetValue(id, out reference!);

    public IEnumerable<CadHierarchyAssembly> ChildAssemblies(AssemblyId parent) =>
        _assemblies.Values.Where(a =>
            a.ParentAssemblyId is { } p
            && string.Equals(p.Value, parent.Value, StringComparison.OrdinalIgnoreCase));

    public IEnumerable<CadHierarchyPart> PartsOf(AssemblyId assembly) =>
        _parts.Values.Where(p =>
            string.Equals(p.Assembly.Value, assembly.Value, StringComparison.OrdinalIgnoreCase));

    public IReadOnlyCollection<Guid> CollectEntityIds(AssemblyId assembly)
    {
        var ids = new HashSet<Guid>();
        CollectEntityIdsRecursive(assembly, ids);
        return ids;
    }

    public IReadOnlyCollection<Guid> CollectEntityIds(PartId part)
    {
        var ids = new HashSet<Guid>();
        if (_reps.TryGetValue(part.Value, out var list))
        {
            foreach (var id in list)
                ids.Add(id);
        }

        if (ids.Count == 0 && TryGetPart(part, out var p) && p.EntityId is { } primary)
            ids.Add(primary);
        return ids;
    }

    public string? ParentOf(string nodeId)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
            return null;
        if (_assemblies.TryGetValue(nodeId, out var asm))
            return asm.ParentAssemblyId?.Value;
        if (_parts.TryGetValue(nodeId, out var part))
            return part.Assembly.Value;
        if (_references.TryGetValue(nodeId, out var reference))
            return reference.ParentAssemblyId?.Value;
        return null;
    }

    private void CollectEntityIdsRecursive(AssemblyId assembly, HashSet<Guid> ids)
    {
        foreach (var part in PartsOf(assembly))
        {
            foreach (var eid in CollectEntityIds(part.Id))
                ids.Add(eid);
        }

        foreach (var child in ChildAssemblies(assembly))
            CollectEntityIdsRecursive(child.Id, ids);

        if (_reps.TryGetValue(assembly.Value, out var list))
        {
            foreach (var id in list)
                ids.Add(id);
        }
    }

    private void EnsurePartOnAssembly(CadHierarchyPart part)
    {
        if (!TryGetAssembly(part.Assembly, out var asm))
            return;
        var members = asm.MemberPartIds?.ToList() ?? [];
        if (members.Any(m => string.Equals(m.Value, part.Id.Value, StringComparison.OrdinalIgnoreCase)))
            return;
        members.Add(part.Id);
        _assemblies[asm.Id.Value] = asm with { MemberPartIds = members };
    }

    private void RememberRepresentation(string nodeId, Guid entityId)
    {
        if (!_reps.TryGetValue(nodeId, out var list))
        {
            list = [];
            _reps[nodeId] = list;
        }

        if (!list.Contains(entityId))
            list.Add(entityId);
    }

    private static IReadOnlyDictionary<string, string>? MergeMeta(
        IReadOnlyDictionary<string, string>? a,
        IReadOnlyDictionary<string, string>? b)
    {
        if (a is null || a.Count == 0) return b;
        if (b is null || b.Count == 0) return a;
        var d = new Dictionary<string, string>(a, StringComparer.OrdinalIgnoreCase);
        foreach (var (k, v) in b)
            d[k] = v;
        return d;
    }
}

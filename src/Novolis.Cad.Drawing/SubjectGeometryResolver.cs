using Novolis.Cad.Hierarchy;
using Novolis.Cad.Primitives;

namespace Novolis.Cad.Drawing;

/// <summary>Resolves a hierarchy node into drawable geometry from a CadDocument.</summary>
public static class SubjectGeometryResolver
{
    public sealed record ResolveResult(
        string SubjectId,
        IReadOnlyList<DrawableGeometry> Geometry,
        IReadOnlyList<string> Diagnostics,
        bool HasDrawableGeometry);

    public static ResolveResult Resolve(
        string subjectId,
        ICadHierarchySource hierarchy,
        CadDocument document)
    {
        var diagnostics = new List<string>();
        var entityToNode = new Dictionary<Guid, string>();
        CollectOwnedEntities(subjectId, hierarchy, entityToNode, diagnostics);

        if (entityToNode.Count == 0
            && hierarchy.TryGetReference(subjectId, out var reference)
            && reference.EntityId is { } refEid)
            entityToNode[refEid] = subjectId;

        var byId = document.Entities.ToDictionary(e => e.Id);
        var geometry = new List<DrawableGeometry>();
        foreach (var (eid, nodeId) in entityToNode)
        {
            if (!byId.TryGetValue(eid, out var entity))
            {
                diagnostics.Add($"Missing entity {eid:N} for node {nodeId}.");
                continue;
            }

            var g = CadGeometryNormalizer.Normalize(entity, nodeId);
            if (g.Kind == DrawablePrimitiveKind.Unsupported)
            {
                diagnostics.Add($"{g.SourceName}: {g.UnsupportedReason}");
                continue;
            }

            geometry.Add(g);
        }

        if (geometry.Count == 0)
            diagnostics.Add($"Subject '{subjectId}' has no drawable geometry.");

        return new ResolveResult(subjectId, geometry, diagnostics, geometry.Count > 0);
    }

    public static IReadOnlyList<CadHierarchyPart> CollectPhysicalParts(
        string subjectId,
        ICadHierarchySource hierarchy)
    {
        var parts = new List<CadHierarchyPart>();
        if (hierarchy.TryGetPart(PartId.Of(subjectId), out var part))
        {
            parts.Add(part);
            return parts;
        }

        if (!hierarchy.TryGetAssembly(AssemblyId.Of(subjectId), out _))
            return parts;

        CollectPartsRecursive(AssemblyId.Of(subjectId), hierarchy, parts);
        return parts;
    }

    private static void CollectPartsRecursive(
        AssemblyId assembly,
        ICadHierarchySource hierarchy,
        List<CadHierarchyPart> parts)
    {
        parts.AddRange(hierarchy.PartsOf(assembly));
        foreach (var child in hierarchy.ChildAssemblies(assembly))
            CollectPartsRecursive(child.Id, hierarchy, parts);
    }

    private static void CollectOwnedEntities(
        string subjectId,
        ICadHierarchySource hierarchy,
        Dictionary<Guid, string> entityToNode,
        List<string> diagnostics)
    {
        if (hierarchy.TryGetPart(PartId.Of(subjectId), out var part))
        {
            foreach (var id in hierarchy.CollectEntityIds(part.Id))
                entityToNode[id] = part.Id.Value;
            return;
        }

        if (hierarchy.TryGetAssembly(AssemblyId.Of(subjectId), out var asm))
        {
            foreach (var id in hierarchy.CollectEntityIds(asm.Id))
                entityToNode[id] = subjectId;
            return;
        }

        if (hierarchy.TryGetReference(subjectId, out _))
            return;

        diagnostics.Add($"Unknown subject '{subjectId}'.");
    }
}

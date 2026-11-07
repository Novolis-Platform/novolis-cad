using Novolis.Cad.Primitives;
using Novolis.Math.Geometry;

namespace Novolis.Cad.SceneBridge;

/// <summary>
/// Tessellates a <see cref="CadDocument"/> into named meshes for interchange export.
/// Filter stays with the caller (domain); this only does geometry collection.
/// </summary>
public static class CadMeshCollector
{
    /// <summary>
    /// Collects tessellated meshes. When <paramref name="include"/> is null, every
    /// successfully tessellated entity is kept.
    /// </summary>
    public static IReadOnlyList<CadNamedMesh> Collect(
        CadDocument document,
        Func<CadEntity, bool>? include = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        var result = new List<CadNamedMesh>(document.Entities.Count);
        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var entity in document.Entities)
        {
            if (include is not null && !include(entity))
                continue;

            var mesh = CadEntityTessellator.TryTessellate(entity);
            if (mesh is null || mesh.VertexCount == 0 || mesh.TriangleCount == 0)
                continue;

            var name = UniqueName(entity.Name ?? entity.Kind, usedNames);
            result.Add(new CadNamedMesh(name, mesh, entity));
        }

        return result;
    }

    /// <summary>Concatenates parts into one mesh (shared vertex buffer, no weld).</summary>
    public static EditableMesh Merge(IEnumerable<CadNamedMesh> parts)
    {
        ArgumentNullException.ThrowIfNull(parts);
        EditableMesh? merged = null;
        foreach (var part in parts)
        {
            merged = merged is null
                ? part.Mesh.Clone()
                : MeshBoolean.Concat(merged, part.Mesh);
        }

        return merged ?? new EditableMesh();
    }

    private static string UniqueName(string raw, HashSet<string> used)
    {
        var baseName = string.IsNullOrWhiteSpace(raw) ? "part" : raw.Trim();
        if (used.Add(baseName))
            return baseName;

        for (var i = 2; ; i++)
        {
            var candidate = $"{baseName}_{i}";
            if (used.Add(candidate))
                return candidate;
        }
    }
}

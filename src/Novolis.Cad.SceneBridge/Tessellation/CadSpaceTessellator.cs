using System.Numerics;
using Novolis.Cad.Primitives;
using Novolis.Math.Geometry;

namespace Novolis.Cad.SceneBridge.Tessellation;

/// <summary>Tessellates space footprints into thin floor (and optional ceiling) plates.</summary>
public static class CadSpaceTessellator
{
    public static EditableMesh? TryTessellate(CadEntity space, bool includeCeiling = false)
    {
        ArgumentNullException.ThrowIfNull(space);
        if (!string.Equals(space.Kind, "space", StringComparison.OrdinalIgnoreCase))
            return null;
        if (space.Points is not { Count: >= 3 })
            return null;

        var lift = space.Deck * CadVec.DeckHeightMeters;
        var ring = space.Points.Select(p => CadVec.To(p) + new Vector3(0, lift, 0)).ToArray();
        var min = ring[0];
        var max = ring[0];
        foreach (var p in ring)
        {
            min = Vector3.Min(min, p);
            max = Vector3.Max(max, p);
        }

        var sx = MathF.Max(0.2f, max.X - min.X) * 0.5f;
        var sz = MathF.Max(0.2f, max.Z - min.Z) * 0.5f;
        var center = new Vector3((min.X + max.X) * 0.5f, min.Y + 0.03f, (min.Z + max.Z) * 0.5f);
        var floor = CadSolidTessellator.Box(new Vector3(sx, 0.03f, sz));
        floor.Transform(Matrix4x4.CreateTranslation(center));

        if (!includeCeiling)
            return floor;

        var height = MathF.Max(0.5f, space.Height <= 0 ? 2.4f : space.Height);
        var ceilingCenter = center + new Vector3(0, height, 0);
        var ceiling = CadSolidTessellator.Box(new Vector3(sx, 0.03f, sz));
        ceiling.Transform(Matrix4x4.CreateTranslation(ceilingCenter));

        var verts = floor.Vertices.ToList();
        var inds = floor.Indices.ToList();
        var baseIndex = verts.Count;
        verts.AddRange(ceiling.Vertices);
        foreach (var i in ceiling.Indices)
            inds.Add(baseIndex + i);
        return new EditableMesh(verts, inds);
    }
}

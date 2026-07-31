using System.Numerics;
using Novolis.Cad.Primitives;
using Novolis.Math.Geometry;

namespace Novolis.Cad.SceneBridge.Tessellation;

/// <summary>Extrudes CAD wall segments into closed slab meshes (world-space).</summary>
public static class CadWallTessellator
{
    public static EditableMesh? TryTessellate(CadEntity wall)
    {
        ArgumentNullException.ThrowIfNull(wall);
        if (!string.Equals(wall.Kind, "wall", StringComparison.OrdinalIgnoreCase))
            return null;

        var lift = wall.Deck * CadVec.DeckHeightMeters;
        var h = MathF.Max(0.5f, wall.Height <= 0 ? 2.4f : wall.Height);
        var thickness = MathF.Max(0.08f, wall.Thickness <= 0 ? 0.15f : wall.Thickness);

        var verts = new List<Vector3>();
        var inds = new List<int>();
        foreach (var (a, b) in EnumerateSegments(wall, lift))
        {
            var dir = b - a;
            dir.Y = 0;
            var length = dir.Length();
            if (length < 1e-4f)
                continue;
            dir /= length;
            var mid = (a + b) * 0.5f + new Vector3(0, h * 0.5f, 0);
            AppendOrientedBox(verts, inds, mid, dir, length, h, thickness);
        }

        return verts.Count == 0 ? null : new EditableMesh(verts, inds);
    }

    private static IEnumerable<(Vector3 A, Vector3 B)> EnumerateSegments(CadEntity wall, float lift)
    {
        if (wall.Points is { Count: >= 2 } pts)
        {
            for (var i = 0; i < pts.Count - 1; i++)
            {
                yield return (
                    CadVec.To(pts[i]) + new Vector3(0, lift, 0),
                    CadVec.To(pts[i + 1]) + new Vector3(0, lift, 0));
            }

            yield break;
        }

        if (wall.A is not null && wall.B is not null)
        {
            yield return (
                CadVec.To(wall.A) + new Vector3(0, lift, 0),
                CadVec.To(wall.B) + new Vector3(0, lift, 0));
        }
    }

    private static void AppendOrientedBox(
        List<Vector3> verts,
        List<int> inds,
        Vector3 center,
        Vector3 dirAlong,
        float length,
        float height,
        float thickness)
    {
        var along = dirAlong;
        var up = Vector3.UnitY;
        var right = Vector3.Cross(up, along);
        if (right.LengthSquared() < 1e-8f)
            right = Vector3.UnitX;
        else
            right = Vector3.Normalize(right);

        var hx = length * 0.5f;
        var hy = height * 0.5f;
        var hz = thickness * 0.5f;
        Vector3 Corner(float x, float y, float z) =>
            center + along * x + up * y + right * z;

        var baseIndex = verts.Count;
        verts.Add(Corner(-hx, -hy, -hz));
        verts.Add(Corner(hx, -hy, -hz));
        verts.Add(Corner(hx, hy, -hz));
        verts.Add(Corner(-hx, hy, -hz));
        verts.Add(Corner(-hx, -hy, hz));
        verts.Add(Corner(hx, -hy, hz));
        verts.Add(Corner(hx, hy, hz));
        verts.Add(Corner(-hx, hy, hz));

        int[] local =
        [
            0, 1, 2, 0, 2, 3,
            4, 6, 5, 4, 7, 6,
            0, 4, 5, 0, 5, 1,
            2, 6, 7, 2, 7, 3,
            0, 3, 7, 0, 7, 4,
            1, 5, 6, 1, 6, 2,
        ];
        foreach (var i in local)
            inds.Add(baseIndex + i);
    }
}

using System.Numerics;
using Novolis.Math.Geometry;

namespace Novolis.Modeling.Scene;

/// <summary>Lightweight local shaping ops when Math.Geometry has no dedicated kernel.</summary>
public static class MeshShaping
{
    public static EditableMesh Subdivide(EditableMesh mesh, int levels)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        var work = mesh.Clone();
        for (var level = 0; level < System.Math.Max(0, levels); level++)
            work = SubdivideOnce(work);
        return work;
    }

    public static EditableMesh Extrude(EditableMesh mesh, float distance)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        var work = mesh.Clone();
        if (work.TriangleCount == 0)
            return work;

        var normals = new Vector3[work.VertexCount];
        var counts = new int[work.VertexCount];
        for (var t = 0; t < work.TriangleCount; t++)
        {
            var i0 = work.Indices[t * 3];
            var i1 = work.Indices[t * 3 + 1];
            var i2 = work.Indices[t * 3 + 2];
            var n = Vector3.Normalize(Vector3.Cross(
                work.Vertices[i1] - work.Vertices[i0],
                work.Vertices[i2] - work.Vertices[i0]));
            normals[i0] += n;
            normals[i1] += n;
            normals[i2] += n;
            counts[i0]++;
            counts[i1]++;
            counts[i2]++;
        }

        for (var i = 0; i < work.VertexCount; i++)
        {
            if (counts[i] == 0)
                continue;
            var n = Vector3.Normalize(normals[i] / counts[i]);
            work.SetVertex(i, work.Vertices[i] + n * distance);
        }

        return work;
    }

    /// <summary>Lite bevel: pull vertices near AABB corners slightly toward center.</summary>
    public static EditableMesh BevelLite(EditableMesh mesh, float amount)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        var work = mesh.Clone();
        if (work.VertexCount == 0)
            return work;

        var min = work.Vertices[0];
        var max = work.Vertices[0];
        foreach (var v in work.Vertices)
        {
            min = Vector3.Min(min, v);
            max = Vector3.Max(max, v);
        }

        var center = (min + max) * 0.5f;
        var extent = max - min;
        var thresh = MathF.Max(0.05f, MathF.Min(extent.X, MathF.Min(extent.Y, extent.Z)) * 0.15f);
        var pull = System.Math.Clamp(amount, 0.01f, 0.5f);

        for (var i = 0; i < work.VertexCount; i++)
        {
            var v = work.Vertices[i];
            var nearCorner =
                (v.X < min.X + thresh || v.X > max.X - thresh ? 1 : 0)
                + (v.Y < min.Y + thresh || v.Y > max.Y - thresh ? 1 : 0)
                + (v.Z < min.Z + thresh || v.Z > max.Z - thresh ? 1 : 0);
            if (nearCorner >= 2)
                work.SetVertex(i, Vector3.Lerp(v, center, pull * 0.35f));
        }

        return work;
    }

    private static EditableMesh SubdivideOnce(EditableMesh mesh)
    {
        var result = new EditableMesh();
        for (var i = 0; i < mesh.VertexCount; i++)
            result.AddVertex(mesh.Vertices[i]);

        var midCache = new Dictionary<(int, int), int>();

        int Midpoint(int a, int b)
        {
            var key = a < b ? (a, b) : (b, a);
            if (midCache.TryGetValue(key, out var existing))
                return existing;
            var m = (mesh.Vertices[a] + mesh.Vertices[b]) * 0.5f;
            var id = result.AddVertex(m);
            midCache[key] = id;
            return id;
        }

        for (var t = 0; t < mesh.TriangleCount; t++)
        {
            var i0 = mesh.Indices[t * 3];
            var i1 = mesh.Indices[t * 3 + 1];
            var i2 = mesh.Indices[t * 3 + 2];
            var m01 = Midpoint(i0, i1);
            var m12 = Midpoint(i1, i2);
            var m20 = Midpoint(i2, i0);
            result.AddTriangle(i0, m01, m20);
            result.AddTriangle(i1, m12, m01);
            result.AddTriangle(i2, m20, m12);
            result.AddTriangle(m01, m12, m20);
        }

        return result;
    }
}

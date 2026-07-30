using System.Numerics;
using Novolis.Math.Geometry;

namespace Novolis.Modeling.Scene;

/// <summary>Selection-aware polygon modeling ops on <see cref="EditableMesh"/>.</summary>
public static class MeshComponentOps
{
    public static EditableMesh ExtrudeFaces(EditableMesh mesh, IReadOnlyCollection<int> faceIndices, float distance)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        ArgumentNullException.ThrowIfNull(faceIndices);
        if (faceIndices.Count == 0 || MathF.Abs(distance) < 1e-8f)
            return mesh.Clone();

        var selected = faceIndices.Where(f => f >= 0 && f < mesh.TriangleCount).Distinct().ToHashSet();
        if (selected.Count == 0)
            return mesh.Clone();

        var result = new EditableMesh();
        for (var i = 0; i < mesh.VertexCount; i++)
            result.AddVertex(mesh.Vertices[i]);

        var vertMap = new Dictionary<int, int>();
        var avg = Vector3.Zero;
        var normalCount = 0;
        foreach (var f in selected)
        {
            var i0 = mesh.Indices[f * 3];
            var i1 = mesh.Indices[f * 3 + 1];
            var i2 = mesh.Indices[f * 3 + 2];
            avg += Vector3.Normalize(Vector3.Cross(
                mesh.Vertices[i1] - mesh.Vertices[i0],
                mesh.Vertices[i2] - mesh.Vertices[i0]));
            normalCount++;
            Ensure(i0);
            Ensure(i1);
            Ensure(i2);
        }

        avg = normalCount > 0 ? Vector3.Normalize(avg / normalCount) : Vector3.UnitY;
        foreach (var (old, neu) in vertMap)
            result.SetVertex(neu, mesh.Vertices[old] + avg * distance);

        for (var f = 0; f < mesh.TriangleCount; f++)
        {
            var i0 = mesh.Indices[f * 3];
            var i1 = mesh.Indices[f * 3 + 1];
            var i2 = mesh.Indices[f * 3 + 2];
            if (!selected.Contains(f))
            {
                result.AddTriangle(i0, i1, i2);
                continue;
            }

            var n0 = vertMap[i0];
            var n1 = vertMap[i1];
            var n2 = vertMap[i2];
            result.AddTriangle(n0, n1, n2);
            result.AddTriangle(i0, i1, n1);
            result.AddTriangle(i0, n1, n0);
            result.AddTriangle(i1, i2, n2);
            result.AddTriangle(i1, n2, n1);
            result.AddTriangle(i2, i0, n0);
            result.AddTriangle(i2, n0, n2);
        }

        return result;

        void Ensure(int old)
        {
            if (vertMap.ContainsKey(old))
                return;
            vertMap[old] = result.AddVertex(mesh.Vertices[old]);
        }
    }

    public static EditableMesh InsetFaces(EditableMesh mesh, IReadOnlyCollection<int> faceIndices, float amount)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        var selected = faceIndices.Where(f => f >= 0 && f < mesh.TriangleCount).Distinct().ToHashSet();
        if (selected.Count == 0)
            return mesh.Clone();

        var t = System.Math.Clamp(amount, 0.01f, 0.49f);
        var result = new EditableMesh();
        for (var i = 0; i < mesh.VertexCount; i++)
            result.AddVertex(mesh.Vertices[i]);

        for (var f = 0; f < mesh.TriangleCount; f++)
        {
            var i0 = mesh.Indices[f * 3];
            var i1 = mesh.Indices[f * 3 + 1];
            var i2 = mesh.Indices[f * 3 + 2];
            if (!selected.Contains(f))
            {
                result.AddTriangle(i0, i1, i2);
                continue;
            }

            var c = (mesh.Vertices[i0] + mesh.Vertices[i1] + mesh.Vertices[i2]) / 3f;
            var n0 = result.AddVertex(Vector3.Lerp(mesh.Vertices[i0], c, t));
            var n1 = result.AddVertex(Vector3.Lerp(mesh.Vertices[i1], c, t));
            var n2 = result.AddVertex(Vector3.Lerp(mesh.Vertices[i2], c, t));
            result.AddTriangle(n0, n1, n2);
            result.AddTriangle(i0, i1, n1);
            result.AddTriangle(i0, n1, n0);
            result.AddTriangle(i1, i2, n2);
            result.AddTriangle(i1, n2, n1);
            result.AddTriangle(i2, i0, n0);
            result.AddTriangle(i2, n0, n2);
        }

        return result;
    }

    public static EditableMesh BevelEdges(EditableMesh mesh, IReadOnlyCollection<(int A, int B)> edges, float amount)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        var work = mesh.Clone();
        if (edges.Count == 0)
            return work;

        var pull = System.Math.Clamp(amount, 0.01f, 0.5f);
        var verts = new HashSet<int>();
        foreach (var (a, b) in edges)
        {
            verts.Add(a);
            verts.Add(b);
        }

        var center = Vector3.Zero;
        foreach (var v in work.Vertices)
            center += v;
        center /= MathF.Max(1, work.VertexCount);

        foreach (var i in verts)
        {
            if (i < 0 || i >= work.VertexCount)
                continue;
            work.SetVertex(i, Vector3.Lerp(work.Vertices[i], center, pull * 0.35f));
        }

        return work;
    }

    public static EditableMesh DissolveFaces(EditableMesh mesh, IReadOnlyCollection<int> faceIndices)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        var drop = faceIndices.Where(f => f >= 0 && f < mesh.TriangleCount).ToHashSet();
        if (drop.Count == 0)
            return mesh.Clone();

        var result = new EditableMesh();
        for (var i = 0; i < mesh.VertexCount; i++)
            result.AddVertex(mesh.Vertices[i]);
        for (var f = 0; f < mesh.TriangleCount; f++)
        {
            if (drop.Contains(f))
                continue;
            result.AddTriangle(mesh.Indices[f * 3], mesh.Indices[f * 3 + 1], mesh.Indices[f * 3 + 2]);
        }

        return result;
    }

    public static EditableMesh DissolveEdges(EditableMesh mesh, IReadOnlyCollection<(int A, int B)> edges)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        var work = mesh.Clone();
        foreach (var (a, b) in edges)
        {
            if (a < 0 || b < 0 || a >= work.VertexCount || b >= work.VertexCount || a == b)
                continue;
            var mid = (work.Vertices[a] + work.Vertices[b]) * 0.5f;
            work.SetVertex(a, mid);
            work.SetVertex(b, mid);
        }

        return MeshWeld.Apply(work, new WeldOptions(Tolerance: 1e-4f));
    }

    public static EditableMesh Knife(EditableMesh mesh, Plane plane)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        var split = MeshPlaneSplit.Split(mesh, plane);
        return MeshBoolean.Concat(split.Positive, split.Negative);
    }

    public static EditableMesh BridgeSelectedEdges(EditableMesh mesh, IReadOnlyList<(int A, int B)> edges)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        _ = edges;
        var loops = mesh.FindBoundaryLoops();
        if (loops.Count >= 2 && loops[0].Count == loops[1].Count && loops[0].Count >= 3)
            return MeshBridge.Apply(mesh, loops[0], loops[1]);

        return mesh.Clone();
    }

    public static EditableMesh MoveVertices(EditableMesh mesh, IReadOnlyCollection<int> vertexIndices, Vector3 delta)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        var work = mesh.Clone();
        foreach (var i in vertexIndices)
        {
            if (i < 0 || i >= work.VertexCount)
                continue;
            work.SetVertex(i, work.Vertices[i] + delta);
        }

        return work;
    }

    public static EditableMesh MoveFaces(EditableMesh mesh, IReadOnlyCollection<int> faceIndices, Vector3 delta)
    {
        var verts = new HashSet<int>();
        foreach (var f in faceIndices)
        {
            if (f < 0 || f >= mesh.TriangleCount)
                continue;
            verts.Add(mesh.Indices[f * 3]);
            verts.Add(mesh.Indices[f * 3 + 1]);
            verts.Add(mesh.Indices[f * 3 + 2]);
        }

        return MoveVertices(mesh, verts, delta);
    }

    public static EditableMesh MoveEdges(EditableMesh mesh, IReadOnlyCollection<(int A, int B)> edges, Vector3 delta)
    {
        var verts = new HashSet<int>();
        foreach (var (a, b) in edges)
        {
            verts.Add(a);
            verts.Add(b);
        }

        return MoveVertices(mesh, verts, delta);
    }
}

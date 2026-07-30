using System.Numerics;

namespace Novolis.Modeling.Scene;

public readonly record struct MeshPickHit(Guid SourceId, SceneEditMode Mode, int Index, int IndexB, float Distance);

/// <summary>Closest vertex / edge / triangle under a world-space ray.</summary>
public static class MeshPicker
{
    public static MeshPickHit? Pick(
        IReadOnlyList<EvaluatedMesh> meshes,
        Ray ray,
        SceneEditMode mode,
        float maxDistance = 500f,
        float pointPixelTolerance = 0.15f,
        float edgePixelTolerance = 0.12f)
    {
        MeshPickHit? best = null;
        foreach (var mesh in meshes)
        {
            var hit = mode switch
            {
                SceneEditMode.Point => PickVertex(mesh, ray, pointPixelTolerance, maxDistance),
                SceneEditMode.Edge => PickEdge(mesh, ray, edgePixelTolerance, maxDistance),
                SceneEditMode.Polygon => PickFace(mesh, ray, maxDistance),
                _ => PickObject(mesh, ray, maxDistance),
            };
            if (hit is null)
                continue;
            if (best is null || hit.Value.Distance < best.Value.Distance)
                best = hit;
        }

        return best;
    }

    public static Ray ScreenRay(Vector3 eye, Vector3 target, Vector3 up, float fovDeg, float aspect, float ndcX, float ndcY)
    {
        var forward = Vector3.Normalize(target - eye);
        var right = Vector3.Normalize(Vector3.Cross(forward, up));
        var camUp = Vector3.Normalize(Vector3.Cross(right, forward));
        var tan = MathF.Tan(fovDeg * MathF.PI / 360f);
        var dir = Vector3.Normalize(forward + right * (ndcX * tan * aspect) + camUp * (ndcY * tan));
        return new Ray(eye, dir);
    }

    private static MeshPickHit? PickObject(EvaluatedMesh mesh, Ray ray, float maxDistance)
    {
        var face = PickFace(mesh, ray, maxDistance);
        return face is null
            ? null
            : new MeshPickHit(mesh.SourceId, SceneEditMode.Object, -1, -1, face.Value.Distance);
    }

    private static MeshPickHit? PickVertex(EvaluatedMesh mesh, Ray ray, float tol, float maxDistance)
    {
        MeshPickHit? best = null;
        for (var i = 0; i < mesh.Vertices.Length; i++)
        {
            var p = Vector3.Transform(mesh.Vertices[i], mesh.World);
            var dist = DistancePointRay(p, ray);
            var along = Vector3.Dot(p - ray.Position, ray.Direction);
            if (along < 0 || along > maxDistance || dist > tol)
                continue;
            if (best is null || along < best.Value.Distance)
                best = new MeshPickHit(mesh.SourceId, SceneEditMode.Point, i, -1, along);
        }

        return best;
    }

    private static MeshPickHit? PickEdge(EvaluatedMesh mesh, Ray ray, float tol, float maxDistance)
    {
        MeshPickHit? best = null;
        var seen = new HashSet<(int, int)>();
        for (var t = 0; t < mesh.Indices.Length; t += 3)
        {
            TryEdge(mesh.Indices[t], mesh.Indices[t + 1]);
            TryEdge(mesh.Indices[t + 1], mesh.Indices[t + 2]);
            TryEdge(mesh.Indices[t + 2], mesh.Indices[t]);
        }

        return best;

        void TryEdge(int a, int b)
        {
            var key = a < b ? (a, b) : (b, a);
            if (!seen.Add(key))
                return;
            var pa = Vector3.Transform(mesh.Vertices[key.Item1], mesh.World);
            var pb = Vector3.Transform(mesh.Vertices[key.Item2], mesh.World);
            if (!ClosestPointsRaySegment(ray, pa, pb, out var along, out var dist) || along < 0 || along > maxDistance || dist > tol)
                return;
            if (best is null || along < best.Value.Distance)
                best = new MeshPickHit(mesh.SourceId, SceneEditMode.Edge, key.Item1, key.Item2, along);
        }
    }

    private static MeshPickHit? PickFace(EvaluatedMesh mesh, Ray ray, float maxDistance)
    {
        MeshPickHit? best = null;
        for (var t = 0; t < mesh.Indices.Length; t += 3)
        {
            var a = Vector3.Transform(mesh.Vertices[mesh.Indices[t]], mesh.World);
            var b = Vector3.Transform(mesh.Vertices[mesh.Indices[t + 1]], mesh.World);
            var c = Vector3.Transform(mesh.Vertices[mesh.Indices[t + 2]], mesh.World);
            if (!RayTriangle(ray, a, b, c, out var dist) || dist < 0 || dist > maxDistance)
                continue;
            if (best is null || dist < best.Value.Distance)
                best = new MeshPickHit(mesh.SourceId, SceneEditMode.Polygon, t / 3, -1, dist);
        }

        return best;
    }

    private static float DistancePointRay(Vector3 point, Ray ray)
    {
        var w = point - ray.Position;
        var proj = Vector3.Dot(w, ray.Direction);
        var closest = ray.Position + ray.Direction * MathF.Max(0, proj);
        return Vector3.Distance(point, closest);
    }

    private static bool ClosestPointsRaySegment(Ray ray, Vector3 a, Vector3 b, out float alongRay, out float distance)
    {
        var u = ray.Direction;
        var v = b - a;
        var w0 = ray.Position - a;
        var aa = Vector3.Dot(u, u);
        var bb = Vector3.Dot(v, v);
        var ab = Vector3.Dot(u, v);
        var aw = Vector3.Dot(u, w0);
        var bw = Vector3.Dot(v, w0);
        var denom = aa * bb - ab * ab;
        float s, t;
        if (MathF.Abs(denom) < 1e-8f)
        {
            s = 0;
            t = bb > 1e-8f ? bw / bb : 0;
        }
        else
        {
            s = (ab * bw - bb * aw) / denom;
            t = (aa * bw - ab * aw) / denom;
        }

        t = System.Math.Clamp(t, 0f, 1f);
        s = MathF.Max(0f, s);
        var pRay = ray.Position + u * s;
        var pSeg = a + v * t;
        alongRay = s;
        distance = Vector3.Distance(pRay, pSeg);
        return true;
    }

    private static bool RayTriangle(Ray ray, Vector3 v0, Vector3 v1, Vector3 v2, out float t)
    {
        t = 0;
        const float eps = 1e-6f;
        var e1 = v1 - v0;
        var e2 = v2 - v0;
        var p = Vector3.Cross(ray.Direction, e2);
        var det = Vector3.Dot(e1, p);
        if (MathF.Abs(det) < eps)
            return false;
        var inv = 1f / det;
        var tv = ray.Position - v0;
        var u = Vector3.Dot(tv, p) * inv;
        if (u < 0 || u > 1)
            return false;
        var q = Vector3.Cross(tv, e1);
        var v = Vector3.Dot(ray.Direction, q) * inv;
        if (v < 0 || u + v > 1)
            return false;
        t = Vector3.Dot(e2, q) * inv;
        return t >= 0;
    }
}

public readonly record struct Ray(Vector3 Position, Vector3 Direction);

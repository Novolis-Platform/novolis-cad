using System.Numerics;
using Assimp;
using Novolis.Math.Geometry;
using NumericsMatrix = System.Numerics.Matrix4x4;

namespace Novolis.Modeling.Import;

/// <summary>Per-vertex bone influences keyed by authoring bone name (Mixamo / FBX).</summary>
public readonly record struct AssimpNamedBoneWeight(string BoneName, float Weight);

/// <summary>Geometry plus optional Assimp skin weights (no Humanoid retarget yet).</summary>
public sealed class AssimpNamedSkinImport
{
    public required TriangleMesh Mesh { get; init; }

    /// <summary>Length equals <see cref="TriangleMesh.VertexCount"/>; empty arrays when a vertex has no weights.</summary>
    public required IReadOnlyList<AssimpNamedBoneWeight[]> VertexWeights { get; init; }

    public bool HasSkinning => VertexWeights.Any(w => w.Length > 0);
}

/// <summary>
/// Assimp import that preserves bone weights when present (does not use PreTransformVertices,
/// which would destroy the skin). Returns null when the file has no skinned meshes.
/// </summary>
public static class AssimpSkinnedMeshImporter
{
    public static bool TryImport(string path, out AssimpNamedSkinImport? result, MeshImportOptions? options = null)
    {
        result = null;
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!File.Exists(path))
            return false;

        options ??= new MeshImportOptions { PreTransformVertices = false, GenerateNormals = true };
        // Skinning requires hierarchy — never pre-transform.
        options = new MeshImportOptions
        {
            TargetLengthMeters = options.TargetLengthMeters,
            CenterAtOrigin = options.CenterAtOrigin,
            LongestAxisToPositiveZ = options.LongestAxisToPositiveZ,
            PreTransformVertices = false,
            GenerateNormals = options.GenerateNormals,
            OptimizeMeshes = options.OptimizeMeshes,
        };

        using var ctx = new AssimpContext();
        var flags = PostProcessSteps.Triangulate
                    | PostProcessSteps.JoinIdenticalVertices
                    | PostProcessSteps.LimitBoneWeights
                    | PostProcessSteps.ImproveCacheLocality;
        if (options.GenerateNormals)
            flags |= PostProcessSteps.GenerateNormals;
        if (options.OptimizeMeshes)
            flags |= PostProcessSteps.OptimizeMeshes;

        var scene = ctx.ImportFile(path, flags);
        if (scene is null || !scene.HasMeshes)
            return false;

        if (!scene.Meshes.Any(m => m.HasBones))
            return false;

        var vertices = new List<Vector3>(65_536);
        var indices = new List<int>(131_072);
        var weights = new List<List<AssimpNamedBoneWeight>>(65_536);

        foreach (var mesh in scene.Meshes)
        {
            if (!mesh.HasBones)
                continue;

            var baseIndex = vertices.Count;
            foreach (var v in mesh.Vertices)
            {
                vertices.Add(new Vector3(v.X, v.Y, v.Z));
                weights.Add([]);
            }

            for (var fi = 0; fi < mesh.FaceCount; fi++)
            {
                var face = mesh.Faces[fi];
                if (face.IndexCount < 3)
                    continue;
                for (var t = 1; t < face.IndexCount - 1; t++)
                {
                    indices.Add(baseIndex + face.Indices[0]);
                    indices.Add(baseIndex + face.Indices[t]);
                    indices.Add(baseIndex + face.Indices[t + 1]);
                }
            }

            foreach (var bone in mesh.Bones)
            {
                var name = bone.Name ?? "";
                foreach (var vw in bone.VertexWeights)
                {
                    var vi = baseIndex + vw.VertexID;
                    if ((uint)vi >= (uint)weights.Count)
                        continue;
                    weights[vi].Add(new AssimpNamedBoneWeight(name, vw.Weight));
                }
            }
        }

        if (vertices.Count == 0 || indices.Count == 0)
            return false;

        var tri = new TriangleMesh(vertices, indices);
        tri = ApplyFraming(tri, options, weights);

        result = new AssimpNamedSkinImport
        {
            Mesh = tri,
            VertexWeights = weights.Select(static w => w.ToArray()).ToArray(),
        };
        return true;
    }

    private static TriangleMesh ApplyFraming(
        TriangleMesh mesh,
        MeshImportOptions options,
        List<List<AssimpNamedBoneWeight>> weights)
    {
        if (options.TargetLengthMeters is null
            && !options.CenterAtOrigin
            && !options.LongestAxisToPositiveZ)
            return mesh;

        // Framing transforms positions only; weight lists stay vertex-aligned.
        var editable = EditableMesh.FromTriangleMesh(mesh);
        var (min, max) = Bounds(editable);
        var size = max - min;
        var longest = MathF.Max(size.X, MathF.Max(size.Y, size.Z));
        if (longest < 1e-6f)
            return mesh;

        var center = (min + max) * 0.5f;
        var xf = NumericsMatrix.Identity;
        if (options.CenterAtOrigin || options.TargetLengthMeters is not null || options.LongestAxisToPositiveZ)
            xf = NumericsMatrix.CreateTranslation(-center);
        if (options.TargetLengthMeters is > 0f)
            xf *= NumericsMatrix.CreateScale(options.TargetLengthMeters.Value / longest);
        if (options.LongestAxisToPositiveZ)
        {
            if (size.X >= size.Y && size.X >= size.Z)
                xf *= NumericsMatrix.CreateRotationY(MathF.PI * 0.5f);
            else if (size.Y >= size.X && size.Y >= size.Z)
                xf *= NumericsMatrix.CreateRotationX(-MathF.PI * 0.5f);
        }

        editable.Transform(xf);
        if (options.CenterAtOrigin)
        {
            (min, max) = Bounds(editable);
            center = (min + max) * 0.5f;
            editable.Transform(NumericsMatrix.CreateTranslation(-center));
        }

        _ = weights;
        return editable.ToTriangleMesh();
    }

    private static (Vector3 Min, Vector3 Max) Bounds(EditableMesh mesh)
    {
        var min = new Vector3(float.MaxValue);
        var max = new Vector3(float.MinValue);
        for (var i = 0; i < mesh.VertexCount; i++)
        {
            var v = mesh.Vertices[i];
            min = Vector3.Min(min, v);
            max = Vector3.Max(max, v);
        }

        return (min, max);
    }
}

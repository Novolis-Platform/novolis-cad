using System.Numerics;
using Assimp;
using Novolis.Math.Geometry;
using NumericsMatrix = System.Numerics.Matrix4x4;

namespace Novolis.Modeling.Import;

/// <summary>
/// Loads triangle meshes from FBX, OBJ, glTF, and other Assimp formats (native runtime required).
/// Port of Frank.GameEngine.Assets.SceneMeshImporter + SceneLab framing options.
/// </summary>
public static class AssimpMeshImporter
{
    /// <summary>Common extensions Assimp can typically open (not exhaustive).</summary>
    public static readonly string[] CommonExtensions =
    [
        ".fbx", ".obj", ".gltf", ".glb", ".dae", ".3ds", ".blend", ".stl", ".ply",
    ];

    public static TriangleMesh ImportFile(string path, MeshImportOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!File.Exists(path))
            throw new FileNotFoundException("Mesh file not found.", path);

        using var ctx = new AssimpContext();
        var scene = ctx.ImportFile(path, BuildFlags(options));
        return MergeAndFrame(scene, options);
    }

    /// <summary>
    /// Import from a stream; <paramref name="formatHintExtension"/> must include the dot (e.g. <c>.fbx</c>).
    /// </summary>
    public static TriangleMesh ImportFromStream(Stream stream, string formatHintExtension, MeshImportOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentException.ThrowIfNullOrWhiteSpace(formatHintExtension);
        var ext = formatHintExtension.StartsWith('.') ? formatHintExtension : "." + formatHintExtension;

        using var ctx = new AssimpContext();
        var scene = ctx.ImportFileFromStream(stream, BuildFlags(options), ext);
        return MergeAndFrame(scene, options);
    }

    public static EditableMesh ImportEditable(string path, MeshImportOptions? options = null) =>
        EditableMesh.FromTriangleMesh(ImportFile(path, options));

    public static EditableMesh ImportEditableFromStream(Stream stream, string formatHintExtension, MeshImportOptions? options = null) =>
        EditableMesh.FromTriangleMesh(ImportFromStream(stream, formatHintExtension, options));

    public static bool IsSupportedExtension(string? pathOrExtension)
    {
        if (string.IsNullOrWhiteSpace(pathOrExtension))
            return false;
        var ext = pathOrExtension.Contains('.')
            ? Path.GetExtension(pathOrExtension)
            : (pathOrExtension.StartsWith('.') ? pathOrExtension : "." + pathOrExtension);
        return CommonExtensions.Any(e => e.Equals(ext, StringComparison.OrdinalIgnoreCase));
    }

    private static PostProcessSteps BuildFlags(MeshImportOptions? options)
    {
        options ??= new MeshImportOptions();
        var flags = PostProcessSteps.Triangulate
                    | PostProcessSteps.JoinIdenticalVertices
                    | PostProcessSteps.ImproveCacheLocality;
        if (options.PreTransformVertices)
            flags |= PostProcessSteps.PreTransformVertices;
        if (options.GenerateNormals)
            flags |= PostProcessSteps.GenerateNormals;
        if (options.OptimizeMeshes)
            flags |= PostProcessSteps.OptimizeMeshes;
        return flags;
    }

    private static TriangleMesh MergeAndFrame(Scene? scene, MeshImportOptions? options)
    {
        if (scene is null || !scene.HasMeshes)
            throw new InvalidOperationException("Assimp loaded no meshes.");

        var vertices = new List<Vector3>(capacity: 65_536);
        var indices = new List<int>(capacity: 131_072);

        foreach (var mesh in scene.Meshes)
        {
            var baseIndex = vertices.Count;
            foreach (var v in mesh.Vertices)
                vertices.Add(new Vector3(v.X, v.Y, v.Z));

            for (var fi = 0; fi < mesh.FaceCount; fi++)
            {
                var face = mesh.Faces[fi];
                if (face.IndexCount < 3)
                    continue;
                // Fan for non-tri (should already be tris after Triangulate)
                for (var t = 1; t < face.IndexCount - 1; t++)
                {
                    indices.Add(baseIndex + face.Indices[0]);
                    indices.Add(baseIndex + face.Indices[t]);
                    indices.Add(baseIndex + face.Indices[t + 1]);
                }
            }
        }

        if (vertices.Count == 0 || indices.Count == 0)
            throw new InvalidOperationException("Assimp scene contained no triangle data.");

        var meshOut = new TriangleMesh(vertices, indices);
        return ApplyFraming(meshOut, options ?? new MeshImportOptions());
    }

    private static TriangleMesh ApplyFraming(TriangleMesh mesh, MeshImportOptions options)
    {
        if (options.TargetLengthMeters is null
            && !options.CenterAtOrigin
            && !options.LongestAxisToPositiveZ)
            return mesh;

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

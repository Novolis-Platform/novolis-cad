using Novolis.Modeling.Import;

namespace Novolis.Cad.Unit;

public sealed class AssimpSkinnedMeshImporterTests
{
    private static bool AssimpNativeAvailable => OperatingSystem.IsWindows();

    [Test]
    public async Task TryImport_null_or_whitespace_path_throws()
    {
        await Assert.That(() => AssimpSkinnedMeshImporter.TryImport(" ", out _))
            .Throws<ArgumentException>();
        await Assert.That(() => AssimpSkinnedMeshImporter.TryImport("", out _))
            .Throws<ArgumentException>();
    }

    [Test]
    public async Task TryImport_missing_file_returns_false()
    {
        var ok = AssimpSkinnedMeshImporter.TryImport(
            Path.Combine(Path.GetTempPath(), $"no-such-{Guid.NewGuid():N}.fbx"),
            out var result);
        await Assert.That(ok).IsFalse();
        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task TryImport_unskinned_obj_returns_false()
    {
        if (!AssimpNativeAvailable)
        {
            // Still exercise ArgumentException / missing-file paths above on non-Windows.
            await Assert.That(AssimpMeshImporter.IsSupportedExtension(".fbx")).IsTrue();
            return;
        }

        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "cube.obj");
        await Assert.That(File.Exists(path)).IsTrue();

        var ok = AssimpSkinnedMeshImporter.TryImport(path, out var result, new MeshImportOptions
        {
            GenerateNormals = true,
            OptimizeMeshes = true,
            CenterAtOrigin = true,
        });
        await Assert.That(ok).IsFalse();
        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task AssimpNamedSkinImport_HasSkinning_false_when_empty_weights()
    {
        var import = new AssimpNamedSkinImport
        {
            Mesh = new Novolis.Math.Geometry.TriangleMesh(
                [new System.Numerics.Vector3(0, 0, 0), new System.Numerics.Vector3(1, 0, 0), new System.Numerics.Vector3(0, 1, 0)],
                [0, 1, 2]),
            VertexWeights =
            [
                Array.Empty<AssimpNamedBoneWeight>(),
                Array.Empty<AssimpNamedBoneWeight>(),
                Array.Empty<AssimpNamedBoneWeight>(),
            ],
        };
        await Assert.That(import.HasSkinning).IsFalse();

        var skinned = new AssimpNamedSkinImport
        {
            Mesh = import.Mesh,
            VertexWeights =
            [
                [new AssimpNamedBoneWeight("Hip", 1f)],
                Array.Empty<AssimpNamedBoneWeight>(),
                Array.Empty<AssimpNamedBoneWeight>(),
            ],
        };
        await Assert.That(skinned.HasSkinning).IsTrue();
    }
}

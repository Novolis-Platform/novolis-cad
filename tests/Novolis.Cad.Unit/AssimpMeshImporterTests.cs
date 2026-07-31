using Novolis.Modeling.Import;

namespace Novolis.Cad.Unit;

public sealed class AssimpMeshImporterTests
{
    [Test]
    public async Task ImportObj_Cube_HasTriangles()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "cube.obj");
        await Assert.That(File.Exists(path)).IsTrue();

        var mesh = AssimpMeshImporter.ImportFile(path);
        await Assert.That(mesh.VertexCount).IsGreaterThanOrEqualTo(8);
        await Assert.That(mesh.Indices.Length).IsGreaterThanOrEqualTo(36);
    }

    [Test]
    public async Task ImportEditable_NormalizeLength_MatchesTarget()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "cube.obj");
        var editable = AssimpMeshImporter.ImportEditable(path, new MeshImportOptions
        {
            TargetLengthMeters = 10f,
            CenterAtOrigin = true,
            LongestAxisToPositiveZ = true,
        });

        float minX = float.MaxValue, maxX = float.MinValue;
        float minY = float.MaxValue, maxY = float.MinValue;
        float minZ = float.MaxValue, maxZ = float.MinValue;
        for (var i = 0; i < editable.VertexCount; i++)
        {
            var v = editable.Vertices[i];
            minX = MathF.Min(minX, v.X); maxX = MathF.Max(maxX, v.X);
            minY = MathF.Min(minY, v.Y); maxY = MathF.Max(maxY, v.Y);
            minZ = MathF.Min(minZ, v.Z); maxZ = MathF.Max(maxZ, v.Z);
        }

        var longest = MathF.Max(maxX - minX, MathF.Max(maxY - minY, maxZ - minZ));
        await Assert.That(longest).IsEqualTo(10f).Within(0.05f);
    }

    [Test]
    public async Task IsSupportedExtension_Fbx_True()
    {
        await Assert.That(AssimpMeshImporter.IsSupportedExtension(".fbx")).IsTrue();
        await Assert.That(AssimpMeshImporter.IsSupportedExtension("ship.FBX")).IsTrue();
        await Assert.That(AssimpMeshImporter.IsSupportedExtension(".xyz")).IsFalse();
    }
}

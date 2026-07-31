using System.Numerics;
using Novolis.Cad.Primitives;
using Novolis.Cad.SceneBridge;
using Novolis.Cad.SceneBridge.Tessellation;
using Novolis.Modeling.Scene;

namespace Novolis.Cad.Unit;

public sealed class CadSceneBridgeTests
{
    [Test]
    public async Task SolidTessellator_Box_HasEightVertices()
    {
        var mesh = CadSolidTessellator.Box(new Vector3(1, 0.5f, 1));
        await Assert.That(mesh.VertexCount).IsEqualTo(8);
        await Assert.That(mesh.TriangleCount).IsEqualTo(12);
    }

    [Test]
    public async Task SolidTessellator_SphereAndCylinder_ProduceMeshes()
    {
        var sphere = CadSolidTessellator.Sphere(1f, 12, 16);
        var cyl = CadSolidTessellator.Cylinder(0.5f, 2f, 24);
        await Assert.That(sphere.VertexCount).IsGreaterThan(10);
        await Assert.That(cyl.VertexCount).IsGreaterThan(10);
    }

    [Test]
    public async Task WallTessellator_AbSegment_ProducesSlab()
    {
        var wall = new CadEntity
        {
            Kind = "wall",
            A = [0, 0, 0],
            B = [4, 0, 0],
            Height = 2.4f,
            Thickness = 0.2f,
        };
        var mesh = CadWallTessellator.TryTessellate(wall);
        await Assert.That(mesh).IsNotNull();
        await Assert.That(mesh!.VertexCount).IsEqualTo(8);
    }

    [Test]
    public async Task ToSceneDocument_BoxWallMaterial_BuildsExpectedNodes()
    {
        var cad = new CadDocument { Name = "BridgeFixture" };
        cad.Entities.Add(new CadEntity
        {
            Kind = "box",
            Name = "Mass",
            Center = [0, 0.5f, 0],
            HalfExtents = [1, 0.5f, 1],
            Material = "Concrete",
        });
        cad.Entities.Add(new CadEntity
        {
            Kind = "wall",
            Name = "Bulkhead",
            A = [0, 0, 0],
            B = [3, 0, 0],
            Height = 2.4f,
            Thickness = 0.15f,
            Sides = new CadWallSides { A = new CadWallSide { ShapeId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee") } },
        });

        var scene = CadSceneBridge.ToSceneDocument(cad, new CadSceneBridgeOptions { EnsureStudioLights = true });
        await Assert.That(scene.Name).IsEqualTo("BridgeFixture");
        await Assert.That(scene.Nodes.OfType<MeshNode>().Count()).IsEqualTo(2);
        await Assert.That(scene.Nodes.OfType<MaterialNode>().Count()).IsGreaterThanOrEqualTo(1);
        await Assert.That(scene.Nodes.OfType<LightNode>().Count()).IsEqualTo(3);

        var tmp = Path.Combine(Path.GetTempPath(), $"cad-bridge-{Guid.NewGuid():N}.nov3djson");
        try
        {
            CadSceneBridge.SaveNov3dJson(cad, tmp);
            var loaded = SceneSerializer.Load(tmp);
            await Assert.That(loaded.Nodes.OfType<MeshNode>().Count()).IsEqualTo(2);
        }
        finally
        {
            if (File.Exists(tmp))
                File.Delete(tmp);
        }
    }
}

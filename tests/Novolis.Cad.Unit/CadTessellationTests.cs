using System.Numerics;
using Novolis.Cad.Primitives;
using Novolis.Cad.SceneBridge;
using Novolis.Cad.SceneBridge.Tessellation;
using Novolis.Modeling.Scene;

namespace Novolis.Cad.Unit;

public sealed class CadTessellationTests
{
    [Test]
    public async Task SpaceTessellator_FloorAndCeiling()
    {
        var space = new CadEntity
        {
            Kind = "space",
            Deck = 1,
            Height = 2.4f,
            Points = [CadVec.Xz(0, 0), CadVec.Xz(4, 0), CadVec.Xz(4, 3), CadVec.Xz(0, 3)],
        };

        var floor = CadSpaceTessellator.TryTessellate(space);
        await Assert.That(floor).IsNotNull();
        await Assert.That(floor!.VertexCount).IsEqualTo(8);

        var both = CadSpaceTessellator.TryTessellate(space, includeCeiling: true);
        await Assert.That(both!.VertexCount).IsEqualTo(16);
    }

    [Test]
    public async Task SpaceTessellator_InvalidEntity_ReturnsNull()
    {
        await Assert.That(CadSpaceTessellator.TryTessellate(new CadEntity { Kind = "wall" })).IsNull();
        await Assert.That(CadSpaceTessellator.TryTessellate(new CadEntity { Kind = "space", Points = [[0, 0, 0]] })).IsNull();
    }

    [Test]
    public async Task EntityTessellator_DispatchesByKind()
    {
        var wall = new CadEntity { Kind = "wall", A = [0, 0, 0], B = [2, 0, 0], Height = 2, Thickness = 0.2f };
        await Assert.That(CadEntityTessellator.TryTessellate(wall)).IsNotNull();

        var sphere = new CadEntity { Kind = "sphere", Center = [0, 1, 0], Radius = 1 };
        await Assert.That(CadEntityTessellator.TryTessellate(sphere)!.VertexCount).IsGreaterThan(10);

        var stored = new CadEntity
        {
            Kind = "mesh",
            MeshVertices = [CadVec.Xyz(-1, 0, -1), CadVec.Xyz(1, 0, -1), CadVec.Xyz(0, 1, 0)],
            MeshIndices = [0, 1, 2],
        };
        await Assert.That(CadEntityTessellator.TryTessellate(stored)!.TriangleCount).IsEqualTo(1);
    }

    [Test]
    public async Task SolidTessellator_StoreRoundTrip()
    {
        var mesh = CadSolidTessellator.Box(new Vector3(1, 0.5f, 1));
        var entity = new CadEntity { Kind = "mesh" };
        CadSolidTessellator.StoreOnEntity(entity, mesh);
        var restored = CadSolidTessellator.FromStored(entity);
        await Assert.That(restored.VertexCount).IsEqualTo(mesh.VertexCount);
        await Assert.That(restored.TriangleCount).IsEqualTo(mesh.TriangleCount);
    }

    [Test]
    public async Task SceneBridge_AddsCameraLightAndMaterialEntities()
    {
        var cad = new CadDocument { Name = "Mixed" };
        cad.Entities.Add(new CadEntity
        {
            Kind = "camera",
            Name = "Iso",
            Center = [5, 4, 6],
            B = [0, 0, 0],
        });
        cad.Entities.Add(new CadEntity
        {
            Kind = "light",
            Name = "Sun",
            LightType = "infinite",
            Intensity = 0.8f,
            Center = [0, 5, 0],
        });
        cad.Entities.Add(new CadEntity
        {
            Kind = "material",
            Name = "Steel",
            Color = [0.7f, 0.7f, 0.75f],
        });
        cad.Entities.Add(new CadEntity
        {
            Kind = "space",
            Name = "Cabin",
            Points = [CadVec.Xz(0, 0), CadVec.Xz(2, 0), CadVec.Xz(2, 2)],
            Height = 2.2f,
        });

        var scene = CadSceneBridge.ToSceneDocument(cad, new CadSceneBridgeOptions
        {
            EnsureStudioLights = false,
            IncludeSpaceCeilings = true,
        });

        await Assert.That(scene.Nodes.OfType<CameraNode>().Count()).IsEqualTo(1);
        await Assert.That(scene.ActiveCameraId).IsNotNull();
        await Assert.That(scene.Nodes.OfType<LightNode>().Count()).IsEqualTo(1);
        await Assert.That(scene.Nodes.OfType<MaterialNode>().Any(m => m.Name == "Steel")).IsTrue();
        await Assert.That(scene.Nodes.OfType<MeshNode>().Any(m => m.Name == "Cabin")).IsTrue();
    }
}

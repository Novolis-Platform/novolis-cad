using System.Numerics;
using Novolis.Cad.Primitives;
using Novolis.Cad.SceneBridge;
using Novolis.Cad.SceneBridge.Tessellation;
using Novolis._3D;

namespace Novolis.Cad.Unit;

public sealed class PublicApiCoverageTests
{
    [Test]
    public async Task WorkspaceMapping_CoversUnknownAliasesAndDisplayLabels()
    {
        await Assert.That(CadWorkspaceMapping.Parse("nonsense")).IsEqualTo(CadWorkspace.Cad);
        await Assert.That(CadWorkspaceMapping.Parse("MODELING")).IsEqualTo(CadWorkspace.Modeling);
        await Assert.That(CadWorkspaceMapping.FromViewMode(CadViewMode.Draft)).IsEqualTo(CadWorkspace.Cad);
        await Assert.That(CadWorkspaceMapping.ToViewMode(CadWorkspace.Modeling)).IsEqualTo(CadViewMode.Model);
        await Assert.That(CadWorkspaceMapping.ToViewMode(CadWorkspace.Preview)).IsEqualTo(CadViewMode.Model);
        await Assert.That(CadWorkspaceMapping.ToStorage(CadWorkspace.Preview)).IsEqualTo("preview");
        await Assert.That(CadWorkspaceMapping.ToStorage(CadWorkspace.Cad)).IsEqualTo("cad");
        await Assert.That(CadWorkspaceMapping.ToDisplay(CadWorkspace.Modeling)).IsEqualTo("Modeling");
        await Assert.That(CadWorkspaceMapping.ToDisplay(CadWorkspace.Preview)).IsEqualTo("Preview");
        await Assert.That(CadWorkspaceMapping.ToDisplay(CadWorkspace.Cad)).IsEqualTo("CAD");
    }

    [Test]
    public async Task CadVec_EnumerateWorldPoints_RectAbAndMinMaxPaths()
    {
        var ab = CadVec.EnumerateWorldPoints(new CadEntity
        {
            Kind = "rect",
            A = [0, 0, 0],
            B = [2, 0, 2],
        }).ToList();
        await Assert.That(ab.Count).IsEqualTo(2);

        var minMax = CadVec.EnumerateWorldPoints(new CadEntity
        {
            Kind = "rect",
            Min = [-1, 0, -1],
            Max = [1, 0, 1],
        }).ToList();
        await Assert.That(minMax.Count).IsEqualTo(2);

        var half2 = CadVec.EnumerateWorldPoints(new CadEntity
        {
            Kind = "rect",
            Center = [0, 0, 0],
            HalfExtents = [1.5f, 2f],
        }).ToList();
        await Assert.That(half2.Count).IsEqualTo(2);

        var openingPts = CadVec.EnumerateWorldPoints(new CadEntity
        {
            Kind = "opening",
            Points = [CadVec.Xyz(0, 0, 0), CadVec.Xyz(1, 0, 0)],
        }).ToList();
        await Assert.That(openingPts.Count).IsEqualTo(2);
    }

    [Test]
    public async Task CadEntity_Summary_OpeningDefaultAndShipBox()
    {
        var opening = new CadEntity { Kind = "opening", Name = "Hatch" };
        await Assert.That(opening.Summary).Contains("opening");
        await Assert.That(opening.Summary).Contains("door");

        var shipBox = new CadEntity
        {
            Kind = "box",
            Name = "Crate",
            Points = [CadVec.Xyz(0, 0, 0), CadVec.Xyz(0, 0, 0)],
            Thickness = 1f,
            Height = 1f,
        };
        await Assert.That(shipBox.Summary).Contains("box");
    }

    [Test]
    public async Task WallTessellator_CoversFallbackBranches()
    {
        await Assert.That(CadWallTessellator.TryTessellate(new CadEntity { Kind = "box" })).IsNull();

        var degenerate = CadWallTessellator.TryTessellate(new CadEntity
        {
            Kind = "wall",
            A = [0, 0, 0],
            B = [0, 0, 0],
        });
        await Assert.That(degenerate).IsNull();

        var poly = CadWallTessellator.TryTessellate(new CadEntity
        {
            Kind = "wall",
            Points = [CadVec.Xyz(0, 0, 0), CadVec.Xyz(2, 0, 0), CadVec.Xyz(2, 0, 2)],
            Height = 0,
            Thickness = 0,
            Deck = 1,
        });
        await Assert.That(poly).IsNotNull();
        await Assert.That(poly!.VertexCount).IsGreaterThan(8);

        // Vertical-ish direction (along Y projected to zero length on XZ → right fallback uses UnitX after normalize of along-X? use along +Y world with A/B same XZ)
        var vertical = CadWallTessellator.TryTessellate(new CadEntity
        {
            Kind = "wall",
            A = [0, 0, 0],
            B = [0, 5, 0],
            Height = 2f,
            Thickness = 0.2f,
        });
        // dir.Y zeroed → length ~0 → skipped → null
        await Assert.That(vertical).IsNull();
    }

    [Test]
    public async Task SolidTessellator_CylinderAndUnknownKinds()
    {
        var cyl = CadSolidTessellator.TryTessellate(new CadEntity
        {
            Kind = "cylinder",
            Center = [1, 0, 1],
            Radius = 0.5f,
            Height = 2f,
        });
        await Assert.That(cyl).IsNotNull();
        await Assert.That(cyl!.VertexCount).IsGreaterThan(10);

        await Assert.That(CadSolidTessellator.TryTessellate(new CadEntity { Kind = "cone" })).IsNull();
        await Assert.That(CadEntityTessellator.TryTessellate(new CadEntity
        {
            Kind = "space",
            Points = [CadVec.Xz(0, 0), CadVec.Xz(2, 0), CadVec.Xz(2, 2)],
            Height = 2.4f,
        })).IsNotNull();
    }

    [Test]
    public async Task SceneBridge_CamerasLightsMaterialsAndSkips()
    {
        var cad = new CadDocument { Name = " " };
        cad.Entities.Add(new CadEntity
        {
            Kind = "camera",
            Name = "MainCam",
            Center = [5, 3, 5],
            B = [0, 0.5f, 0],
        });
        cad.Entities.Add(new CadEntity
        {
            Kind = "light",
            Name = "Spot",
            LightType = "spot",
            Center = [2, 4, 2],
            Intensity = 2f,
        });
        cad.Entities.Add(new CadEntity
        {
            Kind = "light",
            LightType = "area",
            Intensity = 0,
        });
        cad.Entities.Add(new CadEntity
        {
            Kind = "light",
            LightType = "weird",
        });
        cad.Entities.Add(new CadEntity
        {
            Kind = "material",
            Name = "Paint",
            Color = [0.2f, 0.4f, 0.6f],
        });
        cad.Entities.Add(new CadEntity
        {
            Kind = "material",
            Name = "Paint", // duplicate material key reuse
        });
        cad.Entities.Add(new CadEntity { Kind = "wall" }); // no A/B → tessellate null → skip
        cad.Entities.Add(new CadEntity
        {
            Kind = "box",
            Name = "Block",
            Center = [0, 0.5f, 0],
            HalfExtents = [1, 0.5f, 1],
            Material = "Concrete",
        });
        cad.Entities.Add(new CadEntity
        {
            Kind = "box",
            Center = [3, 0.5f, 0],
            HalfExtents = [0.5f, 0.5f, 0.5f],
            Material = "Concrete", // reuse material dictionary hit
        });
        cad.Entities.Add(new CadEntity
        {
            Kind = "wall",
            A = [0, 0, 0],
            B = [2, 0, 0],
            Height = 2.4f,
            Thickness = 0.15f,
            Sides = new CadWallSides(), // both sides null → early return
        });

        var scene = CadSceneBridge.ToSceneDocument(cad, new CadSceneBridgeOptions { EnsureStudioLights = false });
        await Assert.That(scene.Name).IsEqualTo("Cad Bridge");
        await Assert.That(scene.ActiveCameraId).IsNotNull();
        await Assert.That(scene.Nodes.OfType<CameraNode>().Count()).IsEqualTo(1);
        await Assert.That(scene.Nodes.OfType<LightNode>().Count()).IsEqualTo(3);
        await Assert.That(scene.Nodes.OfType<MeshNode>().Count()).IsEqualTo(3);
        await Assert.That(scene.Nodes.OfType<MaterialNode>().Any(m => m.Name == "Paint")).IsTrue();
    }

    [Test]
    public async Task SceneBridge_CameraFallbackPositions_AndDirectionalLight()
    {
        var cad = new CadDocument();
        cad.Entities.Add(new CadEntity
        {
            Kind = "camera",
            A = [1, 2, 3],
        });
        cad.Entities.Add(new CadEntity
        {
            Kind = "light",
            LightType = "directional",
        });
        cad.Entities.Add(new CadEntity
        {
            Kind = "camera",
        });

        var scene = CadSceneBridge.ToSceneDocument(cad);
        await Assert.That(scene.Nodes.OfType<CameraNode>().Count()).IsEqualTo(2);
        await Assert.That(scene.Nodes.OfType<LightNode>().Any(l => l.LightKind == LightKind.Infinite)).IsTrue();
        await Assert.That(scene.ActiveCameraId).IsNotNull();
    }
}

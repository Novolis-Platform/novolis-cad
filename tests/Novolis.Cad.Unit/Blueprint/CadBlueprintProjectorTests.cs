using Novolis.Cad.Blueprint;
using Novolis.Cad.Primitives;

namespace Novolis.Cad.Unit;

public sealed class CadBlueprintProjectorTests
{
    [Test]
    public async Task FromCadDocument_LiftsWallsSpacesOpenings()
    {
        var wallId = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");
        var doc = new CadDocument { Name = "Demo house" };
        doc.Layers.Add(new CadLayer { Id = Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb"), Name = "A-WALL" });
        doc.Entities.Add(new CadEntity
        {
            Id = wallId,
            Kind = "wall",
            Name = "W1",
            Deck = 0,
            A = [0, 0, 0],
            B = [4, 0, 0],
            Thickness = 0.15f,
            Height = 2.7f,
            LayerId = doc.Layers[0].Id,
        });
        doc.Entities.Add(new CadEntity
        {
            Kind = "space",
            Name = "Living",
            Deck = 0,
            Height = 2.7f,
            Footprint = [[0, 0, 0], [4, 0, 0], [4, 0, 3], [0, 0, 3]],
        });
        doc.Entities.Add(new CadEntity
        {
            Kind = "opening",
            Name = "Front door",
            OpeningType = "door",
            Deck = 0,
            Height = 2.1f,
            HostWallId = wallId,
            Footprint = [[1, 0, -0.1f], [2, 0, -0.1f], [2, 0, 0.1f], [1, 0, 0.1f]],
        });
        doc.Entities.Add(new CadEntity
        {
            Kind = "line",
            Name = "construction",
            A = [0, 0, 0],
            B = [1, 0, 1],
        });

        var bp = CadBlueprintProjector.FromCadDocument(doc, CadBlueprintContexts.House, "./demo.cadjson");

        await Assert.That(bp.Format).IsEqualTo("novolis.cad.blueprint");
        await Assert.That(bp.Context).IsEqualTo(CadBlueprintContexts.House);
        await Assert.That(bp.CadDocumentHref).IsEqualTo("./demo.cadjson");
        await Assert.That(bp.Walls).Count().IsEqualTo(1);
        await Assert.That(bp.Spaces).Count().IsEqualTo(1);
        await Assert.That(bp.Openings).Count().IsEqualTo(1);
        await Assert.That(bp.Openings[0].Kind).IsEqualTo("door");
        await Assert.That(bp.Openings[0].HostWallId).IsEqualTo(wallId);
        await Assert.That(bp.Sheets).Count().IsEqualTo(1);
        await Assert.That(bp.Sheets[0].Layers.Any(l => l.Path.StartsWith("interior", StringComparison.Ordinal))).IsTrue();
    }
}

using System.Text.Json;
using Novolis.Cad.Primitives;

namespace Novolis.Cad.Unit;

public sealed class CadPrimitivesTests
{
    [Test]
    public async Task WorkspaceMapping_ParsesAliases()
    {
        await Assert.That(CadWorkspaceMapping.Parse("draft")).IsEqualTo(CadWorkspace.Cad);
        await Assert.That(CadWorkspaceMapping.Parse("model")).IsEqualTo(CadWorkspace.Preview);
        await Assert.That(CadWorkspaceMapping.Parse("modeling")).IsEqualTo(CadWorkspace.Modeling);
        await Assert.That(CadWorkspaceMapping.Parse("preview")).IsEqualTo(CadWorkspace.Preview);
        await Assert.That(CadWorkspaceMapping.ToDisplay(CadWorkspace.Cad)).IsEqualTo("CAD");
    }

    [Test]
    public async Task Document_JsonRoundTrip_PreservesEntities()
    {
        var doc = new CadDocument { Name = "RoundTrip" };
        var id = Guid.NewGuid();
        doc.Entities.Add(new CadEntity
        {
            Id = id,
            Kind = "box",
            Center = [0f, 0.5f, 0f],
            HalfExtents = [1f, 0.5f, 1f],
            Operation = null,
        });
        doc.Entities.Add(new CadEntity
        {
            Kind = "boolean",
            Operation = "subtract",
            Mode = "solid",
            TargetId = id,
            CutterId = Guid.NewGuid(),
        });

        var json = JsonSerializer.Serialize(doc);
        var loaded = JsonSerializer.Deserialize<CadDocument>(json);
        await Assert.That(loaded).IsNotNull();
        await Assert.That(loaded!.Name).IsEqualTo("RoundTrip");
        await Assert.That(loaded.Entities.Count).IsEqualTo(2);
        await Assert.That(loaded.Entities[0].Id).IsEqualTo(id);
        await Assert.That(loaded.Entities[1].Kind).IsEqualTo("boolean");
    }

    [Test]
    public async Task CadVec_DeckFromElevation()
    {
        await Assert.That(CadVec.DeckFromElevation(0f)).IsEqualTo(0);
        await Assert.That(CadVec.DeckFromElevation(CadVec.DeckHeightMeters)).IsEqualTo(1);
        await Assert.That(CadVec.DeckFromElevation(-CadVec.DeckHeightMeters)).IsEqualTo(-1);
    }
}

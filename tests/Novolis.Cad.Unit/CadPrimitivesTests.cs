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

    [Test]
    public async Task CadEntity_JsonRoundTrip_PreservesStyleAndTransform()
    {
        var doc = new CadDocument { Name = "Styled" };
        doc.Entities.Add(new CadEntity
        {
            Kind = "line",
            Style = new CadStyle
            {
                Linetype = "Dashed",
                LineWeightMm = 0.35f,
                Color = [1f, 0f, 0f],
                ColorIndex = 1,
            },
            Transform = new CadTransform
            {
                Center = [1, 2, 3],
                RotationY = 45f,
                Scale = [2, 2, 2],
            },
            BaseTransform = new CadTransform { Center = [0, 0, 0] },
        });

        var json = JsonSerializer.Serialize(doc);
        var loaded = JsonSerializer.Deserialize<CadDocument>(json);
        await Assert.That(loaded!.Entities[0].Style!.Linetype).IsEqualTo("Dashed");
        await Assert.That(loaded.Entities[0].Transform!.RotationY).IsEqualTo(45f);
        await Assert.That(loaded.Entities[0].BaseTransform!.Center![0]).IsEqualTo(0f);
    }

    [Test]
    public async Task CadDocument_JsonRoundTrip_PreservesHooksShapesAndSwing()
    {
        var hookId = Guid.NewGuid();
        var doc = new CadDocument
        {
            Shapes = [new CadShapeRef { Name = "ProfileA" }],
            Entities =
            [
                new CadEntity
                {
                    Kind = "opening",
                    Hooks =
                    [
                        new CadHook
                        {
                            Id = hookId,
                            Tag = "hinge",
                            Position = [0, 1, 0],
                            Normal = [0, 0, 1],
                        },
                    ],
                    Swing = new CadOpeningSwing
                    {
                        StartAngle = 0,
                        EndAngle = 90,
                        Direction = [0, 0, 1],
                    },
                },
            ],
        };
        var loaded = JsonSerializer.Deserialize<CadDocument>(JsonSerializer.Serialize(doc));
        await Assert.That(loaded!.Shapes![0].Name).IsEqualTo("ProfileA");
        await Assert.That(loaded.Entities[0].Hooks![0].Tag).IsEqualTo("hinge");
        await Assert.That(loaded.Entities[0].Swing!.EndAngle).IsEqualTo(90);
    }
}

using Novolis.Cad.Blueprint;
using Novolis.Cad.Primitives;

namespace Novolis.Cad.Unit;

public sealed class CadBlueprintExportTests
{
    [Test]
    public async Task DemoCottage_ExportsFullDraftingSheet()
    {
        using var tmp = new TempDir();
        var result = CadBlueprintDemoCottage.Export(tmp.Path);
        await Assert.That(result.Ok).IsTrue();
        await Assert.That(result.Blueprint!.Spaces.Count).IsGreaterThanOrEqualTo(4);
        await Assert.That(result.Blueprint.Walls.Count).IsGreaterThanOrEqualTo(6);
        await Assert.That(result.Blueprint.Openings.Count).IsGreaterThanOrEqualTo(2);
        var html = await File.ReadAllTextAsync(
            result.WrittenPaths.First(p => p.EndsWith(".html", StringComparison.Ordinal)));
        await Assert.That(html).Contains("NBP-HOUSE-001");
        await Assert.That(html).Contains("Living");
        // Sheet-first: SVG appears before any optional layer UI.
        var svgAt = html.IndexOf("<svg", StringComparison.Ordinal);
        await Assert.That(svgAt).IsGreaterThan(0);
        await Assert.That(html.IndexOf("nbp-stage", StringComparison.Ordinal)).IsLessThan(
            html.Contains("nbp-ui", StringComparison.Ordinal)
                ? html.IndexOf("nbp-ui", StringComparison.Ordinal)
                : int.MaxValue);
    }

    [Test]
    public async Task House_ProjectValidateRoundTripHtml()
    {
        var doc = MakeHouse();
        using var tmp = new TempDir();

        var result = CadBlueprintExporter.Export(doc, new CadBlueprintExportOptions
        {
            OutputDirectory = tmp.Path,
            FileStem = "demo-house",
            ContextOverride = CadBlueprintContexts.House,
            CadDocumentHref = "./demo.cadjson",
        });

        await Assert.That(result.Ok).IsTrue();
        await Assert.That(result.Blueprint).IsNotNull();
        await Assert.That(result.WrittenPaths.Any(p => p.EndsWith(".cadblueprint.json", StringComparison.Ordinal))).IsTrue();
        await Assert.That(result.WrittenPaths.Any(p => p.EndsWith(".html", StringComparison.Ordinal))).IsTrue();

        var jsonPath = result.WrittenPaths.First(p => p.EndsWith(".cadblueprint.json", StringComparison.Ordinal));
        var loaded = CadBlueprintJson.Load(jsonPath);
        await Assert.That(loaded.Format).IsEqualTo(CadBlueprintJson.FormatId);
        await Assert.That(loaded.Context).IsEqualTo(CadBlueprintContexts.House);
        await Assert.That(loaded.Walls).Count().IsEqualTo(1);

        var diags = CadBlueprintValidator.Validate(loaded);
        await Assert.That(CadBlueprintValidator.HasErrors(diags)).IsFalse();

        var htmlPath = result.WrittenPaths.First(p => p.EndsWith(".html", StringComparison.Ordinal));
        var html = await File.ReadAllTextAsync(htmlPath);
        await Assert.That(html).Contains(CadBlueprintDom.FormatValue);
        await Assert.That(html).Contains("data-nbp-layer");
        await Assert.That(html).Contains("interior.spaces");
        await Assert.That(html).Contains("plan-north-up");
    }

    [Test]
    public async Task Invalid_DuplicateWallId_FailsWithoutWrite()
    {
        var bp = CadBlueprintProjector.FromCadDocument(MakeHouse(), CadBlueprintContexts.House);
        var dupId = bp.Walls[0].Id;
        bp.Walls.Add(new CadBlueprintWall
        {
            Id = dupId,
            Name = "Dup",
            LevelIndex = 0,
            A = [0, 0, 0],
            B = [1, 0, 0],
        });

        using var tmp = new TempDir();
        var result = CadBlueprintExporter.Export(bp, new CadBlueprintExportOptions
        {
            OutputDirectory = tmp.Path,
            FileStem = "bad",
            FailOnError = true,
        });

        await Assert.That(result.Ok).IsFalse();
        await Assert.That(result.Errors.Any(e => e.Code == "BP020")).IsTrue();
        await Assert.That(result.WrittenPaths).IsEmpty();
        await Assert.That(Directory.EnumerateFiles(tmp.Path, "*", SearchOption.AllDirectories).Any()).IsFalse();
    }

    [Test]
    public async Task ShipLookingDoc_UsesSternLeftOrientation()
    {
        var doc = new CadDocument { Name = "Tramp" };
        doc.Properties = new Dictionary<string, System.Text.Json.JsonElement>
        {
            ["loa"] = System.Text.Json.JsonSerializer.SerializeToElement(69f),
            ["oah"] = System.Text.Json.JsonSerializer.SerializeToElement(12.4f),
            ["hullLoft"] = System.Text.Json.JsonSerializer.SerializeToElement(new
            {
                stations = new[]
                {
                    new { zFromStem = 0f, halfBeam = 0.2f },
                    new { zFromStem = 34.5f, halfBeam = 10.4f },
                    new { zFromStem = 69f, halfBeam = 4f },
                },
            }),
        };
        doc.Entities.Add(new CadEntity
        {
            Kind = "space",
            Name = "BRIDGE",
            Deck = 1,
            Height = 2.5f,
            Footprint = [[-2, 0, 20], [2, 0, 20], [2, 0, 28], [-2, 0, 28]],
        });

        using var tmp = new TempDir();
        var result = CadBlueprintExporter.Export(doc, new CadBlueprintExportOptions
        {
            OutputDirectory = tmp.Path,
            FileStem = "tramp",
            ContextOverride = CadBlueprintContexts.SeagoingShip,
        });

        await Assert.That(result.Ok).IsTrue();
        await Assert.That(result.Blueprint!.Context).IsEqualTo(CadBlueprintContexts.SeagoingShip);
        await Assert.That(result.Blueprint.Sheets[0].Orientation).IsEqualTo("iso128-15-stern-left");
        await Assert.That(result.Blueprint.Shells).Count().IsEqualTo(1);
        await Assert.That(result.Blueprint.Shells[0].PlanRing!.Count).IsGreaterThanOrEqualTo(6);

        var html = await File.ReadAllTextAsync(result.WrittenPaths.First(p => p.EndsWith(".html", StringComparison.Ordinal)));
        await Assert.That(html).Contains("iso128-15-stern-left");
        await Assert.That(html).Contains("seagoing-ship");
        await Assert.That(html).Contains("NOT DNV PLAN APPROVAL");
    }

    private static CadDocument MakeHouse()
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
        return doc;
    }

    private sealed class TempDir : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "nbp-test-" + Guid.NewGuid().ToString("N"));

        public TempDir() => Directory.CreateDirectory(Path);

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Path))
                    Directory.Delete(Path, recursive: true);
            }
            catch
            {
                /* best-effort */
            }
        }
    }
}

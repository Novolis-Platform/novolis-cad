using Novolis.Cad.Primitives;

namespace Novolis.Cad.Blueprint;

/// <summary>
/// Complete miniature house CAD model for drafting / CadBlueprint export tests
/// (genericity: zero Ship types). ~10×8 m footprint, one storey.
/// </summary>
public static class CadBlueprintDemoCottage
{
    public const string DrawingNumber = "NBP-HOUSE-001";

    public static CadDocument CreateDocument()
    {
        var wallLayer = Guid.Parse("b1000000-0000-4000-8000-000000000001");
        var doc = new CadDocument
        {
            Name = "Demo cottage",
            LinearUnit = "meter",
            UnitScaleMeters = 1f,
            CreatedAt = DateTime.UtcNow.ToString("o"),
            ModifiedAt = DateTime.UtcNow.ToString("o"),
        };
        doc.Layers.Add(new CadLayer { Id = wallLayer, Name = "A-WALL" });

        // Exterior envelope as shell box (also lifted as shell).
        doc.Entities.Add(new CadEntity
        {
            Kind = "box",
            Name = "cottage-shell",
            Center = [5f, 1.35f, 4f],
            HalfExtents = [5f, 1.35f, 4f],
        });

        // Exterior walls (clockwise from SW): 10 m E-W × 8 m N-S in plan (X×Z).
        var wS = AddWall(doc, wallLayer, "W-S", [0, 0, 0], [10, 0, 0]);
        var wE = AddWall(doc, wallLayer, "W-E", [10, 0, 0], [10, 0, 8]);
        AddWall(doc, wallLayer, "W-N", [10, 0, 8], [0, 0, 8]);
        AddWall(doc, wallLayer, "W-W", [0, 0, 8], [0, 0, 0]);

        // Interior partitions
        AddWall(doc, wallLayer, "W-hall", [4, 0, 0], [4, 0, 8]); // N-S
        AddWall(doc, wallLayer, "W-kit", [4, 0, 4], [10, 0, 4]); // E-W kitchen/living
        var wBath = AddWall(doc, wallLayer, "W-bath", [0, 0, 3], [4, 0, 3]);

        // Spaces
        AddSpace(doc, "Entry", [[0, 0, 0], [4, 0, 0], [4, 0, 3], [0, 0, 3]]);
        AddSpace(doc, "Bath", [[0, 0, 3], [4, 0, 3], [4, 0, 8], [0, 0, 8]]);
        AddSpace(doc, "Living", [[4, 0, 0], [10, 0, 0], [10, 0, 4], [4, 0, 4]]);
        AddSpace(doc, "Kitchen", [[4, 0, 4], [10, 0, 4], [10, 0, 8], [4, 0, 8]]);

        // Openings (host walls match footprint; clear widths for door/window symbols)
        AddOpening(doc, "Front door", "door", wS, [[3.4f, 0, -0.1f], [4.6f, 0, -0.1f], [4.6f, 0, 0.1f], [3.4f, 0, 0.1f]], 1.2f, 2.1f);
        AddOpening(doc, "Bath door", "door", wBath, [[1.5f, 0, 2.9f], [2.5f, 0, 2.9f], [2.5f, 0, 3.1f], [1.5f, 0, 3.1f]], 0.9f, 2.1f);
        AddOpening(doc, "Living window", "window", wS, [[6, 0, -0.1f], [8, 0, -0.1f], [8, 0, 0.1f], [6, 0, 0.1f]], 2.0f, 1.2f);
        AddOpening(doc, "Kitchen window", "window", wE, [[10.1f, 0, 5], [10.1f, 0, 7], [9.9f, 0, 7], [9.9f, 0, 5]], 2.0f, 1.2f);

        return doc;
    }

    public static CadBlueprintExportResult Export(string outputDirectory, bool writeCadJson = true)
    {
        var doc = CreateDocument();
        Directory.CreateDirectory(outputDirectory);
        if (writeCadJson)
        {
            File.WriteAllText(
                Path.Combine(outputDirectory, "demo-cottage.cadjson"),
                System.Text.Json.JsonSerializer.Serialize(doc, CadJson.Options));
        }

        var (bp, lift) = CadBlueprintProjector.FromCadDocumentWithDiagnostics(
            doc, CadBlueprintContexts.House, "./demo-cottage.cadjson");
        bp.Name = "Demo cottage · ground floor plan";
        foreach (var sheet in bp.Sheets)
        {
            sheet.Id = DrawingNumber;
            sheet.Title = "Demo cottage — ground floor GA";
            sheet.Rev = "A";
            sheet.ShowUi = false;
            sheet.Orientation = "plan-north-up";
        }

        return CadBlueprintExporter.Export(bp, new CadBlueprintExportOptions
        {
            OutputDirectory = outputDirectory,
            FileStem = "demo-cottage",
            SmartHtmlSubfolder = "sheets",
            ContextOverride = CadBlueprintContexts.House,
            CadDocumentHref = "./demo-cottage.cadjson",
            FailOnError = true,
            FailOnWarning = false,
        }, lift);
    }

    private static Guid AddWall(CadDocument doc, Guid layerId, string name, float[] a, float[] b)
    {
        var id = Guid.NewGuid();
        doc.Entities.Add(new CadEntity
        {
            Id = id,
            Kind = "wall",
            Name = name,
            Deck = 0,
            A = a,
            B = b,
            Thickness = 0.15f,
            Height = 2.7f,
            LayerId = layerId,
        });
        return id;
    }

    private static void AddSpace(CadDocument doc, string name, float[][] footprint)
    {
        doc.Entities.Add(new CadEntity
        {
            Kind = "space",
            Name = name,
            Deck = 0,
            Height = 2.7f,
            Footprint = footprint.ToList(),
        });
    }

    private static void AddOpening(
        CadDocument doc,
        string name,
        string type,
        Guid? hostWall,
        float[][] footprint,
        float clearW,
        float clearH)
    {
        doc.Entities.Add(new CadEntity
        {
            Kind = "opening",
            Name = name,
            OpeningType = type,
            Deck = 0,
            Height = clearH,
            Thickness = clearW,
            HostWallId = hostWall,
            Footprint = footprint.ToList(),
        });
    }
}

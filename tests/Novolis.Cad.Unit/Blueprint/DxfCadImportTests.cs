using Novolis.Cad.Blueprint;
using Novolis.Cad.Dxf;

namespace Novolis.Cad.Unit;

public sealed class DxfCadImportTests
{
    [Test]
    public async Task BishopOverland_ImportsWallsAndExportsSheet()
    {
        var dxf = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Dxf", "bishop-overland-floorplan.dxf");
        await Assert.That(File.Exists(dxf)).IsTrue();

        var import = DxfCadImporter.ImportFile(dxf, new DxfImportOptions
        {
            DocumentName = "Bishop-Overland",
        });

        // Face-pair merge: ~170 WALL lines → ~one centerline per wall run.
        await Assert.That(import.WallCount).IsGreaterThanOrEqualTo(20);
        await Assert.That(import.WallCount).IsLessThanOrEqualTo(120);
        await Assert.That(import.OpeningCount).IsLessThanOrEqualTo(50);
        await Assert.That(import.OpeningCount).IsGreaterThanOrEqualTo(5);
        await Assert.That(import.Notes.Any(n => n.Contains("paired=", StringComparison.Ordinal))).IsTrue();
        await Assert.That(import.Notes.Any(n => n.Contains("Joined wall corners", StringComparison.Ordinal))).IsTrue();
        await Assert.That(import.Notes.Any(n => n.Contains("wall-face gaps", StringComparison.Ordinal))).IsTrue();

        var avgThick = import.Document.Entities
            .Where(e => e.Kind == "wall")
            .Select(e => e.Thickness)
            .DefaultIfEmpty(0)
            .Average();
        // Paired 4–6" faces should land near ~0.10 m, not the 0.15 m default on every face.
        await Assert.That(avgThick).IsLessThan(0.14f);
        await Assert.That(import.UnitScaleMeters).IsGreaterThan(0);
        // Mis-tagged mm headers should auto-promote to inches for this residential plan.
        await Assert.That(import.UnitScaleMeters).IsEqualTo(0.0254);

        await Assert.That(DxfCadImporter.IsRoomLabel("Kitchen")).IsTrue();
        await Assert.That(DxfCadImporter.IsRoomLabel("ENTRY")).IsTrue();
        await Assert.That(DxfCadImporter.IsRoomLabel("14'6 x 8'2")).IsFalse();
        await Assert.That(DxfCadImporter.IsRoomLabel("TOP ROOF")).IsFalse();
        await Assert.That(DxfCadImporter.IsRoomLabel("3068")).IsFalse();
        await Assert.That(DxfCadImporter.IsRoomLabel("HOUSE, ALSO ON BEARING WALLS")).IsFalse();

        using var tmp = new TempDir();
        var export = CadBlueprintExporter.Export(import.Document, new CadBlueprintExportOptions
        {
            OutputDirectory = tmp.Path,
            FileStem = "bishop",
            ContextOverride = CadBlueprintContexts.House,
            SmartHtmlSubfolder = "sheets",
            SvgSubfolder = "svg",
            PdfSubfolder = "pdf",
            FailOnError = true,
            FailOnWarning = false,
        });

        await Assert.That(export.Ok).IsTrue();
        await Assert.That(export.WrittenPaths.Any(p => p.EndsWith(".html", StringComparison.Ordinal))).IsTrue();
        await Assert.That(export.WrittenPaths.Any(p => p.EndsWith(".svg", StringComparison.Ordinal))).IsTrue();
        await Assert.That(export.WrittenPaths.Any(p => p.EndsWith(".pdf", StringComparison.Ordinal))).IsTrue();

        var html = await File.ReadAllTextAsync(export.WrittenPaths.First(p => p.EndsWith(".html", StringComparison.Ordinal)));
        await Assert.That(html).Contains("<svg");
        await Assert.That(html).Contains("data-nbp-iso=\"7200\"");
    }

    private sealed class TempDir : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "nbp-dxf-" + Guid.NewGuid().ToString("N"));

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

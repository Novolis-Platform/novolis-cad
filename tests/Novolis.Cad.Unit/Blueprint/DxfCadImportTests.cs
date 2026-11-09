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

        await Assert.That(import.WallCount).IsGreaterThanOrEqualTo(100);
        await Assert.That(import.UnitScaleMeters).IsGreaterThan(0);
        // Mis-tagged mm headers should auto-promote to inches for this residential plan.
        await Assert.That(import.UnitScaleMeters).IsEqualTo(0.0254);

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

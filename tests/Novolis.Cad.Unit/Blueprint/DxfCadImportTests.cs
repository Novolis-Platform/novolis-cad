using Novolis.Cad.Blueprint;
using Novolis.Cad.Dxf;

namespace Novolis.Cad.Unit;

public sealed class DxfCadImportTests
{
    [Test]
    public async Task InnersceneBungalow_ImportsWallsAndExportsSheet()
    {
        var dxf = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Dxf", "innerscene-three-bedroom-bungalow.dxf");
        await Assert.That(File.Exists(dxf)).IsTrue();

        var import = DxfCadImporter.ImportFile(dxf, new DxfImportOptions
        {
            DocumentName = "Innerscene bungalow",
        });

        // Closed poche rectangles merged across openings → fewer continuous hosts.
        await Assert.That(import.WallCount).IsGreaterThanOrEqualTo(8);
        await Assert.That(import.WallCount).IsLessThanOrEqualTo(30);
        await Assert.That(import.OpeningCount).IsGreaterThanOrEqualTo(4);
        await Assert.That(import.OpeningCount).IsLessThanOrEqualTo(40);
        await Assert.That(import.Notes.Any(n =>
            n.Contains("Merged", StringComparison.Ordinal)
            && n.Contains("across openings", StringComparison.Ordinal))).IsTrue();
        await Assert.That(import.UnitScaleMeters).IsEqualTo(0.001);

        var walls = import.Document.Entities
            .Where(e => e.Kind == "wall" && e.A is { Length: >= 3 } && e.B is { Length: >= 3 })
            .ToDictionary(e => e.Id);
        var openings = import.Document.Entities.Where(e => e.Kind == "opening").ToList();
        await Assert.That(openings.Count).IsGreaterThanOrEqualTo(4);

        var doors = openings.Count(o =>
            string.Equals(o.OpeningType, "door", StringComparison.OrdinalIgnoreCase));
        await Assert.That(doors).IsGreaterThanOrEqualTo(2);

        var avgThick = walls.Values.Select(e => e.Thickness).DefaultIfEmpty(0).Average();
        await Assert.That(avgThick).IsGreaterThan(0.12f);
        await Assert.That(avgThick).IsLessThan(0.40f);

        // Every opening mid must project onto its host within the host span.
        foreach (var op in openings)
        {
            await Assert.That(op.HostWallId is { }).IsTrue();
            await Assert.That(walls.ContainsKey(op.HostWallId!.Value)).IsTrue();
            var host = walls[op.HostWallId.Value];
            var ax = host.A![0];
            var az = host.A[2];
            var bx = host.B![0];
            var bz = host.B[2];
            var dx = bx - ax;
            var dz = bz - az;
            var len = (float)System.Math.Sqrt(dx * dx + dz * dz);
            await Assert.That(len).IsGreaterThan(0.2f);
            var tx = dx / len;
            var tz = dz / len;
            var midX = op.Footprint!.Average(p => p[0]);
            var midZ = op.Footprint!.Average(p => p.Length > 2 ? p[2] : p[1]);
            var t = (midX - ax) * tx + (midZ - az) * tz;
            var half = op.Thickness > 1e-3f ? op.Thickness * 0.5f : 0.3f;
            await Assert.That(t).IsGreaterThanOrEqualTo(-0.05f);
            await Assert.That(t).IsLessThanOrEqualTo(len + 0.05f);
            await Assert.That(t - half).IsLessThanOrEqualTo(len + 0.1f);
            await Assert.That(t + half).IsGreaterThanOrEqualTo(-0.1f);
        }

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
            FileStem = "innerscene",
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

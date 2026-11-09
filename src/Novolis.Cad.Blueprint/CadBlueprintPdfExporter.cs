using System.Text;
using SkiaSharp;
using Svg.Skia;

namespace Novolis.Cad.Blueprint;

/// <summary>
/// Writes A1 PDF sheets by rasterizing the typed Markup.Svg sheet through Svg.Skia + SkiaSharp.
/// </summary>
public static class CadBlueprintPdfExporter
{
    public static byte[] EmitSheet(CadBlueprint blueprint, CadBlueprintSheet sheet)
    {
        ArgumentNullException.ThrowIfNull(blueprint);
        ArgumentNullException.ThrowIfNull(sheet);

        var size = sheet.Sheet ?? CadBlueprintSheetSize.A1Landscape();
        var markup = CadBlueprintSheetSvg.ToMarkup(blueprint, sheet);
        var widthPt = MmToPoints(size.WidthMm);
        var heightPt = MmToPoints(size.HeightMm);

        using var svg = new SKSvg();
        using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(markup)))
        {
            if (svg.Load(stream) is null || svg.Picture is null)
            {
                throw new InvalidOperationException(
                    $"Svg.Skia failed to load sheet '{sheet.Id}' for PDF export.");
            }
        }

        using var memory = new MemoryStream();
        using (var pdf = SKDocument.CreatePdf(memory))
        {
            ArgumentNullException.ThrowIfNull(pdf);
            using var canvas = pdf.BeginPage(widthPt, heightPt);
            canvas.Clear(SKColors.White);
            var bounds = svg.Picture.CullRect;
            if (bounds.Width > 0.1f && bounds.Height > 0.1f)
            {
                var scale = System.Math.Min(widthPt / bounds.Width, heightPt / bounds.Height);
                canvas.Save();
                canvas.Scale(scale);
                canvas.DrawPicture(svg.Picture);
                canvas.Restore();
            }

            pdf.EndPage();
            pdf.Close();
        }

        return memory.ToArray();
    }

    public static void WriteSheet(CadBlueprint blueprint, CadBlueprintSheet sheet, string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        File.WriteAllBytes(path, EmitSheet(blueprint, sheet));
    }

    private static float MmToPoints(double mm) => (float)(mm * 72.0 / 25.4);
}

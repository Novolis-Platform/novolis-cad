using Novolis.Markup.Svg;

namespace Novolis.Cad.Blueprint;

/// <summary>Writes standalone typed SVG sheets from <see cref="CadBlueprintSheetSvg"/>.</summary>
public static class CadBlueprintSvgExporter
{
    public static SvgDocument EmitSheet(CadBlueprint blueprint, CadBlueprintSheet sheet) =>
        CadBlueprintSheetSvg.Compose(blueprint, sheet);

    public static string EmitMarkup(CadBlueprint blueprint, CadBlueprintSheet sheet) =>
        CadBlueprintSheetSvg.ToMarkup(blueprint, sheet);

    public static void WriteSheet(CadBlueprint blueprint, CadBlueprintSheet sheet, string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        using var writer = new StreamWriter(path);
        writer.WriteLine("""<?xml version="1.0" encoding="UTF-8"?>""");
        CadBlueprintSheetSvg.Compose(blueprint, sheet).WriteTo(writer);
    }
}

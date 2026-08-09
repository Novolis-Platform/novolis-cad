using System.Text.Json;

namespace Novolis.Cad.Blueprint;

/// <summary>
/// Smart sheet presentation attached to a <see cref="CadBlueprint"/>.
/// Geometry stays on the blueprint; the sheet projects it into HTML5/SVG with toggleable layers.
/// </summary>
public sealed class CadBlueprintSheet
{
    public string Id { get; set; } = "";

    public string Title { get; set; } = "";

    public string? Subtitle { get; set; }

    public string? Rev { get; set; }

    public string Units { get; set; } = "m";

    /// <summary>iso128-15-stern-left | plan-north-up | custom</summary>
    public string Orientation { get; set; } = "plan-north-up";

    public CadBlueprintSheetSize Sheet { get; set; } = CadBlueprintSheetSize.A1Landscape();

    public List<CadBlueprintFolder> Folders { get; set; } = [];

    public List<CadBlueprintLayer> Layers { get; set; } = [];

    public List<CadBlueprintView> Views { get; set; } = [];

    public List<CadBlueprintPreset> Presets { get; set; } = [];

    public bool ShowUi { get; set; } = true;

    public string? DefaultPresetId { get; set; }

    public Dictionary<string, JsonElement>? Properties { get; set; }
}

public sealed class CadBlueprintSheetSize
{
    public string Size { get; set; } = "A1";

    public double WidthMm { get; set; } = 841;

    public double HeightMm { get; set; } = 594;

    public bool Landscape { get; set; } = true;

    public double BorderLeftMm { get; set; } = 20;

    public double BorderMm { get; set; } = 10;

    public static CadBlueprintSheetSize A1Landscape() => new();
}

public sealed class CadBlueprintFolder
{
    public string Path { get; set; } = "";

    public string Label { get; set; } = "";

    public string? Description { get; set; }

    public bool DefaultExpanded { get; set; } = true;
}

public sealed class CadBlueprintLayer
{
    public string Id { get; set; } = "";

    public string Path { get; set; } = "";

    public string Label { get; set; } = "";

    /// <summary>chrome | outline | structure | space | opening | dimension | annotation | overlay | custom</summary>
    public string Kind { get; set; } = "custom";

    public Guid? CadLayerId { get; set; }

    public string? CadLayerName { get; set; }

    public bool DefaultVisible { get; set; } = true;

    public bool Plot { get; set; } = true;

    public bool Locked { get; set; }

    public string? Description { get; set; }
}

public sealed class CadBlueprintView
{
    public string Id { get; set; } = "";

    public string Label { get; set; } = "";

    public string? Scale { get; set; }

    /// <summary>profile | plan | section | detail | schedule | other</summary>
    public string Kind { get; set; } = "plan";

    public int? LevelIndex { get; set; }
}

public sealed class CadBlueprintPreset
{
    public string Id { get; set; } = "";

    public string Label { get; set; } = "";

    public List<string> VisibleLayerIds { get; set; } = [];

    public bool ForPlot { get; set; }
}

/// <summary>DOM contract constants for HTML5 smart-sheet emitters.</summary>
public static class CadBlueprintDom
{
    public const string FormatAttr = "data-nbp-format";
    public const string FormatValue = "novolis.cad.blueprint";
    public const string SchemaAttr = "data-nbp-schema";
    public const string ManifestElementId = "nbp-manifest";
    public const string SheetElementId = "nbp-sheet";
    public const string UiElementId = "nbp-ui";
    public const string LayerAttr = "data-nbp-layer";
    public const string PathAttr = "data-nbp-path";
    public const string KindAttr = "data-nbp-kind";
    public const string PlotAttr = "data-nbp-plot";
    public const string ViewAttr = "data-nbp-view";
    public const string HiddenClass = "nbp-hidden";
}

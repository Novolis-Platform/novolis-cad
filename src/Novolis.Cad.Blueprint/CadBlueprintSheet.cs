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

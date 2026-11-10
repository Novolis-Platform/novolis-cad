using System.Text.Json;

namespace Novolis.Cad.Blueprint;

public sealed class CadBlueprintView
{
    public string Id { get; set; } = "";

    public string Label { get; set; } = "";

    public string? Scale { get; set; }

    /// <summary>profile | plan | section | elevation | detail | schedule | other</summary>
    public string Kind { get; set; } = "plan";

    public int? LevelIndex { get; set; }

    /// <summary>DXF stroke packs (furniture, dims, section/elevation linework).</summary>
    public List<CadBlueprintStroke> Strokes { get; set; } = [];

    public List<CadBlueprintViewText> Texts { get; set; } = [];

    public Dictionary<string, JsonElement>? Properties { get; set; }
}

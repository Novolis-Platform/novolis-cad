using System.Text.Json;

namespace Novolis.Cad.Blueprint;

public sealed class CadBlueprintView
{
    public string Id { get; set; } = "";

    public string Label { get; set; } = "";

    public string? Scale { get; set; }

    /// <summary>profile | plan | section | detail | schedule | other</summary>
    public string Kind { get; set; } = "plan";

    public int? LevelIndex { get; set; }
}

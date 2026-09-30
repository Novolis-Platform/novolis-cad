using System.Text.Json;

namespace Novolis.Cad.Blueprint;

public sealed class CadBlueprintFolder
{
    public string Path { get; set; } = "";

    public string Label { get; set; } = "";

    public string? Description { get; set; }

    public bool DefaultExpanded { get; set; } = true;
}

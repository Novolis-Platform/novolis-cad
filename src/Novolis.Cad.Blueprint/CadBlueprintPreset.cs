using System.Text.Json;

namespace Novolis.Cad.Blueprint;

public sealed class CadBlueprintPreset
{
    public string Id { get; set; } = "";

    public string Label { get; set; } = "";

    public List<string> VisibleLayerIds { get; set; } = [];

    public bool ForPlot { get; set; }
}

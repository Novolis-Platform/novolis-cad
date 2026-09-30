using System.Text.Json;

namespace Novolis.Cad.Blueprint;

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

namespace Novolis.Cad.Blueprint;

/// <summary>2D stroke path for plan overlays or section/elevation pages.</summary>
public sealed class CadBlueprintStroke
{
    /// <summary>solid | dashed | dim | heavy</summary>
    public string Style { get; set; } = "solid";

    public string? LayerHint { get; set; }

    public bool Closed { get; set; }

    /// <summary>Points in metres as [x,y,z] (plan/elevation use x/z).</summary>
    public List<float[]> Points { get; set; } = [];
}

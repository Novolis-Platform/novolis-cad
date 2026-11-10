namespace Novolis.Cad.Blueprint;

/// <summary>Text label attached to a blueprint view (stroke pack).</summary>
public sealed class CadBlueprintViewText
{
    public string Text { get; set; } = "";

    /// <summary>Position in metres as [x,y,z].</summary>
    public float[] Position { get; set; } = [0, 0, 0];

    public float Height { get; set; } = 0.2f;
}

using System.Text.Json;

namespace Novolis.Cad.Blueprint;

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

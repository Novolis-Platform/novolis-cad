using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Novolis.Cad.Primitives;

public sealed class CadStyle
{
    public string Linetype { get; set; } = "Continuous";

    public float LineWeightMm { get; set; }

    public float[]? Color { get; set; }

    public int? ColorIndex { get; set; }
}

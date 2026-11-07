using System.Text.Json;
using System.Text.Json.Serialization;

namespace Novolis.Cad.Primitives;

public sealed class CadStroke
{
    public float[]? Color { get; set; }
    public float LineWeightMm { get; set; }
    public string Linetype { get; set; } = "Continuous";
}

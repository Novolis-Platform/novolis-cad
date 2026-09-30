using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Novolis.Cad.Primitives;

public sealed class CadLinetype
{
    public string Name { get; set; } = "Continuous";

    public float[]? Pattern { get; set; }
}

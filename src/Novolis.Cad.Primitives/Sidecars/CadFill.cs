using System.Text.Json;
using System.Text.Json.Serialization;

namespace Novolis.Cad.Primitives;

public sealed class CadFill
{
    public bool Enabled { get; set; } = true;
    public float[]? Color { get; set; }
}

using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Novolis.Cad.Primitives;

public sealed class CadGenerator
{
    public string Name { get; set; } = "Novolis.Cad";

    public string Version { get; set; } = "2026.1.0";
}

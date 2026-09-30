using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Novolis.Cad.Primitives;

public sealed class CadCoordinateSystem
{
    public string Handedness { get; set; } = "right";

    public string UpAxis { get; set; } = "y";

    public string ForwardAxis { get; set; } = "z";
}

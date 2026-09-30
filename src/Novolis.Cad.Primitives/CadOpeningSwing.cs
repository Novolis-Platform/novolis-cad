using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Novolis.Cad.Primitives;

public sealed class CadOpeningSwing
{
    public float StartAngle { get; set; }

    public float EndAngle { get; set; }

    public float[] Direction { get; set; } = [0f, 0f, 1f];
}

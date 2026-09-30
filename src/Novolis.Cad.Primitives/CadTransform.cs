using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Novolis.Cad.Primitives;

public sealed class CadTransform
{
    public float[] Center { get; set; } = [0f, 0f, 0f];

    public float? RotationY { get; set; }

    public float[]? RotationQuat { get; set; }

    public float[]? Scale { get; set; }
}

using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Novolis.Cad.Primitives;

public sealed class CadCamera
{
    public float Yaw { get; set; } = 0.9f;

    public float Pitch { get; set; } = 0.45f;

    public float Distance { get; set; } = 24f;

    public float[] Target { get; set; } = [0f, 0.5f, 0f];
}

using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Novolis.Cad.Primitives;

public sealed class CadLayer
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = "0";

    public bool Visible { get; set; } = true;

    public bool Locked { get; set; }

    public float[]? Color { get; set; }
}

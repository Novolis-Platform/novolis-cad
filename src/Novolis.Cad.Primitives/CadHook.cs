using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Novolis.Cad.Primitives;

public sealed class CadHook
{
    public Guid Id { get; set; }

    public string Tag { get; set; } = "";

    public float[]? Position { get; set; }

    public float[]? Normal { get; set; }

    public Dictionary<string, JsonElement>? Properties { get; set; }
}

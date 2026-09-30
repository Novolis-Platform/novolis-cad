using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Novolis.Cad.Primitives;

public sealed class CadShapeRef
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string? Name { get; set; }
}

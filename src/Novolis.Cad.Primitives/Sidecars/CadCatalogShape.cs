using System.Text.Json;
using System.Text.Json.Serialization;

namespace Novolis.Cad.Primitives;

public sealed class CadCatalogShape
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string? Name { get; set; }
    public CadShapeExtensions? Extensions { get; set; }
}

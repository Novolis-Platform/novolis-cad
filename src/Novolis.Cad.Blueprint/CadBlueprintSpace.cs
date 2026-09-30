using System.Text.Json;
using System.Text.Json.Serialization;
using Novolis.Cad.Primitives;

namespace Novolis.Cad.Blueprint;

/// <summary>Interior compartment / room / cabin / zone.</summary>
public sealed class CadBlueprintSpace
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = "Space";

    public int LevelIndex { get; set; }

    /// <summary>interior | circulation | service | cargo | wet | custom</summary>
    public string Kind { get; set; } = "interior";

    public List<float[]>? Footprint { get; set; }

    public float Height { get; set; }

    public Guid? LayerId { get; set; }

    public string? LayerName { get; set; }

    public Guid? SourceEntityId { get; set; }

    public Dictionary<string, JsonElement>? Properties { get; set; }
}

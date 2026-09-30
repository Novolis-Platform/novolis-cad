using System.Text.Json;
using System.Text.Json.Serialization;
using Novolis.Cad.Primitives;

namespace Novolis.Cad.Blueprint;

/// <summary>Partition / bulkhead / load-bearing wall segment.</summary>
public sealed class CadBlueprintWall
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string? Name { get; set; }

    public int LevelIndex { get; set; }

    public float[]? A { get; set; }

    public float[]? B { get; set; }

    public float Thickness { get; set; }

    public float Height { get; set; }

    public Guid? LayerId { get; set; }

    public string? LayerName { get; set; }

    public Guid? SourceEntityId { get; set; }

    public Dictionary<string, JsonElement>? Properties { get; set; }
}

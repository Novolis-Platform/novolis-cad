using System.Text.Json;
using System.Text.Json.Serialization;
using Novolis.Cad.Primitives;

namespace Novolis.Cad.Blueprint;

/// <summary>Door, hatch, or hole through a wall / shell.</summary>
public sealed class CadBlueprintOpening
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string? Name { get; set; }

    public int LevelIndex { get; set; }

    /// <summary>door | hatch | hole | window | custom</summary>
    public string Kind { get; set; } = "door";

    public float ClearWidth { get; set; }

    public float ClearHeight { get; set; }

    public List<float[]>? Footprint { get; set; }

    public Guid? HostWallId { get; set; }

    public Guid? HostShellId { get; set; }

    public Guid? LayerId { get; set; }

    public string? LayerName { get; set; }

    public Guid? SourceEntityId { get; set; }

    public Dictionary<string, JsonElement>? Properties { get; set; }
}

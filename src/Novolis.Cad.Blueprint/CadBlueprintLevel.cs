using System.Text.Json;
using System.Text.Json.Serialization;
using Novolis.Cad.Primitives;

namespace Novolis.Cad.Blueprint;

/// <summary>Deck / storey / level index used by walls, spaces, and openings.</summary>
public sealed class CadBlueprintLevel
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Stable index — ship deck (0 mid) or building storey (0 ground).</summary>
    public int Index { get; set; }

    public string Name { get; set; } = "Level 0";

    /// <summary>Elevation of finished floor / deck plate in document meters.</summary>
    public float Elevation { get; set; }

    public float? ClearHeight { get; set; }

    public string? Description { get; set; }
}

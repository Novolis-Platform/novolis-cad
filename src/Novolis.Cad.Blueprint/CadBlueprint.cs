using System.Text.Json;
using System.Text.Json.Serialization;
using Novolis.Cad.Primitives;

namespace Novolis.Cad.Blueprint;

/// <summary>
/// Contextual companion to <see cref="CadDocument"/>.
/// <para>
/// <see cref="CadDocument"/> is the full CAD SoT (analytic sketch, solids, modifiers, meshes).
/// <see cref="CadBlueprint"/> is a simplified, domain-shaped projection: shells (exteriors),
/// walls, spaces (interiors), and openings (doors / hatches / holes) — suitable for spaceships,
/// stations, seagoing ships, houses, and skyscrapers — plus optional smart HTML sheet exports.
/// </para>
/// Format id: <c>novolis.cad.blueprint</c> (file: <c>.cadblueprint.json</c>).
/// </summary>
public sealed class CadBlueprint
{
    public string Format { get; set; } = "novolis.cad.blueprint";

    public int SchemaVersion { get; set; } = 1;

    public string Name { get; set; } = "Untitled";

    public CadGenerator Generator { get; set; } = new() { Name = "Novolis.Cad.Blueprint" };

    public string? CreatedAt { get; set; }

    public string? ModifiedAt { get; set; }

    /// <summary>Building context — guides default folders/presets, not a closed world.</summary>
    public string Context { get; set; } = "generic";

    public string LinearUnit { get; set; } = "meter";

    public float UnitScaleMeters { get; set; } = 1f;

    /// <summary>Optional path/URI to the companion <c>.cadjson</c>.</summary>
    public string? CadDocumentHref { get; set; }

    /// <summary>Optional id echoed from the companion document when embedded in a package.</summary>
    public string? CadDocumentName { get; set; }

    /// <summary>Optional layer catalog (<c>novolis.cad.layers</c>).</summary>
    public string? LayerCatalogHref { get; set; }

    public List<CadBlueprintLevel> Levels { get; set; } = [];

    public List<CadBlueprintShell> Shells { get; set; } = [];

    public List<CadBlueprintWall> Walls { get; set; } = [];

    public List<CadBlueprintSpace> Spaces { get; set; } = [];

    public List<CadBlueprintOpening> Openings { get; set; } = [];

    /// <summary>Presentation sheets (smart HTML5 export). Geometry still lives above.</summary>
    public List<CadBlueprintSheet> Sheets { get; set; } = [];

    public Dictionary<string, JsonElement>? Properties { get; set; }
}

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

/// <summary>Exterior / pressure hull / facade envelope (simplified).</summary>
public sealed class CadBlueprintShell
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = "Shell";

    /// <summary>exterior | facade | hull | custom</summary>
    public string Kind { get; set; } = "exterior";

    /// <summary>Closed plan ring as [x,y,z] samples (Y often 0 in plan).</summary>
    public List<float[]>? PlanRing { get; set; }

    public float? Height { get; set; }

    public Guid? SourceEntityId { get; set; }

    public Dictionary<string, JsonElement>? Properties { get; set; }
}

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

/// <summary>Well-known <see cref="CadBlueprint.Context"/> values — open set; custom strings allowed.</summary>
public static class CadBlueprintContexts
{
    public const string Generic = "generic";
    public const string Spaceship = "spaceship";
    public const string SpaceStation = "space-station";
    public const string SeagoingShip = "seagoing-ship";
    public const string House = "house";
    public const string Skyscraper = "skyscraper";
}

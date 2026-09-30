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

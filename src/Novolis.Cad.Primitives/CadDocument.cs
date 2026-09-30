using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Novolis.Cad.Primitives;

/// <summary>In-memory / on-disk <c>novolis.cad</c> document (.cadjson).</summary>
public sealed class CadDocument
{
    public string Format { get; set; } = "novolis.cad";

    public int SchemaVersion { get; set; } = 1;

    public string Name { get; set; } = "Untitled";

    public CadGenerator Generator { get; set; } = new();

    public string? CreatedAt { get; set; }

    public string? ModifiedAt { get; set; }

    public float UnitScaleMeters { get; set; } = 1f;

    public string LinearUnit { get; set; } = "meter";

    public string AngleUnit { get; set; } = "radian";

    public CadCoordinateSystem CoordinateSystem { get; set; } = new();

    /// <summary>Optional sidecar path (layers catalog).</summary>
    public string? LayersDocument { get; set; }

    /// <summary>Optional sidecar path (shapes catalog).</summary>
    public string? ShapesDocument { get; set; }

    public List<CadLayer> Layers { get; set; } = [];

    public List<CadLinetype> Linetypes { get; set; } = [new() { Name = "Continuous" }];

    /// <summary>Optional inline shapes (may also use a sidecar).</summary>
    public List<CadShapeRef>? Shapes { get; set; }

    public List<CadEntity> Entities { get; set; } = [];

    public CadCamera Camera { get; set; } = new();

    public Dictionary<string, JsonElement>? Properties { get; set; }
}

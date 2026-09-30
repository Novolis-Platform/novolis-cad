using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Novolis.Cad.Primitives;

/// <summary>Single entity; unused fields stay null/default for the active <see cref="Kind"/>.</summary>
public sealed class CadEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string? Name { get; set; }

    public Guid? LayerId { get; set; }

    public Guid? ParentId { get; set; }

    public string Kind { get; set; } = "line";

    public List<CadHook>? Hooks { get; set; }

    public Guid? ShapeId { get; set; }

    public CadStyle? Style { get; set; }

    public string? Material { get; set; }

    public float[]? Color { get; set; }

    public float[]? A { get; set; }

    public float[]? B { get; set; }

    public float[]? Center { get; set; }

    public float Radius { get; set; }

    public float Height { get; set; }

    /// <summary>Wall / extruded thickness (meters). Also used as box width fallback.</summary>
    public float Thickness { get; set; }

    /// <summary>Ship deck index; 0 = mid, negative = lower.</summary>
    public int Deck { get; set; }

    public float[]? HalfExtents { get; set; }

    public float[]? Min { get; set; }

    public float[]? Max { get; set; }

    public float[]? Normal { get; set; }

    public float StartAngle { get; set; }

    public float EndAngle { get; set; }

    public float RotationY { get; set; }

    public List<float[]>? Points { get; set; }

    public CadWallSides? Sides { get; set; }

    public Guid? FloorShapeId { get; set; }

    public Guid? CeilingShapeId { get; set; }

    public string? OpeningType { get; set; }

    public List<float[]>? Footprint { get; set; }

    public Guid? HostWallId { get; set; }

    public List<string>? ConnectsSides { get; set; }

    public CadOpeningSwing? Swing { get; set; }

    public string? Operation { get; set; }

    public Guid? LeftId { get; set; }

    public Guid? RightId { get; set; }

    /// <summary>Boolean target alias for <see cref="LeftId"/>.</summary>
    public Guid? TargetId { get; set; }

    /// <summary>Boolean cutter alias for <see cref="RightId"/>.</summary>
    public Guid? CutterId { get; set; }

    /// <summary>Named operand role when this entity is linked under a generator (e.g. Target, Cutter, Source).</summary>
    public string? OperandRole { get; set; }

    public string? Mode { get; set; }

    public float? TouchEpsilonMeters { get; set; }

    public List<Guid>? MemberIds { get; set; }

    public Guid? PrototypeId { get; set; }

    /// <summary>Single-input stack link (mesh modifiers, MeshFromSolid source).</summary>
    public Guid? SourceId { get; set; }

    /// <summary>Modifier stack input (same as SourceId; preferred for weld/optimize/bridge).</summary>
    public Guid? InputId { get; set; }

    /// <summary>MeshFromSolid link mode: linked | detached | baked.</summary>
    public string? LinkMode { get; set; }

    /// <summary>Clone / array realization: instances | separateCopies | fusedSolid.</summary>
    public string? Realization { get; set; }

    /// <summary>Symmetry / split plane as point+normal (normal in <see cref="Normal"/>).</summary>
    public float[]? PlanePoint { get; set; }

    public bool MergeAtPlane { get; set; }

    public float MergeTolerance { get; set; }

    /// <summary>Inline baked mesh vertices (for mesh / baked MeshFromSolid).</summary>
    public List<float[]>? MeshVertices { get; set; }

    public List<int>? MeshIndices { get; set; }

    /// <summary>Preview light intensity / type extras.</summary>
    public float Intensity { get; set; }

    public string? LightType { get; set; }

    public bool Visible { get; set; } = true;

    public CadTransform? Transform { get; set; }

    public CadTransform? BaseTransform { get; set; }

    public int[]? Counts { get; set; }

    public float[]? Spacing { get; set; }

    /// <summary>Radial clone: axis + step radians + count (Count in Counts[0] when set).</summary>
    public float[]? Axis { get; set; }

    public float? StepRadians { get; set; }

    public CadSpaceFlags? Flags { get; set; }

    public bool Closed { get; set; }

    public int Degree { get; set; }

    public List<float[]>? ControlPoints { get; set; }

    public float[]? Knots { get; set; }

    public float[]? Weights { get; set; }

    public bool Periodic { get; set; }

    public List<float[]>? FitPoints { get; set; }

    public Dictionary<string, JsonElement>? Properties { get; set; }

    [JsonIgnore]
    public bool IsSolid => Kind is "box" or "cylinder" or "sphere" or "cone" or "wedge";

    [JsonIgnore]
    public string Summary
    {
        get
        {
            var name = Name ?? Kind;
            return Kind.ToLowerInvariant() switch
            {
                "line" when A is { Length: >= 3 } && B is { Length: >= 3 } =>
                    $"{name} — line ({A[0]:0.##},{A[2]:0.##})→({B[0]:0.##},{B[2]:0.##})",
                "circle" => $"{name} — circle r={Radius:0.##}",
                "rect" => $"{name} — rect",
                "spline" => $"{name} — spline deg={Degree} cps={ControlPoints?.Count ?? 0}",
                "box" when CadShipGeometry.TryGetBox(this, out _, out var he) =>
                    $"{name} — box {he.X * 2:0.##}×{he.Y * 2:0.##}×{he.Z * 2:0.##}",
                "wall" => $"{name} — wall t={Thickness:0.##} h={Height:0.##} deck={Deck}",
                "space" => $"{name} — space h={Height:0.##} deck={Deck}",
                "opening" => $"{name} — opening {OpeningType ?? "door"}",
                "cylinder" => $"{name} — cylinder r={Radius:0.##} h={Height:0.##}",
                "sphere" => $"{name} — sphere r={Radius:0.##}",
                _ => $"{name} — {Kind}",
            };
        }
    }
}

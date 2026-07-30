using System.Numerics;
using System.Text.Json.Serialization;
using Novolis.Math.Geometry;

namespace Novolis.Modeling.Scene;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum LightKind
{
    Omni,
    Spot,
    Infinite,
    Area,
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MeshPrimitiveKind
{
    Box,
    Sphere,
    Plane,
    Cylinder,
    Cone,
    Capsule,
    Torus,
    Pyramid,
    Disc,
    Tube,
    PlatonicTetra,
    PlatonicOcta,
    PlatonicIcosa,
    PlatonicDodeca,
    Landscape,
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum GeneratorKind
{
    Cloner,
    Symmetry,
    Boole,
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum BooleanKind
{
    Union,
    Difference,
    Intersection,
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ModifierKind
{
    Weld,
    Subdivision,
    Optimize,
    Bridge,
    Extrude,
    Bevel,
    Inset,
    Dissolve,
    Knife,
}

/// <summary>Local transform (translation, euler degrees, scale).</summary>
public sealed class SceneTransform
{
    public float[] Position { get; set; } = [0, 0, 0];
    public float[] RotationDeg { get; set; } = [0, 0, 0];
    public float[] Scale { get; set; } = [1, 1, 1];

    public Vector3 PositionV => new(Position[0], Position[1], Position[2]);
    public Vector3 RotationDegV => new(RotationDeg[0], RotationDeg[1], RotationDeg[2]);
    public Vector3 ScaleV => new(Scale[0], Scale[1], Scale[2]);

    public SceneTransform Clone() => new()
    {
        Position = [Position[0], Position[1], Position[2]],
        RotationDeg = [RotationDeg[0], RotationDeg[1], RotationDeg[2]],
        Scale = [Scale[0], Scale[1], Scale[2]],
    };

    public Matrix4x4 ToMatrix()
    {
        var t = Matrix4x4.CreateTranslation(PositionV);
        var rx = Matrix4x4.CreateRotationX(RotationDeg[0] * MathF.PI / 180f);
        var ry = Matrix4x4.CreateRotationY(RotationDeg[1] * MathF.PI / 180f);
        var rz = Matrix4x4.CreateRotationZ(RotationDeg[2] * MathF.PI / 180f);
        var s = Matrix4x4.CreateScale(ScaleV);
        return s * rx * ry * rz * t;
    }
}

/// <summary>Evaluated triangle mesh ready for viewport / further ops.</summary>
public sealed class EvaluatedMesh
{
    public required Guid SourceId { get; init; }
    public required Vector3[] Vertices { get; init; }
    public required int[] Indices { get; init; }
    public required Matrix4x4 World { get; init; }

    public EditableMesh ToEditableMesh() => new(Vertices, Indices);

    public static EvaluatedMesh FromEditable(Guid sourceId, EditableMesh mesh, Matrix4x4 world) => new()
    {
        SourceId = sourceId,
        Vertices = mesh.Vertices.ToArray(),
        Indices = mesh.Indices.ToArray(),
        World = world,
    };
}

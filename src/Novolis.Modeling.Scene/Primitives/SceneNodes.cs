using System.Text.Json.Serialization;

namespace Novolis.Modeling.Scene;

/// <summary>Base scene graph node.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(GroupNode), "group")]
[JsonDerivedType(typeof(MeshNode), "mesh")]
[JsonDerivedType(typeof(GeneratorNode), "generator")]
[JsonDerivedType(typeof(ModifierNode), "modifier")]
[JsonDerivedType(typeof(MaterialNode), "material")]
[JsonDerivedType(typeof(LightNode), "light")]
[JsonDerivedType(typeof(CameraNode), "camera")]
[JsonDerivedType(typeof(NullNode), "null")]
public abstract class SceneNode
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Node";
    public Guid? ParentId { get; set; }
    public SceneTransform Transform { get; set; } = new();
    public bool Visible { get; set; } = true;
}

public sealed class GroupNode : SceneNode
{
    public GroupNode() => Name = "Group";
}

public sealed class MeshNode : SceneNode
{
    public MeshNode() => Name = "Mesh";

    public MeshPrimitiveKind Primitive { get; set; } = MeshPrimitiveKind.Box;
    public float[] Size { get; set; } = [1, 1, 1];
    public int Segments { get; set; } = 16;
    public Guid? MaterialId { get; set; }

    /// <summary>Optional raw triangle soup (xyz per vertex) for baked meshes.</summary>
    public float[]? Vertices { get; set; }
    public int[]? Indices { get; set; }
}

public sealed class GeneratorNode : SceneNode
{
    public GeneratorNode() => Name = "Generator";

    public GeneratorKind Generator { get; set; } = GeneratorKind.Cloner;
    public Guid? SourceId { get; set; }
    public Guid? TargetId { get; set; }
    public Guid? CutterId { get; set; }
    public BooleanKind BooleanKind { get; set; } = BooleanKind.Difference;
    public int Count { get; set; } = 3;
    public float[] Offset { get; set; } = [1.5f, 0, 0];
    public string Axis { get; set; } = "x";
}

public sealed class ModifierNode : SceneNode
{
    public ModifierNode() => Name = "Modifier";

    public ModifierKind Modifier { get; set; } = ModifierKind.Weld;
    public Guid? InputId { get; set; }
    public float Tolerance { get; set; } = 0.001f;
    public int Levels { get; set; } = 1;
    public float Distance { get; set; } = 0.2f;
}

public sealed class MaterialNode : SceneNode
{
    public MaterialNode() => Name = "Material";

    public float[] Color { get; set; } = [0.75f, 0.75f, 0.78f];
    public float Roughness { get; set; } = 0.45f;
    public float Metallic { get; set; }
}

public sealed class LightNode : SceneNode
{
    public LightNode() => Name = "Light";

    public LightKind LightKind { get; set; } = LightKind.Omni;
    public float[] Color { get; set; } = [1, 1, 1];
    public float Intensity { get; set; } = 1f;
    public float? TemperatureKelvin { get; set; }
    public float ConeAngleDeg { get; set; } = 45f;
    public float PenumbraDeg { get; set; } = 5f;
    public float[] AreaSize { get; set; } = [1, 1];
    public bool CastShadows { get; set; } = true;
    public bool Enabled { get; set; } = true;
}

public sealed class CameraNode : SceneNode
{
    public CameraNode() => Name = "Camera";

    public float FovDeg { get; set; } = 45f;
    public float Near { get; set; } = 0.1f;
    public float Far { get; set; } = 1000f;
    public float[] Target { get; set; } = [0, 0, 0];
}

public sealed class NullNode : SceneNode
{
    public NullNode() => Name = "Null";
}

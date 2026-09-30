namespace Novolis.Cad.Primitives;

public sealed class CadCollider
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid? EntityId { get; set; }

    public string Kind { get; set; } = "box";

    public float[]? Center { get; set; }

    public float[]? HalfExtents { get; set; }

    public float Radius { get; set; }

    public float[]? A { get; set; }

    public float[]? B { get; set; }

    public Guid? MeshId { get; set; }

    public bool IsTrigger { get; set; }

    public CadColliderBody? Body { get; set; }
}

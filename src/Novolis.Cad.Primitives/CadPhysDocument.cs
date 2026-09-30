namespace Novolis.Cad.Primitives;

/// <summary><c>novolis.cad.phys</c> document (.cadphys.json).</summary>
public sealed class CadPhysDocument
{
    public string Format { get; set; } = "novolis.cad.phys";

    public int SchemaVersion { get; set; } = 1;

    public string Name { get; set; } = "Untitled";

    public CadGenerator Generator { get; set; } = new();

    public string? CreatedAt { get; set; }

    public string? ModifiedAt { get; set; }

    public float UnitScaleMeters { get; set; } = 1f;

    public string LinearUnit { get; set; } = "meter";

    public string AngleUnit { get; set; } = "radian";

    public string UpAxis { get; set; } = "y";

    public string? BaseDocument { get; set; }

    public List<CadMesh> Meshes { get; set; } = [];

    public List<CadCollider> Colliders { get; set; } = [];
}

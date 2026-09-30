namespace Novolis.Cad.Primitives;

public sealed class CadMesh
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string? Name { get; set; }

    public Guid? EntityId { get; set; }

    public List<float[]> Vertices { get; set; } = [];

    public List<int> Indices { get; set; } = [];

    public List<float[]>? Normals { get; set; }

    public string Winding { get; set; } = "ccw";

    public string Space { get; set; } = "local";

    public string? Material { get; set; }
}

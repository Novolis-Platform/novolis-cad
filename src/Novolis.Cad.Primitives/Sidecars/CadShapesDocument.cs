using System.Text.Json;
using System.Text.Json.Serialization;

namespace Novolis.Cad.Primitives;

/// <summary>Shapes catalog sidecar (<c>novolis.cad.shape</c>) with appearance/material extensions.</summary>
public sealed class CadShapesDocument
{
    public string Format { get; set; } = "novolis.cad.shape";
    public int SchemaVersion { get; set; } = 1;
    public string Name { get; set; } = "shapes";
    public CadGenerator Generator { get; set; } = new() { Name = "Novolis.Cad" };
    public string? CreatedAt { get; set; }
    public string? ModifiedAt { get; set; }
    public string? BaseDocument { get; set; }
    public List<CadCatalogShape> Shapes { get; set; } = [];
}

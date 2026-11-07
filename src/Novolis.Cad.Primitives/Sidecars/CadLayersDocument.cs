using System.Text.Json;
using System.Text.Json.Serialization;

namespace Novolis.Cad.Primitives;

/// <summary>Layers catalog sidecar sidecar (<c>novolis.cad.layers</c>).</summary>
public sealed class CadLayersDocument
{
    public string Format { get; set; } = "novolis.cad.layers";
    public int SchemaVersion { get; set; } = 1;
    public string Name { get; set; } = "layers";
    public string Standard { get; set; } = "custom";
    public string? StandardVersion { get; set; }
    public CadGenerator Generator { get; set; } = new() { Name = "Novolis.Cad" };
    public string? CreatedAt { get; set; }
    public string? ModifiedAt { get; set; }
    public List<CadCatalogLayer> Layers { get; set; } = [];
}

using System.Text.Json;
using System.Text.Json.Serialization;
using Novolis.Cad.Primitives;

namespace Novolis.Cad.Blueprint;

/// <summary>Exterior / pressure hull / facade envelope (simplified).</summary>
public sealed class CadBlueprintShell
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = "Shell";

    /// <summary>exterior | facade | hull | custom</summary>
    public string Kind { get; set; } = "exterior";

    /// <summary>Closed plan ring as [x,y,z] samples (Y often 0 in plan).</summary>
    public List<float[]>? PlanRing { get; set; }

    public float? Height { get; set; }

    public Guid? SourceEntityId { get; set; }

    public Dictionary<string, JsonElement>? Properties { get; set; }
}

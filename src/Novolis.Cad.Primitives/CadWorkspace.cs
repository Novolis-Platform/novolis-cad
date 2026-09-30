namespace Novolis.Cad.Primitives;

/// <summary>Top-level editor workspace lens over one document.</summary>
public enum CadWorkspace
{
    /// <summary>Exact solids / sketches (legacy alias: draft).</summary>
    Cad = 0,

    /// <summary>Polygon modeling modifiers on MeshFromSolid adapters.</summary>
    Modeling = 1,

    /// <summary>Materials, lights, cameras (legacy alias: model).</summary>
    Preview = 2,
}

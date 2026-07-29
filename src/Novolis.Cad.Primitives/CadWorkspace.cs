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

public enum CadSelectionMode
{
    Object = 0,
    Body = 1,
    SketchElement = 2,
    MeshIsland = 3,
    Face = 4,
    Edge = 5,
    Vertex = 6,
    MaterialSlot = 7,
    Light = 8,
    Camera = 9,
}

public enum CadSceneNodeCategory
{
    Group,
    Geometry,
    Generator,
    MeshFromSolid,
    MeshModifier,
    Material,
    Light,
    Camera,
    Transform,
    Unknown,
}

public enum MeshLinkMode
{
    Linked,
    Detached,
    Baked,
}

public enum CloneRealization
{
    Instances,
    SeparateCopies,
    FusedSolid,
}

public enum ConnectMode
{
    Group,
    JoinMesh,
    CompoundSolid,
    FuseSolid,
}

public enum SplitMode
{
    CuttingPlane,
    ConnectedComponents,
    SelectedFaces,
    CuttingSolid,
}

/// <summary>Legacy Draft/Model dual view — prefer <see cref="CadWorkspace"/>.</summary>
public enum CadViewMode
{
    Draft,
    Model,
}

public static class CadWorkspaceMapping
{
    public static CadWorkspace FromViewMode(CadViewMode mode) =>
        mode == CadViewMode.Model ? CadWorkspace.Preview : CadWorkspace.Cad;

    public static CadViewMode ToViewMode(CadWorkspace workspace) =>
        workspace == CadWorkspace.Cad ? CadViewMode.Draft : CadViewMode.Model;

    public static CadWorkspace Parse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return CadWorkspace.Cad;
        return raw.Trim().ToLowerInvariant() switch
        {
            "modeling" or "modeler" => CadWorkspace.Modeling,
            "preview" or "model" or "render" => CadWorkspace.Preview,
            "cad" or "draft" or "sketch" => CadWorkspace.Cad,
            _ => CadWorkspace.Cad,
        };
    }

    public static string ToStorage(CadWorkspace workspace) =>
        workspace switch
        {
            CadWorkspace.Modeling => "modeling",
            CadWorkspace.Preview => "preview",
            _ => "cad",
        };

    public static string ToDisplay(CadWorkspace workspace) =>
        workspace switch
        {
            CadWorkspace.Modeling => "Modeling",
            CadWorkspace.Preview => "Preview",
            _ => "CAD",
        };
}

using System.Text.Json.Serialization;

namespace Novolis.Modeling.Scene;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SceneEditMode
{
    Object,
    Point,
    Edge,
    Polygon,
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SceneDisplayMode
{
    Wireframe,
    WirePoints,
    Isoline,
}

/// <summary>Runtime component selection / display state (not persisted in .nov3djson).</summary>
public sealed class MeshEditState
{
    public SceneEditMode Mode { get; set; } = SceneEditMode.Object;
    public SceneDisplayMode DisplayMode { get; set; } = SceneDisplayMode.Wireframe;
    public Guid? EditMeshId { get; set; }
    public HashSet<int> SelectedVertices { get; } = [];
    public HashSet<(int A, int B)> SelectedEdges { get; } = [];
    public HashSet<int> SelectedFaces { get; } = [];

    public int SelectionCount => Mode switch
    {
        SceneEditMode.Point => SelectedVertices.Count,
        SceneEditMode.Edge => SelectedEdges.Count,
        SceneEditMode.Polygon => SelectedFaces.Count,
        _ => 0,
    };

    public void ClearComponents()
    {
        SelectedVertices.Clear();
        SelectedEdges.Clear();
        SelectedFaces.Clear();
    }

    public void ClearAll()
    {
        ClearComponents();
        EditMeshId = null;
        Mode = SceneEditMode.Object;
    }
}

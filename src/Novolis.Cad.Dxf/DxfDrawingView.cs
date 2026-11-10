namespace Novolis.Cad.Dxf;

/// <summary>One INSERT drawing view discovered in a sheet-style DXF (plan / section / elevation).</summary>
public sealed class DxfDrawingView
{
    public required string Id { get; init; }

    /// <summary>plan | section | elevation | detail | other</summary>
    public required string Kind { get; init; }

    public required string Label { get; init; }

    public string? ScaleHint { get; init; }

    public required string SourceBlockName { get; init; }

    public int StrokeCount { get; init; }

    public int TextCount { get; init; }
}

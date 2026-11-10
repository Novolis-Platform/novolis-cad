using Novolis.Cad.Primitives;

namespace Novolis.Cad.Dxf;

public sealed class DxfImportResult
{
    public required CadDocument Document { get; init; }

    public required double UnitScaleMeters { get; init; }

    public required string UnitReason { get; init; }

    public int WallCount { get; init; }

    public int SpaceCount { get; init; }

    public int OpeningCount { get; init; }

    public int SkippedEntityCount { get; init; }

    public int StrokeCount { get; init; }

    public IReadOnlyList<DxfDrawingView> Views { get; init; } = [];

    public IReadOnlyList<string> Notes { get; init; } = [];
}

namespace Novolis.Cad.Dxf;

/// <summary>Controls DXF → <c>CadDocument</c> lift heuristics.</summary>
public sealed class DxfImportOptions
{
    /// <summary>
    /// Force metres-per-drawing-unit. When null, pick among mm/in/ft/m so the
    /// longest plan span lands in about 5–200 m (architectural sanity).
    /// </summary>
    public double? UnitScaleMeters { get; init; }

    /// <summary>Default wall thickness in metres after unit conversion.</summary>
    public float WallThicknessMeters { get; init; } = 0.15f;

    /// <summary>Default wall / space height in metres.</summary>
    public float WallHeightMeters { get; init; } = 2.7f;

    /// <summary>Substring match (ordinal ignore-case) for wall layers.</summary>
    public IReadOnlyList<string> WallLayerHints { get; init; } =
        ["A-WALL", "WALL"];

    /// <summary>Substring match for opening / door / window layers.</summary>
    public IReadOnlyList<string> OpeningLayerHints { get; init; } =
        ["OPENING", "DOOR", "WINDOW", "GLAZ", "GARAGE"];

    /// <summary>Substring match for closed space / room polylines.</summary>
    public IReadOnlyList<string> SpaceLayerHints { get; init; } =
        ["FOOTPRINT", "SPACE", "ROOM", "AREA"];

    /// <summary>
    /// Layers matching these hints are never lifted as walls/openings/spaces
    /// (dims, casework, structure notes, temp geometry).
    /// </summary>
    public IReadOnlyList<string> ExcludeLayerHints { get; init; } =
    [
        "DIM", "TEXT", "NOTE", "HATCH", "CASE", "JOIST", "BEAM",
        "OVERHANG", "HEADER", "TEMP", "FURN", "FIXT", "ANNOT", "GRID",
    ];

    /// <summary>When true, also treat closed LWPOLYLINE on wall layers as spaces.</summary>
    public bool ClosedWallPolylinesAsSpaces { get; init; }

    /// <summary>Skip LINE shorter than this (metres) when lifting walls.</summary>
    public float MinWallLengthMeters { get; init; } = 0.35f;

    /// <summary>
    /// Pair parallel wall-face lines into one centerline wall with measured thickness
    /// (AIA double-line plans). When false, every face becomes a solid of
    /// <see cref="WallThicknessMeters"/>.
    /// </summary>
    public bool PairParallelWallFaces { get; init; } = true;

    /// <summary>Minimum face-pair separation treated as wall thickness (metres).</summary>
    public float MinWallThicknessMeters { get; init; } = 0.05f;

    /// <summary>Maximum face-pair separation treated as wall thickness (metres).</summary>
    public float MaxWallThicknessMeters { get; init; } = 0.45f;

    /// <summary>
    /// Keep face lines that have no parallel partner. Default false: AIA double-line
    /// leftovers otherwise redraw as overlapping thick solids.
    /// </summary>
    public bool KeepUnpairedWallFaces { get; init; }

    /// <summary>Opening clear width must be at least this (metres).</summary>
    public float MinOpeningLengthMeters { get; init; } = 0.55f;

    /// <summary>Opening clear width must be at most this (metres).</summary>
    public float MaxOpeningLengthMeters { get; init; } = 3.6f;

    /// <summary>Merge openings whose midpoints fall within this distance (metres).</summary>
    public float OpeningDedupeMeters { get; init; } = 0.4f;

    /// <summary>Document name override.</summary>
    public string? DocumentName { get; init; }
}

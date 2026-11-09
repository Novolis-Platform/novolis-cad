namespace Novolis.Cad.Blueprint;

/// <summary>Sheet layout / plan projection metrics for a blueprint sheet.</summary>
public sealed class CadBlueprintSheetLayout
{
    public required CadBlueprintSheetSize Size { get; init; }

    public required bool SternLeft { get; init; }

    public required double Scale { get; init; }

    public required double OriginX { get; init; }

    public required double OriginY { get; init; }

    public required double ScaleRatio { get; init; }

    public required double MinX { get; init; }

    public required double MaxX { get; init; }

    public required double MinZ { get; init; }

    public required double MaxZ { get; init; }

    public double SpanX => System.Math.Max(0.2, MaxX - MinX);

    public double SpanY => System.Math.Max(0.2, MaxZ - MinZ);

    public double PageX(float x, float z) => SternLeft ? OriginX + z * Scale : OriginX + x * Scale;

    public double PageY(float x, float z) => SternLeft ? OriginY - x * Scale : OriginY - z * Scale;

    public static CadBlueprintSheetLayout Compute(CadBlueprint blueprint, CadBlueprintSheet sheet)
    {
        ArgumentNullException.ThrowIfNull(blueprint);
        ArgumentNullException.ThrowIfNull(sheet);

        var size = sheet.Sheet ?? CadBlueprintSheetSize.A1Landscape();
        var sternLeft = sheet.Orientation.Contains("stern-left", StringComparison.OrdinalIgnoreCase);
        var (minX, maxX, minZ, maxZ) = CollectBounds(blueprint);

        const double pad = 28.0;
        const double titleBlockH = 56.0;
        var contentW = size.WidthMm - size.BorderLeftMm - size.BorderMm - 2 * pad - 8;
        var contentH = size.HeightMm - 2 * size.BorderMm - 2 * pad - titleBlockH - 12;
        var pageSpanX = sternLeft ? System.Math.Max(0.2, maxZ - minZ) : System.Math.Max(0.2, maxX - minX);
        var pageSpanY = sternLeft ? System.Math.Max(0.2, maxX - minX) : System.Math.Max(0.2, maxZ - minZ);
        var scale = pageSpanX > 1e-6 && pageSpanY > 1e-6
            ? System.Math.Min(contentW / pageSpanX, contentH / pageSpanY) * 0.88
            : 1.0;
        var ox = sternLeft
            ? size.BorderLeftMm + pad + (contentW - pageSpanX * scale) * 0.5 - minZ * scale
            : size.BorderLeftMm + pad + (contentW - pageSpanX * scale) * 0.5 - minX * scale;
        var oy = sternLeft
            ? size.BorderMm + pad + 22 + (contentH - pageSpanY * scale) * 0.5 + maxX * scale
            : size.BorderMm + pad + 22 + (contentH - pageSpanY * scale) * 0.5 + maxZ * scale;
        var scaleRatio = scale > 1e-6 ? System.Math.Max(1, System.Math.Round(1000.0 / scale)) : 1;

        return new CadBlueprintSheetLayout
        {
            Size = size,
            SternLeft = sternLeft,
            Scale = scale,
            OriginX = ox,
            OriginY = oy,
            ScaleRatio = scaleRatio,
            MinX = minX,
            MaxX = maxX,
            MinZ = minZ,
            MaxZ = maxZ,
        };
    }

    public static void PlanXZ(float[] p, out float x, out float z)
    {
        x = p.Length > 0 ? p[0] : 0;
        z = p.Length > 2 ? p[2] : p.Length > 1 ? p[1] : 0;
    }

    private static (double MinX, double MaxX, double MinZ, double MaxZ) CollectBounds(CadBlueprint blueprint)
    {
        var minX = double.PositiveInfinity;
        var maxX = double.NegativeInfinity;
        var minZ = double.PositiveInfinity;
        var maxZ = double.NegativeInfinity;
        var any = false;

        void Acc(float x, float z)
        {
            any = true;
            minX = System.Math.Min(minX, x);
            maxX = System.Math.Max(maxX, x);
            minZ = System.Math.Min(minZ, z);
            maxZ = System.Math.Max(maxZ, z);
        }

        foreach (var shell in blueprint.Shells)
        {
            if (shell.PlanRing is null) continue;
            foreach (var p in shell.PlanRing)
            {
                PlanXZ(p, out var x, out var z);
                Acc(x, z);
            }
        }

        foreach (var space in blueprint.Spaces)
        {
            if (space.Footprint is null) continue;
            foreach (var p in space.Footprint)
            {
                PlanXZ(p, out var x, out var z);
                Acc(x, z);
            }
        }

        foreach (var wall in blueprint.Walls)
        {
            if (wall.A is not null) { PlanXZ(wall.A, out var x, out var z); Acc(x, z); }
            if (wall.B is not null) { PlanXZ(wall.B, out var x, out var z); Acc(x, z); }
        }

        foreach (var opening in blueprint.Openings)
        {
            if (opening.Footprint is null) continue;
            foreach (var p in opening.Footprint)
            {
                PlanXZ(p, out var x, out var z);
                Acc(x, z);
            }
        }

        return any ? (minX, maxX, minZ, maxZ) : (0, 10, 0, 10);
    }
}

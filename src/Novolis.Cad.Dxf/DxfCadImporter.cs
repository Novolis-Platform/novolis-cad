using ACadSharp.Entities;
using ACadSharp.IO;
using ACadSharp.Types.Units;
using Novolis.Cad.Primitives;
using CSMath;

namespace Novolis.Cad.Dxf;

/// <summary>
/// Lifts architectural DXF floor plans into <see cref="CadDocument"/> using ACadSharp.
/// Layer-name heuristics map LINE / LWPOLYLINE to walls, spaces, and openings.
/// </summary>
public static class DxfCadImporter
{
    public static DxfImportResult ImportFile(string path, DxfImportOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        options ??= new DxfImportOptions();
        var dxf = DxfReader.Read(path);
        return Import(dxf, options, Path.GetFileNameWithoutExtension(path));
    }

    public static DxfImportResult ImportStream(Stream stream, DxfImportOptions? options = null, string? name = null)
    {
        ArgumentNullException.ThrowIfNull(stream);
        options ??= new DxfImportOptions();
        var dxf = DxfReader.Read(stream);
        return Import(dxf, options, name ?? "dxf-import");
    }

    public static DxfImportResult Import(ACadSharp.CadDocument dxf, DxfImportOptions options, string name)
    {
        ArgumentNullException.ThrowIfNull(dxf);
        ArgumentNullException.ThrowIfNull(options);

        var notes = new List<string>();
        var (scale, reason) = ResolveUnitScale(dxf, options, notes);
        notes.Add(reason);

        var wallLayerId = Guid.Parse("d1000000-0000-4000-8000-000000000001");
        var openingLayerId = Guid.Parse("d1000000-0000-4000-8000-000000000002");
        var spaceLayerId = Guid.Parse("d1000000-0000-4000-8000-000000000003");

        var doc = new CadDocument
        {
            Name = options.DocumentName ?? name,
            LinearUnit = "meter",
            UnitScaleMeters = 1f,
            CreatedAt = DateTime.UtcNow.ToString("o"),
            ModifiedAt = DateTime.UtcNow.ToString("o"),
            Generator = new CadGenerator { Name = "Novolis.Cad.Dxf", Version = "1" },
        };
        doc.Layers.Add(new CadLayer { Id = wallLayerId, Name = "A-WALL" });
        doc.Layers.Add(new CadLayer { Id = openingLayerId, Name = "A-OPENING" });
        doc.Layers.Add(new CadLayer { Id = spaceLayerId, Name = "A-SPACE" });

        var walls = 0;
        var spaces = 0;
        var openings = 0;
        var skipped = 0;

        foreach (var entity in dxf.Entities)
        {
            var layerName = entity.Layer?.Name ?? "";
            switch (entity)
            {
                case Line line when Matches(layerName, options.WallLayerHints)
                                    && !Matches(layerName, options.OpeningLayerHints):
                {
                    if (TryAddWall(doc, wallLayerId, Xy(line.StartPoint), Xy(line.EndPoint), scale, options))
                        walls++;
                    else
                        skipped++;
                    break;
                }
                case Line line when Matches(layerName, options.OpeningLayerHints):
                {
                    if (TryAddOpeningFromSegment(doc, openingLayerId, Xy(line.StartPoint), Xy(line.EndPoint), scale, options))
                        openings++;
                    else
                        skipped++;
                    break;
                }
                case LwPolyline poly:
                {
                    var pts = poly.Vertices.Select(v => (v.Location.X, v.Location.Y)).ToList();
                    if (pts.Count < 2)
                    {
                        skipped++;
                        break;
                    }

                    if (poly.IsClosed && (Matches(layerName, options.SpaceLayerHints)
                                          || (options.ClosedWallPolylinesAsSpaces
                                              && Matches(layerName, options.WallLayerHints))))
                    {
                        if (TryAddSpace(doc, spaceLayerId, pts, scale, options, layerName))
                            spaces++;
                        else
                            skipped++;
                    }
                    else if (Matches(layerName, options.WallLayerHints)
                             && !Matches(layerName, options.OpeningLayerHints))
                    {
                        var added = 0;
                        for (var i = 0; i < pts.Count - 1; i++)
                        {
                            if (TryAddWall(doc, wallLayerId, pts[i], pts[i + 1], scale, options))
                                added++;
                        }

                        if (poly.IsClosed && pts.Count >= 2
                            && TryAddWall(doc, wallLayerId, pts[^1], pts[0], scale, options))
                            added++;
                        walls += added;
                        if (added == 0) skipped++;
                    }
                    else if (Matches(layerName, options.OpeningLayerHints) && pts.Count >= 2)
                    {
                        if (TryAddOpeningFromSegment(doc, openingLayerId, pts[0], pts[^1], scale, options))
                            openings++;
                        else
                            skipped++;
                    }
                    else
                    {
                        skipped++;
                    }

                    break;
                }
                default:
                    skipped++;
                    break;
            }
        }

        ApplyTextLabels(dxf, doc, scale);
        notes.Add($"Lifted walls={walls} spaces={spaces} openings={openings} skipped={skipped}.");

        return new DxfImportResult
        {
            Document = doc,
            UnitScaleMeters = scale,
            UnitReason = reason,
            WallCount = walls,
            SpaceCount = spaces,
            OpeningCount = openings,
            SkippedEntityCount = skipped,
            Notes = notes,
        };
    }

    private static (double X, double Y) Xy(XYZ p) => (p.X, p.Y);

    private static (double Scale, string Reason) ResolveUnitScale(
        ACadSharp.CadDocument dxf,
        DxfImportOptions options,
        List<string> notes)
    {
        if (options.UnitScaleMeters is { } forced && forced > 0)
            return (forced, $"Forced unit scale {forced} m/du.");

        var (spanX, spanY) = MeasurePlanSpan(dxf);
        var longest = System.Math.Max(spanX, spanY);
        notes.Add($"Raw plan span ≈ {spanX:0.##} × {spanY:0.##} drawing-units (header InsUnits={dxf.Header.InsUnits}).");

        var candidates = new (double Scale, string Label)[]
        {
            (0.001, "millimetres"),
            (0.01, "centimetres"),
            (0.0254, "inches"),
            (0.3048, "feet"),
            (1.0, "metres"),
        };

        var headerScale = InsUnitsToMeters(dxf.Header.InsUnits);
        if (headerScale > 0)
        {
            var headerSpan = longest * headerScale;
            // Trust header only when it already looks like a building footprint.
            if (headerSpan is >= 8 and <= 120)
                return (headerScale, $"Header InsUnits → {headerScale} m/du (span {headerSpan:0.#} m).");
        }

        // Score candidates: prefer ~10–60 m longest span (house / small public building).
        (double Scale, string Label, double Span, double Score)? best = null;
        foreach (var (scale, label) in candidates)
        {
            var meters = longest * scale;
            if (meters is < 5 or > 200)
                continue;
            // Distance from a 25 m target; slight penalty outside 10–60.
            var score = System.Math.Abs(meters - 25.0);
            if (meters is < 10 or > 60)
                score += 20;
            if (best is null || score < best.Value.Score)
                best = (scale, label, meters, score);
        }

        if (best is { } hit)
            return (hit.Scale, $"Auto-detected {hit.Label} ({hit.Scale} m/du) → span {hit.Span:0.#} m.");

        return (0.0254, $"Fallback inches (0.0254 m/du); raw span {longest:0.#} du → {longest * 0.0254:0.#} m.");
    }

    private static double InsUnitsToMeters(UnitsType units) => units switch
    {
        UnitsType.Millimeters => 0.001,
        UnitsType.Centimeters => 0.01,
        UnitsType.Meters => 1.0,
        UnitsType.Inches => 0.0254,
        UnitsType.Feet => 0.3048,
        UnitsType.Yards => 0.9144,
        UnitsType.Kilometers => 1000.0,
        UnitsType.Miles => 1609.344,
        _ => 0,
    };

    private static (double SpanX, double SpanY) MeasurePlanSpan(ACadSharp.CadDocument dxf)
    {
        var minX = double.PositiveInfinity;
        var maxX = double.NegativeInfinity;
        var minY = double.PositiveInfinity;
        var maxY = double.NegativeInfinity;
        var any = false;

        void Acc(double x, double y)
        {
            any = true;
            minX = System.Math.Min(minX, x);
            maxX = System.Math.Max(maxX, x);
            minY = System.Math.Min(minY, y);
            maxY = System.Math.Max(maxY, y);
        }

        foreach (var e in dxf.Entities)
        {
            switch (e)
            {
                case Line l:
                    Acc(l.StartPoint.X, l.StartPoint.Y);
                    Acc(l.EndPoint.X, l.EndPoint.Y);
                    break;
                case LwPolyline pl:
                    foreach (var v in pl.Vertices)
                        Acc(v.Location.X, v.Location.Y);
                    break;
            }
        }

        if (!any)
            return (10, 10);
        return (System.Math.Max(0.1, maxX - minX), System.Math.Max(0.1, maxY - minY));
    }

    private static bool TryAddWall(
        CadDocument doc,
        Guid layerId,
        (double X, double Y) a,
        (double X, double Y) b,
        double scale,
        DxfImportOptions options)
    {
        var ax = (float)(a.X * scale);
        var az = (float)(a.Y * scale);
        var bx = (float)(b.X * scale);
        var bz = (float)(b.Y * scale);
        var dx = bx - ax;
        var dz = bz - az;
        var len = System.Math.Sqrt(dx * dx + dz * dz);
        if (len < options.MinWallLengthMeters)
            return false;

        doc.Entities.Add(new CadEntity
        {
            Kind = "wall",
            Name = $"W-{doc.Entities.Count(e => e.Kind == "wall") + 1}",
            Deck = 0,
            A = [ax, 0, az],
            B = [bx, 0, bz],
            Thickness = options.WallThicknessMeters,
            Height = options.WallHeightMeters,
            LayerId = layerId,
        });
        return true;
    }

    private static bool TryAddSpace(
        CadDocument doc,
        Guid layerId,
        IReadOnlyList<(double X, double Y)> pts,
        double scale,
        DxfImportOptions options,
        string layerName)
    {
        if (pts.Count < 3)
            return false;

        var footprint = pts.Select(p => new float[]
        {
            (float)(p.X * scale),
            0f,
            (float)(p.Y * scale),
        }).ToList();

        if (PlanArea(footprint) < 0.05)
            return false;

        doc.Entities.Add(new CadEntity
        {
            Kind = "space",
            Name = InferSpaceName(layerName, doc.Entities.Count(e => e.Kind == "space") + 1),
            Deck = 0,
            Height = options.WallHeightMeters,
            Footprint = footprint,
            LayerId = layerId,
        });
        return true;
    }

    private static bool TryAddOpeningFromSegment(
        CadDocument doc,
        Guid layerId,
        (double X, double Y) a,
        (double X, double Y) b,
        double scale,
        DxfImportOptions options)
    {
        var ax = a.X * scale;
        var az = a.Y * scale;
        var bx = b.X * scale;
        var bz = b.Y * scale;
        var dx = bx - ax;
        var dz = bz - az;
        var len = System.Math.Sqrt(dx * dx + dz * dz);
        if (len < 0.2 || len > 6.0)
            return false;

        var hx = -dz / len * 0.08;
        var hz = dx / len * 0.08;
        var footprint = new List<float[]>
        {
            new[] { (float)(ax + hx), 0f, (float)(az + hz) },
            new[] { (float)(bx + hx), 0f, (float)(bz + hz) },
            new[] { (float)(bx - hx), 0f, (float)(bz - hz) },
            new[] { (float)(ax - hx), 0f, (float)(az - hz) },
        };

        var kind = len >= 1.5 ? "window" : "door";
        doc.Entities.Add(new CadEntity
        {
            Kind = "opening",
            Name = $"{kind}-{doc.Entities.Count(e => e.Kind == "opening") + 1}",
            OpeningType = kind,
            Deck = 0,
            Height = kind is "window" ? 1.2f : 2.1f,
            Thickness = (float)len,
            Footprint = footprint,
            LayerId = layerId,
        });
        return true;
    }

    private static void ApplyTextLabels(ACadSharp.CadDocument dxf, CadDocument doc, double scale)
    {
        var labels = new List<(float X, float Z, string Text)>();
        foreach (var entity in dxf.Entities)
        {
            string? text = entity switch
            {
                TextEntity t => t.Value,
                MText mt => string.IsNullOrWhiteSpace(mt.PlainText) ? mt.Value : mt.PlainText,
                _ => null,
            };
            if (string.IsNullOrWhiteSpace(text))
                continue;

            var clean = CleanLabel(text);
            if (clean.Length is < 2 or > 32)
                continue;

            XYZ? origin = entity switch
            {
                TextEntity t => t.InsertPoint,
                MText mt => mt.InsertPoint,
                _ => null,
            };
            if (origin is null)
                continue;

            labels.Add(((float)(origin.Value.X * scale), (float)(origin.Value.Y * scale), clean));
        }

        foreach (var space in doc.Entities.Where(e => e.Kind == "space"))
        {
            var fp = space.Footprint;
            if (fp is not { Count: >= 3 })
                continue;

            var cx = fp.Average(p => p[0]);
            var cz = fp.Average(p => p[2]);
            string? bestText = null;
            var bestDist = double.MaxValue;
            foreach (var label in labels)
            {
                var ddx = label.X - cx;
                var ddz = label.Z - cz;
                var d = ddx * ddx + ddz * ddz;
                if (d < bestDist)
                {
                    bestDist = d;
                    bestText = label.Text;
                }
            }

            if (bestText is not null && bestDist < 25)
                space.Name = bestText;
        }
    }

    private static string CleanLabel(string raw)
    {
        var s = raw.Replace("\\P", " ", StringComparison.Ordinal)
            .Replace("{", "", StringComparison.Ordinal)
            .Replace("}", "", StringComparison.Ordinal);
        while (true)
        {
            var i = s.IndexOf('\\');
            if (i < 0) break;
            var j = s.IndexOf(';', i);
            if (j < 0)
            {
                s = s[..i];
                break;
            }

            s = s[..i] + s[(j + 1)..];
        }

        return string.Join(' ', s.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    private static string InferSpaceName(string layerName, int index) =>
        layerName.Contains("FOOTPRINT", StringComparison.OrdinalIgnoreCase)
            ? $"Footprint-{index}"
            : $"Space-{index}";

    private static double PlanArea(IReadOnlyList<float[]> ring)
    {
        double a = 0;
        for (var i = 0; i < ring.Count; i++)
        {
            var j = (i + 1) % ring.Count;
            a += ring[i][0] * ring[j][2] - ring[j][0] * ring[i][2];
        }

        return System.Math.Abs(a) * 0.5;
    }

    private static bool Matches(string layerName, IReadOnlyList<string> hints) =>
        hints.Any(h => layerName.Contains(h, StringComparison.OrdinalIgnoreCase));
}

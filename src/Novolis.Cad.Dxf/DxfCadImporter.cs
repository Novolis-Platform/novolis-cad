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
        var skipped = 0;
        var openingCandidates = new List<OpeningCandidate>();

        foreach (var entity in dxf.Entities)
        {
            var layerName = entity.Layer?.Name ?? "";
            if (Matches(layerName, options.ExcludeLayerHints))
            {
                skipped++;
                continue;
            }

            switch (entity)
            {
                case Line line when IsWallLayer(layerName, options):
                {
                    if (TryAddWall(doc, wallLayerId, Xy(line.StartPoint), Xy(line.EndPoint), scale, options))
                        walls++;
                    else
                        skipped++;
                    break;
                }
                case Line line when Matches(layerName, options.OpeningLayerHints):
                {
                    if (TryCollectOpening(openingCandidates, Xy(line.StartPoint), Xy(line.EndPoint), scale, options, layerName))
                    { }
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
                                              && IsWallLayer(layerName, options))))
                    {
                        if (TryAddSpace(doc, spaceLayerId, pts, scale, options, layerName))
                            spaces++;
                        else
                            skipped++;
                    }
                    else if (IsWallLayer(layerName, options))
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
                        if (!TryCollectOpening(openingCandidates, pts[0], pts[^1], scale, options, layerName))
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

        var openings = CommitOpenings(doc, openingLayerId, openingCandidates, options);
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

    private static bool IsWallLayer(string layerName, DxfImportOptions options) =>
        Matches(layerName, options.WallLayerHints)
        && !Matches(layerName, options.OpeningLayerHints)
        && !Matches(layerName, options.SpaceLayerHints);

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

        // Drop near-duplicate wall segments (double-line wall traces).
        foreach (var existing in doc.Entities.Where(e => e.Kind == "wall" && e.A is { Length: >= 3 } && e.B is { Length: >= 3 }))
        {
            var ea = existing.A!;
            var eb = existing.B!;
            if (SameSegment(ax, az, bx, bz, ea[0], ea[2], eb[0], eb[2], 0.12f))
                return false;
        }

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

    private static bool SameSegment(
        float ax, float az, float bx, float bz,
        float cx, float cz, float dx, float dz,
        float tol)
    {
        static float Dist2(float x0, float z0, float x1, float z1)
        {
            var x = x0 - x1;
            var z = z0 - z1;
            return x * x + z * z;
        }

        var tol2 = tol * tol;
        return (Dist2(ax, az, cx, cz) <= tol2 && Dist2(bx, bz, dx, dz) <= tol2)
               || (Dist2(ax, az, dx, dz) <= tol2 && Dist2(bx, bz, cx, cz) <= tol2);
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

        // Ignore tiny annotation loops; keep porch / closet scale (≥ 1 m²).
        if (PlanArea(footprint) < 1.0)
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

    private readonly record struct OpeningCandidate(
        float MidX,
        float MidZ,
        float Ax,
        float Az,
        float Bx,
        float Bz,
        float Len,
        string LayerName);

    private static bool TryCollectOpening(
        List<OpeningCandidate> candidates,
        (double X, double Y) a,
        (double X, double Y) b,
        double scale,
        DxfImportOptions options,
        string layerName)
    {
        var ax = (float)(a.X * scale);
        var az = (float)(a.Y * scale);
        var bx = (float)(b.X * scale);
        var bz = (float)(b.Y * scale);
        var dx = bx - ax;
        var dz = bz - az;
        var len = (float)System.Math.Sqrt(dx * dx + dz * dz);
        if (len < options.MinOpeningLengthMeters || len > options.MaxOpeningLengthMeters)
            return false;

        candidates.Add(new OpeningCandidate(
            (ax + bx) * 0.5f,
            (az + bz) * 0.5f,
            ax, az, bx, bz, len, layerName));
        return true;
    }

    private static int CommitOpenings(
        CadDocument doc,
        Guid layerId,
        List<OpeningCandidate> candidates,
        DxfImportOptions options)
    {
        if (candidates.Count == 0)
            return 0;

        // Prefer the longest segment in each midpoint cluster (jamb soup → one clear).
        var ordered = candidates.OrderByDescending(c => c.Len).ToList();
        var kept = new List<OpeningCandidate>();
        var dedupe2 = options.OpeningDedupeMeters * options.OpeningDedupeMeters;
        foreach (var c in ordered)
        {
            if (kept.Any(k =>
            {
                var dx = k.MidX - c.MidX;
                var dz = k.MidZ - c.MidZ;
                return dx * dx + dz * dz <= dedupe2;
            }))
                continue;
            kept.Add(c);
        }

        var count = 0;
        foreach (var c in kept)
        {
            var kind = InferOpeningKind(c.LayerName, c.Len);
            var hx = -(c.Bz - c.Az) / c.Len * 0.08f;
            var hz = (c.Bx - c.Ax) / c.Len * 0.08f;
            var footprint = new List<float[]>
            {
                new[] { c.Ax + hx, 0f, c.Az + hz },
                new[] { c.Bx + hx, 0f, c.Bz + hz },
                new[] { c.Bx - hx, 0f, c.Bz - hz },
                new[] { c.Ax - hx, 0f, c.Az - hz },
            };

            doc.Entities.Add(new CadEntity
            {
                Kind = "opening",
                Name = $"{kind}-{count + 1}",
                OpeningType = kind,
                Deck = 0,
                Height = kind is "window" ? 1.2f : 2.1f,
                Thickness = c.Len,
                Footprint = footprint,
                LayerId = layerId,
            });
            count++;
        }

        return count;
    }

    private static string InferOpeningKind(string layerName, float len)
    {
        if (layerName.Contains("WINDOW", StringComparison.OrdinalIgnoreCase)
            || layerName.Contains("GLAZ", StringComparison.OrdinalIgnoreCase))
            return "window";
        if (layerName.Contains("DOOR", StringComparison.OrdinalIgnoreCase)
            || layerName.Contains("GARAGE", StringComparison.OrdinalIgnoreCase))
            return "door";
        // Generic OPENING: short → door leaf, mid → window, long → garage/door.
        if (len < 1.05f) return "door";
        if (len < 2.2f) return "window";
        return "door";
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
            if (!IsRoomLabel(clean))
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

            // Only rename when the label sits inside / near the space centroid.
            if (bestText is not null && bestDist < 16)
                space.Name = bestText;
        }
    }

    /// <summary>Keep human room names; drop dims, codes, notes, roof tags.</summary>
    public static bool IsRoomLabel(string text)
    {
        if (text.Length is < 2 or > 28)
            return false;
        if (text.Contains('\'') || text.Contains('"') || text.Contains('′') || text.Contains('″'))
            return false;
        if (LooksLikeDimension(text))
            return false;

        var letters = text.Count(char.IsLetter);
        var digitsOnly = text.Count(char.IsDigit);
        if (letters == 0)
            return false;
        if (digitsOnly >= 3 && letters <= 2)
            return false;

        // Prefer short room names (Kitchen, Master Bath); drop multi-clause notes.
        if (text.Count(c => c == ' ') >= 3 || text.Contains(','))
            return false;

        ReadOnlySpan<string> banned =
        [
            "ROOF", "NOTE", "SCALE", "DETAIL", "SECTION", "ELEV", "PLAN",
            "NORTH", "REV", "SHEET", "DWG", "DRAWING", "TOP OF", "B.O.",
            "BOTTOM", "HEADER", "JOIST", "BEAM", "DIM", "ALSO", "BEARING",
            "SEE ", "TYP.", "TYP ", "CONT.",
        ];
        foreach (var b in banned)
        {
            if (text.Contains(b, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        return true;
    }

    private static bool LooksLikeDimension(string text)
    {
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c is not ('x' or 'X' or '×'))
                continue;
            var leftDigit = false;
            for (var j = 0; j < i; j++)
            {
                if (char.IsDigit(text[j])) { leftDigit = true; break; }
            }

            var rightDigit = false;
            for (var j = i + 1; j < text.Length; j++)
            {
                if (char.IsDigit(text[j])) { rightDigit = true; break; }
            }

            if (leftDigit && rightDigit)
                return true;
        }

        return false;
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

        // AutoCAD special codes: %%u underline, %%d degree, %%c diameter, %%p ±, %%nnn ASCII.
        while (true)
        {
            var i = s.IndexOf("%%", StringComparison.Ordinal);
            if (i < 0) break;
            if (i + 2 >= s.Length)
            {
                s = s[..i];
                break;
            }

            var code = s[i + 2];
            if (code is >= '0' and <= '9')
            {
                var end = i + 3;
                while (end < s.Length && end < i + 5 && char.IsDigit(s[end]))
                    end++;
                s = s[..i] + s[end..];
            }
            else
            {
                s = s[..i] + s[(i + 3)..];
            }
        }

        return string.Join(' ', s.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    private static string InferSpaceName(string layerName, int index) =>
        layerName.Contains("FOOTPRINT", StringComparison.OrdinalIgnoreCase)
            ? $"Area {index}"
            : $"Room {index}";

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

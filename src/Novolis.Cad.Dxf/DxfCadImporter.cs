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

        var spaces = 0;
        var skipped = 0;
        var wallFaces = new List<WallFace>();
        // LINE faces only — LWPOLYLINE wall traces often bridge door/window clears.
        var gapFaces = new List<WallFace>();

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
                    if (TryCollectWallFace(wallFaces, Xy(line.StartPoint), Xy(line.EndPoint), scale, options))
                        TryCollectWallFace(gapFaces, Xy(line.StartPoint), Xy(line.EndPoint), scale, options);
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
                            if (TryCollectWallFace(wallFaces, pts[i], pts[i + 1], scale, options))
                                added++;
                        }

                        if (poly.IsClosed && pts.Count >= 2
                            && TryCollectWallFace(wallFaces, pts[^1], pts[0], scale, options))
                            added++;
                        if (added == 0) skipped++;
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

        var walls = CommitWalls(doc, wallLayerId, wallFaces, options, notes);
        walls = MergeCollinearWalls(doc, notes);
        JoinWallCorners(doc, notes);
        walls = MergeCollinearWalls(doc, notes);
        // OPENING-layer lines are mostly door leaves/swings in the room — not clears.
        // Real openings are gaps in LINE wall-face coverage along each centerline.
        var openings = DetectOpeningsFromFaceGaps(doc, openingLayerId, gapFaces, options, notes);
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

    private readonly record struct WallFace(
        float Ax, float Az, float Bx, float Bz,
        float Len, float Tx, float Tz, float Nx, float Nz, float Mx, float Mz);

    private static bool TryCollectWallFace(
        List<WallFace> faces,
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
        var len = (float)System.Math.Sqrt(dx * dx + dz * dz);
        if (len < options.MinWallLengthMeters)
            return false;

        var tx = dx / len;
        var tz = dz / len;
        faces.Add(new WallFace(
            ax, az, bx, bz, len, tx, tz, -tz, tx, (ax + bx) * 0.5f, (az + bz) * 0.5f));
        return true;
    }

    private static int CommitWalls(
        CadDocument doc,
        Guid layerId,
        List<WallFace> faces,
        DxfImportOptions options,
        List<string> notes)
    {
        if (faces.Count == 0)
            return 0;

        if (!options.PairParallelWallFaces)
        {
            foreach (var f in faces)
                AddWallEntity(doc, layerId, f.Ax, f.Az, f.Bx, f.Bz, options.WallThicknessMeters, options);
            return faces.Count;
        }

        var ordered = faces.OrderByDescending(f => f.Len).ToList();
        var used = new bool[ordered.Count];
        var paired = 0;
        var singles = 0;

        for (var i = 0; i < ordered.Count; i++)
        {
            if (used[i]) continue;
            var a = ordered[i];
            var bestJ = -1;
            var bestSep = float.MaxValue;
            for (var j = i + 1; j < ordered.Count; j++)
            {
                if (used[j]) continue;
                var b = ordered[j];
                if (!TryFacePairSeparation(a, b, options, out var sep))
                    continue;
                if (sep < bestSep)
                {
                    bestSep = sep;
                    bestJ = j;
                }
            }

            if (bestJ >= 0)
            {
                used[i] = true;
                used[bestJ] = true;
                var b = ordered[bestJ];
                // Offset face A toward B by half the measured thickness → centerline.
                var side = (b.Mx - a.Mx) * a.Nx + (b.Mz - a.Mz) * a.Nz;
                var sign = side >= 0 ? 1f : -1f;
                var ox = a.Nx * (bestSep * 0.5f) * sign;
                var oz = a.Nz * (bestSep * 0.5f) * sign;
                AddWallEntity(doc, layerId, a.Ax + ox, a.Az + oz, a.Bx + ox, a.Bz + oz, bestSep, options);
                paired++;
            }
            else if (options.KeepUnpairedWallFaces)
            {
                used[i] = true;
                AddWallEntity(doc, layerId, a.Ax, a.Az, a.Bx, a.Bz, options.WallThicknessMeters, options);
                singles++;
            }
            else
            {
                used[i] = true;
            }
        }

        notes.Add($"Wall faces={faces.Count} → paired={paired} singles={singles}.");
        return paired + singles;
    }

    private static bool TryFacePairSeparation(
        WallFace a,
        WallFace b,
        DxfImportOptions options,
        out float sep)
    {
        sep = 0;
        // Parallel: unit tangents nearly collinear.
        var parallel = System.Math.Abs(a.Tx * b.Tx + a.Tz * b.Tz);
        if (parallel < 0.97f)
            return false;

        sep = System.Math.Abs((b.Mx - a.Mx) * a.Nx + (b.Mz - a.Mz) * a.Nz);
        if (sep < options.MinWallThicknessMeters || sep > options.MaxWallThicknessMeters)
            return false;

        // Require midpoints to overlap along the longer face.
        var along = (b.Mx - a.Mx) * a.Tx + (b.Mz - a.Mz) * a.Tz;
        var half = (a.Len + b.Len) * 0.5f;
        if (System.Math.Abs(along) > half * 0.65f)
            return false;

        // Prefer similar lengths (avoid pairing a long exterior with a short stub).
        var lenRatio = a.Len > b.Len ? b.Len / a.Len : a.Len / b.Len;
        return lenRatio >= 0.25f;
    }

    private static void AddWallEntity(
        CadDocument doc,
        Guid layerId,
        float ax,
        float az,
        float bx,
        float bz,
        float thickness,
        DxfImportOptions options)
    {
        doc.Entities.Add(new CadEntity
        {
            Kind = "wall",
            Name = $"W-{doc.Entities.Count(e => e.Kind == "wall") + 1}",
            Deck = 0,
            A = [ax, 0, az],
            B = [bx, 0, bz],
            Thickness = thickness,
            Height = options.WallHeightMeters,
            LayerId = layerId,
        });
    }

    /// <summary>
    /// Collapse near-collinear overlapping wall runs so door gaps are not
    /// painted over by a twin centerline from a second face pair.
    /// </summary>
    private static int MergeCollinearWalls(CadDocument doc, List<string> notes)
    {
        var walls = doc.Entities.Where(e => e.Kind == "wall" && e.A is { Length: >= 3 } && e.B is { Length: >= 3 })
            .ToList();
        if (walls.Count < 2)
            return walls.Count;

        var remove = new HashSet<CadEntity>();
        var merged = 0;

        for (var i = 0; i < walls.Count; i++)
        {
            if (remove.Contains(walls[i])) continue;
            var a = walls[i];
            var ax = a.A![0];
            var az = a.A[2];
            var bx = a.B![0];
            var bz = a.B[2];
            var adx = bx - ax;
            var adz = bz - az;
            var alen = (float)System.Math.Sqrt(adx * adx + adz * adz);
            if (alen < 1e-4f) continue;
            var atx = adx / alen;
            var atz = adz / alen;
            var anx = -atz;
            var anz = atx;
            var aThick = a.Thickness > 1e-4f ? a.Thickness : 0.15f;

            for (var j = i + 1; j < walls.Count; j++)
            {
                if (remove.Contains(walls[j])) continue;
                var b = walls[j];
                var cx = b.A![0];
                var cz = b.A[2];
                var dx = b.B![0];
                var dz = b.B[2];
                var bdx = dx - cx;
                var bdz = dz - cz;
                var blen = (float)System.Math.Sqrt(bdx * bdx + bdz * bdz);
                if (blen < 1e-4f) continue;
                var btx = bdx / blen;
                var btz = bdz / blen;
                if (System.Math.Abs(atx * btx + atz * btz) < 0.98f)
                    continue;

                var bmx = (cx + dx) * 0.5f;
                var bmz = (cz + dz) * 0.5f;
                var sep = System.Math.Abs((bmx - ax) * anx + (bmz - az) * anz);
                var bThick = b.Thickness > 1e-4f ? b.Thickness : 0.15f;
                if (sep > System.Math.Max(aThick, bThick) * 0.55f + 0.04f)
                    continue;

                // Project both segments onto A's tangent; require overlap.
                float Proj(float x, float z) => (x - ax) * atx + (z - az) * atz;
                var a0 = 0f;
                var a1 = alen;
                var b0 = Proj(cx, cz);
                var b1 = Proj(dx, dz);
                if (b0 > b1) (b0, b1) = (b1, b0);
                var left = System.Math.Max(a0, b0);
                var right = System.Math.Min(a1, b1);
                if (right - left < 0.25f)
                    continue;

                var union0 = System.Math.Min(a0, b0);
                var union1 = System.Math.Max(a1, b1);
                a.A = [ax + atx * union0, 0f, az + atz * union0];
                a.B = [ax + atx * union1, 0f, az + atz * union1];
                a.Thickness = System.Math.Max(aThick, bThick);
                alen = union1 - union0;
                // Keep `a` frame origin at new A for subsequent merges.
                ax = a.A[0];
                az = a.A[2];
                bx = a.B[0];
                bz = a.B[2];
                adx = bx - ax;
                adz = bz - az;
                alen = (float)System.Math.Sqrt(adx * adx + adz * adz);
                atx = adx / alen;
                atz = adz / alen;
                anx = -atz;
                anz = atx;
                aThick = a.Thickness;
                remove.Add(b);
                merged++;
            }
        }

        foreach (var dead in remove)
            doc.Entities.Remove(dead);

        // Renumber surviving walls.
        var n = 0;
        foreach (var w in doc.Entities.Where(e => e.Kind == "wall"))
            w.Name = $"W-{++n}";

        if (merged > 0)
            notes.Add($"Merged {merged} overlapping collinear wall run(s).");
        return doc.Entities.Count(e => e.Kind == "wall");
    }

    /// <summary>
    /// Snap near wall endpoints to line–line intersections so face-pair centerlines
    /// form a connected plan graph instead of floating stubs.
    /// </summary>
    private static void JoinWallCorners(CadDocument doc, List<string> notes)
    {
        const float maxExtend = 0.75f;
        const float nearEnd = 0.75f;
        var walls = doc.Entities
            .Where(e => e.Kind == "wall" && e.A is { Length: >= 3 } && e.B is { Length: >= 3 })
            .ToList();
        var joins = 0;

        for (var i = 0; i < walls.Count; i++)
        {
            for (var j = i + 1; j < walls.Count; j++)
            {
                var a = walls[i];
                var b = walls[j];
                if (!TryWallIntersection(a, b, maxExtend, out var ix, out var iz, out var hitA, out var hitB))
                    continue;

                if (hitA)
                {
                    if (EndpointDist(a.A!, ix, iz) <= nearEnd)
                    {
                        a.A = [ix, 0f, iz];
                        joins++;
                    }
                    else if (EndpointDist(a.B!, ix, iz) <= nearEnd)
                    {
                        a.B = [ix, 0f, iz];
                        joins++;
                    }
                }

                if (hitB)
                {
                    if (EndpointDist(b.A!, ix, iz) <= nearEnd)
                    {
                        b.A = [ix, 0f, iz];
                        joins++;
                    }
                    else if (EndpointDist(b.B!, ix, iz) <= nearEnd)
                    {
                        b.B = [ix, 0f, iz];
                        joins++;
                    }
                }
            }
        }

        var welds = WeldNearbyEndpoints(walls, 0.28f);
        var tees = SnapDanglingToWalls(walls, 0.40f);
        if (joins > 0 || welds > 0 || tees > 0)
            notes.Add($"Joined wall corners: intersects={joins} endpoint-welds={welds} T-snaps={tees}.");
    }

    /// <summary>Project remaining dangling endpoints onto the nearest crossing wall (T-junction).</summary>
    private static int SnapDanglingToWalls(List<CadEntity> walls, float maxDist)
    {
        // Degree-1 endpoints only.
        var ends = new List<(CadEntity Wall, bool IsA, float X, float Z)>();
        foreach (var w in walls)
        {
            ends.Add((w, true, w.A![0], w.A[2]));
            ends.Add((w, false, w.B![0], w.B[2]));
        }

        var snaps = 0;
        var tol2 = 0.05f * 0.05f;
        for (var i = 0; i < ends.Count; i++)
        {
            var mates = 0;
            for (var j = 0; j < ends.Count; j++)
            {
                if (i == j) continue;
                var dx = ends[i].X - ends[j].X;
                var dz = ends[i].Z - ends[j].Z;
                if (dx * dx + dz * dz <= tol2)
                    mates++;
            }

            if (mates > 0)
                continue; // already joined

            var e = ends[i];
            CadEntity? bestWall = null;
            var bestD = maxDist;
            float bestX = 0, bestZ = 0;
            foreach (var w in walls)
            {
                if (ReferenceEquals(w, e.Wall)) continue;
                var ax = w.A![0];
                var az = w.A[2];
                var bx = w.B![0];
                var bz = w.B[2];
                var wdx = bx - ax;
                var wdz = bz - az;
                var wlen = (float)System.Math.Sqrt(wdx * wdx + wdz * wdz);
                if (wlen < 1e-4f) continue;
                var tx = wdx / wlen;
                var tz = wdz / wlen;
                var along = (e.X - ax) * tx + (e.Z - az) * tz;
                if (along < -0.05f || along > wlen + 0.05f) continue;
                along = System.Math.Clamp(along, 0f, wlen);
                var px = ax + tx * along;
                var pz = az + tz * along;
                var d = EndpointDist([e.X, 0, e.Z], px, pz);
                if (d < bestD)
                {
                    bestD = d;
                    bestWall = w;
                    bestX = px;
                    bestZ = pz;
                }
            }

            if (bestWall is null)
                continue;
            if (e.IsA) e.Wall.A = [bestX, 0f, bestZ];
            else e.Wall.B = [bestX, 0f, bestZ];
            snaps++;
        }

        return snaps;
    }

    private static bool TryWallIntersection(
        CadEntity a,
        CadEntity b,
        float maxExtend,
        out float ix,
        out float iz,
        out bool hitA,
        out bool hitB)
    {
        ix = iz = 0;
        hitA = hitB = false;
        var ax = a.A![0];
        var az = a.A[2];
        var bx = a.B![0];
        var bz = a.B[2];
        var cx = b.A![0];
        var cz = b.A[2];
        var dx = b.B![0];
        var dz = b.B[2];
        var adx = bx - ax;
        var adz = bz - az;
        var bdx = dx - cx;
        var bdz = dz - cz;
        var alen = (float)System.Math.Sqrt(adx * adx + adz * adz);
        var blen = (float)System.Math.Sqrt(bdx * bdx + bdz * bdz);
        if (alen < 1e-4f || blen < 1e-4f)
            return false;

        var atx = adx / alen;
        var atz = adz / alen;
        var btx = bdx / blen;
        var btz = bdz / blen;
        if (System.Math.Abs(atx * btx + atz * btz) > 0.97f)
            return false; // parallel

        // Solve ax + at * s = cx + bt * t
        var den = atx * btz - atz * btx;
        if (System.Math.Abs(den) < 1e-6f)
            return false;
        var rx = cx - ax;
        var rz = cz - az;
        var s = (rx * btz - rz * btx) / den;
        var t = (rx * atz - rz * atx) / den;
        if (s < -maxExtend || s > alen + maxExtend || t < -maxExtend || t > blen + maxExtend)
            return false;

        ix = ax + atx * s;
        iz = az + atz * s;
        hitA = EndpointDist(a.A!, ix, iz) <= maxExtend || EndpointDist(a.B!, ix, iz) <= maxExtend;
        hitB = EndpointDist(b.A!, ix, iz) <= maxExtend || EndpointDist(b.B!, ix, iz) <= maxExtend;
        return hitA || hitB;
    }

    private static float EndpointDist(float[] p, float x, float z)
    {
        var dx = p[0] - x;
        var dz = p[2] - z;
        return (float)System.Math.Sqrt(dx * dx + dz * dz);
    }

    private static int WeldNearbyEndpoints(List<CadEntity> walls, float tol)
    {
        var ends = new List<(CadEntity Wall, bool IsA, float X, float Z)>();
        foreach (var w in walls)
        {
            ends.Add((w, true, w.A![0], w.A[2]));
            ends.Add((w, false, w.B![0], w.B[2]));
        }

        var welds = 0;
        var used = new bool[ends.Count];
        var tol2 = tol * tol;
        for (var i = 0; i < ends.Count; i++)
        {
            if (used[i]) continue;
            var cluster = new List<int> { i };
            for (var j = i + 1; j < ends.Count; j++)
            {
                if (used[j]) continue;
                var dx = ends[i].X - ends[j].X;
                var dz = ends[i].Z - ends[j].Z;
                if (dx * dx + dz * dz <= tol2)
                    cluster.Add(j);
            }

            if (cluster.Count < 2)
                continue;

            var cx = cluster.Average(k => ends[k].X);
            var cz = cluster.Average(k => ends[k].Z);
            foreach (var k in cluster)
            {
                used[k] = true;
                var e = ends[k];
                if (e.IsA) e.Wall.A = [cx, 0f, cz];
                else e.Wall.B = [cx, 0f, cz];
                welds++;
            }
        }

        return welds;
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

    /// <summary>
    /// Openings = uncovered spans along each wall centerline where parallel wall faces
    /// leave a door/window-sized gap (not OPENING-layer swing strokes in the room).
    /// </summary>
    private static int DetectOpeningsFromFaceGaps(
        CadDocument doc,
        Guid layerId,
        List<WallFace> faces,
        DxfImportOptions options,
        List<string> notes)
    {
        var count = 0;
        var walls = doc.Entities
            .Where(e => e.Kind == "wall" && e.A is { Length: >= 3 } && e.B is { Length: >= 3 })
            .ToList();

        foreach (var wall in walls)
        {
            var ax = wall.A![0];
            var az = wall.A[2];
            var bx = wall.B![0];
            var bz = wall.B[2];
            var wdx = bx - ax;
            var wdz = bz - az;
            var wlen = (float)System.Math.Sqrt(wdx * wdx + wdz * wdz);
            if (wlen < options.MinWallLengthMeters)
                continue;

            var tx = wdx / wlen;
            var tz = wdz / wlen;
            var nx = -tz;
            var nz = tx;
            var thick = wall.Thickness > 1e-4f ? wall.Thickness : options.WallThicknessMeters;
            var cover = new List<(float T0, float T1)>();

            foreach (var face in faces)
            {
                if (System.Math.Abs(face.Tx * tx + face.Tz * tz) < 0.95f)
                    continue;
                // After corner joins the centerline can drift — keep a looser poche band.
                var lat = System.Math.Abs((face.Mx - ax) * nx + (face.Mz - az) * nz);
                if (lat > thick * 0.5f + 0.25f)
                    continue;

                float Proj(float x, float z) => (x - ax) * tx + (z - az) * tz;
                var t0 = Proj(face.Ax, face.Az);
                var t1 = Proj(face.Bx, face.Bz);
                if (t0 > t1) (t0, t1) = (t1, t0);
                t0 = System.Math.Clamp(t0, 0f, wlen);
                t1 = System.Math.Clamp(t1, 0f, wlen);
                if (t1 - t0 < 0.05f)
                    continue;
                cover.Add((t0, t1));
            }

            if (cover.Count == 0)
                continue;

            cover.Sort((a, b) => a.T0.CompareTo(b.T0));
            var merged = new List<(float T0, float T1)>();
            foreach (var c in cover)
            {
                // Tight merge — do not bridge door/window clears.
                if (merged.Count > 0 && c.T0 <= merged[^1].T1 + 0.03f)
                    merged[^1] = (merged[^1].T0, System.Math.Max(merged[^1].T1, c.T1));
                else
                    merged.Add(c);
            }

            var cursor = 0f;
            foreach (var (t0, t1) in merged)
            {
                count += TryEmitFaceGap(
                    doc, layerId, wall, ax, az, tx, tz, nx, nz, thick, wlen, cursor, t0, options);
                cursor = System.Math.Max(cursor, t1);
            }

            count += TryEmitFaceGap(
                doc, layerId, wall, ax, az, tx, tz, nx, nz, thick, wlen, cursor, wlen, options);
        }

        notes.Add($"Detected {count} opening(s) from wall-face gaps.");
        return count;
    }

    private static int TryEmitFaceGap(
        CadDocument doc,
        Guid layerId,
        CadEntity wall,
        float ax,
        float az,
        float tx,
        float tz,
        float nx,
        float nz,
        float thick,
        float wlen,
        float g0,
        float g1,
        DxfImportOptions options)
    {
        var glen = g1 - g0;
        if (glen < options.MinOpeningLengthMeters || glen > options.MaxOpeningLengthMeters)
            return 0;
        // Corner pairing noise at wall ends — ignore short end gaps.
        if ((g0 < 0.15f || g1 > wlen - 0.15f) && glen < 0.85f)
            return 0;

        var ox0 = ax + tx * g0;
        var oz0 = az + tz * g0;
        var ox1 = ax + tx * g1;
        var oz1 = az + tz * g1;
        var halfD = System.Math.Max(thick * 0.55f, 0.06f);
        var kind = glen >= 1.15f ? "window" : "door";
        var n = doc.Entities.Count(e => e.Kind == "opening") + 1;
        doc.Entities.Add(new CadEntity
        {
            Kind = "opening",
            Name = $"{kind}-{n}",
            OpeningType = kind,
            Deck = 0,
            Height = kind is "window" ? 1.2f : 2.1f,
            Thickness = glen,
            Footprint =
            [
                [ox0 + nx * halfD, 0f, oz0 + nz * halfD],
                [ox1 + nx * halfD, 0f, oz1 + nz * halfD],
                [ox1 - nx * halfD, 0f, oz1 - nz * halfD],
                [ox0 - nx * halfD, 0f, oz0 - nz * halfD],
            ],
            HostWallId = wall.Id,
            LayerId = layerId,
        });
        return 1;
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

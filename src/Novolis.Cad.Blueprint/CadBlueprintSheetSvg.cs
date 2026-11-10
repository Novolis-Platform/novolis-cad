using System.Globalization;
using Novolis.Markup.Svg;

namespace Novolis.Cad.Blueprint;

/// <summary>
/// Builds a typed <see cref="SvgDocument"/> for one blueprint sheet —
/// single source of truth for HTML embed, standalone SVG, and PDF.
/// </summary>
public static class CadBlueprintSheetSvg
{
    private static readonly SvgPaint Ink = SvgPaint.Color("#000");
    private static readonly SvgPaint Paper = SvgPaint.Color("#fff");
    private static readonly SvgPaint White = SvgPaint.Color("#fff");
    private static readonly SvgPaint Muted = SvgPaint.Color("#333");
    private static readonly SvgPaint DimInk = SvgPaint.Color("#111");
    private static readonly SvgPaint WallFill = SvgPaint.Color("#111");
    private static readonly SvgPaint ShellStroke = SvgPaint.Color("#666");
    private static readonly SvgPaint SpaceFill = SvgPaint.None;
    private static readonly SvgPaint WindowStroke = SvgPaint.Color("#000");
    private static readonly SvgPaint GlassStroke = SvgPaint.Color("#333");
    private static readonly SvgPaint LabelMuted = SvgPaint.Color("#333");
    private const string Mono = "IBM Plex Mono,Consolas,monospace";

    public static SvgDocument Compose(CadBlueprint blueprint, CadBlueprintSheet sheet)
    {
        ArgumentNullException.ThrowIfNull(blueprint);
        ArgumentNullException.ThrowIfNull(sheet);

        var layout = CadBlueprintSheetLayout.Compute(blueprint, sheet);
        var size = layout.Size;
        var w = size.WidthMm;
        var h = size.HeightMm;
        var isShip = IsShipContext(blueprint.Context);

        var doc = SvgMarkup.Document(svg =>
        {
            svg.Id(CadBlueprintDom.SheetElementId)
                .Width(w)
                .Height(h)
                .ViewBox(0, 0, w, h)
                .Role("img")
                .AriaLabel(sheet.Title)
                .Data("nbp-format", CadBlueprintDom.FormatValue);

            AppendChrome(svg, blueprint, sheet, layout, isShip);

            var views = sheet.Views.Count > 0
                ? sheet.Views
                : [new CadBlueprintView { Id = "plan", Label = "Plan", Kind = "plan" }];

            foreach (var view in views)
            {
                var kind = string.IsNullOrEmpty(view.Kind) ? "plan" : view.Kind;
                svg.Group(g =>
                {
                    g.Data("nbp-view", view.Id).Data("nbp-view-kind", kind);
                    if (kind is "plan" or "other")
                    {
                        AppendPlan(g, blueprint, sheet, view.LevelIndex, layout);
                        AppendStrokes(g, sheet, view, layout, planOverlays: true);
                    }
                    else if (kind is "section" or "elevation" or "profile" or "detail")
                    {
                        AppendStrokes(g, sheet, view, layout, planOverlays: false);
                    }
                });
            }
        });

        return doc;
    }

    public static string ToMarkup(CadBlueprint blueprint, CadBlueprintSheet sheet) =>
        Compose(blueprint, sheet).ToString();

    private static void AppendChrome(
        SvgDocument svg,
        CadBlueprint blueprint,
        CadBlueprintSheet sheet,
        CadBlueprintSheetLayout layout,
        bool isShip)
    {
        var size = layout.Size;
        var w = size.WidthMm;
        var h = size.HeightMm;
        var frame = sheet.Layers.FirstOrDefault(l => l.Id is "chrome.frame" || l.Kind is "chrome");

        svg.Group(g =>
        {
            LayerMeta(g, frame?.Id ?? "chrome.frame", frame?.Path ?? "chrome/frame", "chrome", plot: true);
            g.Rect(0, 0, w, h, r => r.Fill(Paper).Stroke(SvgPaint.None));

            var ix = size.BorderLeftMm;
            var iy = size.BorderMm;
            var iw = w - size.BorderLeftMm - size.BorderMm;
            var ih = h - 2 * size.BorderMm;
            g.Rect(ix, iy, iw, ih, r => r.Fill(SvgPaint.None).Stroke(Ink).StrokeWidth(0.9));
            g.Rect(ix + 3, iy + 3, iw - 6, ih - 6, r => r.Fill(SvgPaint.None).Stroke(Ink).StrokeWidth(0.35));

            g.Text(new SvgPoint(ix + 12, iy + 18), sheet.Title, t =>
                t.Fill(Ink).FontFamily(Mono).FontSize(SvgLength.Number(11)).Style("font-weight:600"));

            var primaryKind = sheet.Views.FirstOrDefault()?.Kind ?? "plan";
            var orientNote = layout.SternLeft
                ? "ISO 128-15 stern left"
                : primaryKind switch
                {
                    "section" => "section",
                    "elevation" => "elevation",
                    "profile" => "profile",
                    _ => "plan north up",
                };
            g.Text(
                new SvgPoint(ix + 12, iy + 30),
                $"{blueprint.Context} · {orientNote} · {sheet.Units} · SCALE 1:{F(layout.ScaleRatio)}",
                t => t.Fill(Ink).FontFamily(Mono).FontSize(SvgLength.Number(5.5)));

            if (!layout.SternLeft && primaryKind is "plan" or "other" or "")
            {
                g.Group(n =>
                {
                    n.Transform(new SvgTransformList().Translate(ix + iw - 36, iy + 36));
                    n.Polygon(
                        [new SvgPoint(0, -14), new SvgPoint(5, 8), new SvgPoint(-5, 8)],
                        p => p.Fill(Ink).Stroke(SvgPaint.None));
                    n.Text(new SvgPoint(0, 18), "N", t =>
                        t.Fill(Ink).FontFamily(Mono).FontSize(SvgLength.Number(6)).TextAnchor(SvgTextAnchor.Middle));
                });
            }

            var note = isShip
                ? "CLASS-STYLE GA COMPANION (novolis.cad.blueprint) — NOT FOR PLAN APPROVAL. ISO 128-15 / 5457 / 7200."
                : "ARCHITECTURAL GA COMPANION (novolis.cad.blueprint) — NOT FOR BUILDING PERMIT. ISO 5457 / 7200 sheet chrome.";
            g.Text(new SvgPoint(ix + 12, h - size.BorderMm - 10), note, t =>
                t.Fill(Muted).FontFamily(Mono).FontSize(SvgLength.Number(4)));

            AppendIso7200(g, sheet, blueprint, layout);
        });
    }

    private static void AppendIso7200(
        SvgGroup g,
        CadBlueprintSheet sheet,
        CadBlueprint blueprint,
        CadBlueprintSheetLayout layout)
    {
        const double tw = 220;
        const double th = 78;
        var size = layout.Size;
        var x = size.WidthMm - size.BorderMm - tw - 4;
        var y = size.HeightMm - size.BorderMm - th - 4;

        g.Group(tb =>
        {
            tb.Class("titleblock").Data("nbp-iso", "7200");
            tb.Rect(x, y, tw, th, r => r.Fill(White).Stroke(Ink).StrokeWidth(0.9));

            var c1 = x + 64;
            var c2 = x + 148;
            tb.Line(c1, y, c1, y + th, l => l.Stroke(Ink).StrokeWidth(0.45));
            tb.Line(c2, y, c2, y + th, l => l.Stroke(Ink).StrokeWidth(0.45));
            var r1 = y + 18;
            var r2 = y + 38;
            var r3 = y + 58;
            tb.Line(x, r1, x + tw, r1, l => l.Stroke(Ink).StrokeWidth(0.45));
            tb.Line(x, r2, x + tw, r2, l => l.Stroke(Ink).StrokeWidth(0.45));
            tb.Line(x, r3, x + tw, r3, l => l.Stroke(Ink).StrokeWidth(0.45));

            void Lbl(double lx, double ly, string text) =>
                tb.Text(new SvgPoint(lx, ly), text, t =>
                    t.Fill(LabelMuted).FontFamily(Mono).FontSize(SvgLength.Number(3.6)));
            void Val(double lx, double ly, string text, double sizePt = 5.5) =>
                tb.Text(new SvgPoint(lx, ly), text, t =>
                {
                    t.Fill(Ink).FontFamily(Mono).FontSize(SvgLength.Number(sizePt));
                    if (sizePt >= 6.5)
                        t.Style("font-weight:600");
                });

            Lbl(x + 3, y + 7, "OWNER / LEGAL");
            Val(x + 3, y + 14, "Novolis");
            Lbl(c1 + 3, y + 7, "DRAWING TITLE");
            Val(c1 + 3, y + 14, Truncate(sheet.Title, 22), 5);
            Lbl(c2 + 3, y + 7, "SHEET");
            Val(c2 + 3, y + 14, $"{size.Size} {(size.Landscape ? "LANDSCAPE" : "PORTRAIT")}", 5);

            Lbl(x + 3, r1 + 7, "PROJECT");
            Val(x + 3, r1 + 14, Truncate(blueprint.Name, 14), 5);
            Lbl(c1 + 3, r1 + 7, "DRAWING NO.");
            Val(c1 + 3, r1 + 14, sheet.Id, 7.5);
            Lbl(c2 + 3, r1 + 7, "REV");
            Val(c2 + 3, r1 + 14, sheet.Rev ?? "A", 7);

            Lbl(x + 3, r2 + 7, "CONTEXT");
            Val(x + 3, r2 + 14, blueprint.Context);
            Lbl(c1 + 3, r2 + 7, "SCALE");
            Val(c1 + 3, r2 + 14, $"1:{F(layout.ScaleRatio)}");
            Lbl(c2 + 3, r2 + 7, "UNITS");
            Val(c2 + 3, r2 + 14, sheet.Units);

            Lbl(x + 3, r3 + 7, "CREATED");
            Val(x + 3, r3 + 14, Truncate(blueprint.CreatedAt ?? "", 16));
            Lbl(c1 + 3, r3 + 7, "STANDARD");
            Val(c1 + 3, r3 + 14, "ISO 5457 / 7200");
            Lbl(c2 + 3, r3 + 7, "STATUS");
            Val(c2 + 3, r3 + 14, "PRELIMINARY");
        });
    }

    private static void AppendPlan(
        SvgGroup root,
        CadBlueprint blueprint,
        CadBlueprintSheet sheet,
        int? levelIndex,
        CadBlueprintSheetLayout layout)
    {
        var hasSpaces = blueprint.Spaces.Any(s =>
            (!levelIndex.HasValue || s.LevelIndex == levelIndex) && s.Footprint is { Count: >= 3 });

        root.Group(g =>
        {
            ResolveLayer(g, sheet, "exterior.shell", "exterior/shell", "outline");
            foreach (var shell in blueprint.Shells)
            {
                if (shell.PlanRing is not { Count: >= 3 }) continue;
                g.Path(RingPath(shell.PlanRing, layout), p =>
                    p.Fill(SvgPaint.None).Stroke(ShellStroke).StrokeWidth(0.45).StrokeDashArray([2, 1.2]));
                if (!hasSpaces && !string.IsNullOrWhiteSpace(shell.Name))
                    RoomLabel(g, shell.PlanRing, layout, shell.Name!);
            }
        });

        root.Group(g =>
        {
            ResolveLayer(g, sheet, "interior.spaces", "interior/spaces", "space");
            foreach (var space in blueprint.Spaces)
            {
                if (levelIndex is { } li && space.LevelIndex != li) continue;
                if (space.Footprint is not { Count: >= 3 }) continue;
                g.Path(RingPath(space.Footprint, layout), p => p.Fill(SpaceFill).Stroke(SvgPaint.None));
                if (ShouldPlotRoomLabel(space.Name))
                    RoomLabel(g, space.Footprint, layout, space.Name);
            }
        });

        root.Group(g =>
        {
            ResolveLayer(g, sheet, "structure.walls", "structure/walls", "structure");
            foreach (var wall in blueprint.Walls)
            {
                if (levelIndex is { } li && wall.LevelIndex != li) continue;
                if (wall.A is null || wall.B is null) continue;
                CadBlueprintSheetLayout.PlanXZ(wall.A, out var ax, out var az);
                CadBlueprintSheetLayout.PlanXZ(wall.B, out var bx, out var bz);
                var thick = wall.Thickness > 1e-4f ? wall.Thickness : 0.15f;
                AppendWallWithOpeningGaps(
                    g, ax, az, bx, bz, thick, wall.Id, blueprint, levelIndex, layout);
            }
        });

        root.Group(g =>
        {
            ResolveLayer(g, sheet, "openings.all", "openings/all", "opening");
            var planCx = (float)((layout.MinX + layout.MaxX) * 0.5);
            var planCz = (float)((layout.MinZ + layout.MaxZ) * 0.5);
            foreach (var opening in blueprint.Openings)
            {
                if (levelIndex is { } li && opening.LevelIndex != li) continue;
                if (!TryOpeningAxis(opening, blueprint, out var cx, out var cz, out var tx, out var tz, out var halfW, out var hostThick))
                    continue;
                var kind = (opening.Kind ?? "door").ToLowerInvariant();
                if (kind is "window")
                    AppendWindow(g, cx, cz, tx, tz, halfW, hostThick, layout);
                else if (kind is "hatch" or "hole")
                    AppendHole(g, cx, cz, tx, tz, halfW, hostThick, layout);
                else
                    AppendDoor(g, cx, cz, tx, tz, halfW, hostThick, planCx, planCz, layout);
            }
        });

        root.Group(g =>
        {
            ResolveLayer(g, sheet, "dims.primary", "dims/primary", "dimension");
            AppendOverallDims(g, layout);
        });
    }

    private static void AppendWallWithOpeningGaps(
        SvgGroup g,
        float ax,
        float az,
        float bx,
        float bz,
        float thickness,
        Guid wallId,
        CadBlueprint blueprint,
        int? levelIndex,
        CadBlueprintSheetLayout layout)
    {
        var dx = bx - ax;
        var dz = bz - az;
        var len = (float)System.Math.Sqrt(dx * dx + dz * dz);
        if (len < 1e-6f) return;

        var gaps = new List<(float T0, float T1)>();
        foreach (var opening in blueprint.Openings)
        {
            if (levelIndex is { } li && opening.LevelIndex != li) continue;
            if (opening.HostWallId != wallId) continue;
            if (!TryOpeningAxis(opening, blueprint, out var cx, out var cz, out _, out _, out var halfW, out _))
                continue;

            var tx = dx / len;
            var tz = dz / len;
            var along = (cx - ax) * tx + (cz - az) * tz;
            var pad = 0.02f;
            var t0 = (along - halfW - pad) / len;
            var t1 = (along + halfW + pad) / len;
            if (t1 <= 0.001f || t0 >= 0.999f) continue;
            gaps.Add((System.Math.Clamp(t0, 0f, 1f), System.Math.Clamp(t1, 0f, 1f)));
        }

        if (gaps.Count == 0)
        {
            AppendThickWall(g, ax, az, bx, bz, thickness, layout);
            return;
        }

        gaps.Sort((a, b) => a.T0.CompareTo(b.T0));
        var merged = new List<(float T0, float T1)>();
        foreach (var gap in gaps)
        {
            if (merged.Count > 0 && gap.T0 <= merged[^1].T1 + 0.001f)
                merged[^1] = (merged[^1].T0, System.Math.Max(merged[^1].T1, gap.T1));
            else
                merged.Add(gap);
        }

        var cursor = 0f;
        foreach (var (t0, t1) in merged)
        {
            if (t0 - cursor > 0.01f)
                AppendThickWall(g, ax + dx * cursor, az + dz * cursor, ax + dx * t0, az + dz * t0, thickness, layout);
            cursor = System.Math.Max(cursor, t1);
        }

        if (1f - cursor > 0.01f)
            AppendThickWall(g, ax + dx * cursor, az + dz * cursor, bx, bz, thickness, layout);
    }

    private static void AppendThickWall(
        SvgGroup g,
        float ax,
        float az,
        float bx,
        float bz,
        float thickness,
        CadBlueprintSheetLayout layout)
    {
        var dx = bx - ax;
        var dz = bz - az;
        var len = System.Math.Sqrt(dx * dx + dz * dz);
        if (len < 1e-6) return;
        var hx = (float)(-dz / len * (thickness * 0.5));
        var hz = (float)(dx / len * (thickness * 0.5));
        var path = new SvgPathData()
            .MoveTo(P(ax + hx, az + hz, layout))
            .LineTo(P(bx + hx, bz + hz, layout))
            .LineTo(P(bx - hx, bz - hz, layout))
            .LineTo(P(ax - hx, az - hz, layout))
            .Close();
        g.Path(path, p => p.Fill(WallFill).Stroke(Ink).StrokeWidth(0.25));
    }

    private static void AppendDoor(
        SvgGroup g,
        float cx,
        float cz,
        float tx,
        float tz,
        float halfW,
        float hostThick,
        float planCx,
        float planCz,
        CadBlueprintSheetLayout layout)
    {
        // Swing toward plan centre so exterior doors open inward by default.
        var nx = -tz;
        var nz = tx;
        if ((planCx - cx) * nx + (planCz - cz) * nz < 0)
        {
            nx = -nx;
            nz = -nz;
        }

        var clear = halfW * 2;
        var hingeX = cx - tx * halfW;
        var hingeZ = cz - tz * halfW;
        var latchX = cx + tx * halfW;
        var latchZ = cz + tz * halfW;
        var leafEndX = hingeX + nx * clear;
        var leafEndZ = hingeZ + nz * clear;

        // Jamb ticks (door stop marks) at the clear edges.
        var j = System.Math.Max(hostThick * 0.55f, 0.06f);
        g.Line(P(hingeX + nx * j, hingeZ + nz * j, layout), P(hingeX - nx * j, hingeZ - nz * j, layout), l =>
            l.Stroke(Ink).StrokeWidth(0.45).Fill(SvgPaint.None));
        g.Line(P(latchX + nx * j, latchZ + nz * j, layout), P(latchX - nx * j, latchZ - nz * j, layout), l =>
            l.Stroke(Ink).StrokeWidth(0.45).Fill(SvgPaint.None));

        g.Line(P(hingeX, hingeZ, layout), P(leafEndX, leafEndZ, layout), l =>
            l.Stroke(Ink).StrokeWidth(0.9).Fill(SvgPaint.None));

        var pageHinge = P(hingeX, hingeZ, layout);
        var pageLatch = P(latchX, latchZ, layout);
        var pageLeaf = P(leafEndX, leafEndZ, layout);
        var radius = Dist(pageHinge, pageLatch);
        if (radius < 0.5) radius = Dist(pageHinge, pageLeaf);
        var sweep = ArcSweepMinor(pageHinge, pageLeaf, pageLatch);
        var swing = new SvgPathData()
            .MoveTo(pageLeaf)
            .ArcTo(radius, radius, 0, false, sweep, pageLatch);
        g.Path(swing, p =>
            p.Fill(SvgPaint.None).Stroke(Ink).StrokeWidth(0.35));
    }

    /// <summary>Pick SVG sweep so the minor arc from <paramref name="from"/> to <paramref name="to"/> is used.</summary>
    private static bool ArcSweepMinor(SvgPoint center, SvgPoint from, SvgPoint to)
    {
        var fx = from.X - center.X;
        var fy = from.Y - center.Y;
        var tx = to.X - center.X;
        var ty = to.Y - center.Y;
        // SVG y-down: sweep=1 is clockwise. Positive cross (fx,fy)×(tx,ty) is CCW in y-down? 
        // In y-down, (1,0)×(0,1)=+1 points "into the page" same as y-up math for x-right y-down...
        // Clockwise from +X toward +Y: cross of +X with +Y = 1, and clockwise needs sweep=1.
        var cross = fx * ty - fy * tx;
        return cross > 0;
    }

    private static void AppendWindow(
        SvgGroup g,
        float cx,
        float cz,
        float tx,
        float tz,
        float halfW,
        float hostThick,
        CadBlueprintSheetLayout layout)
    {
        var nx = -tz;
        var nz = tx;
        var aX = cx - tx * halfW;
        var aZ = cz - tz * halfW;
        var bX = cx + tx * halfW;
        var bZ = cz + tz * halfW;
        var t = hostThick * 0.35f;
        g.Line(P(aX + nx * t, aZ + nz * t, layout), P(bX + nx * t, bZ + nz * t, layout), l =>
            l.Stroke(WindowStroke).StrokeWidth(0.45));
        g.Line(P(aX - nx * t, aZ - nz * t, layout), P(bX - nx * t, bZ - nz * t, layout), l =>
            l.Stroke(WindowStroke).StrokeWidth(0.45));
        g.Line(P(aX, aZ, layout), P(bX, bZ, layout), l =>
            l.Stroke(GlassStroke).StrokeWidth(0.4));
    }

    private static void AppendHole(
        SvgGroup g,
        float cx,
        float cz,
        float tx,
        float tz,
        float halfW,
        float hostThick,
        CadBlueprintSheetLayout layout)
    {
        var nx = -tz;
        var nz = tx;
        var aX = cx - tx * halfW;
        var aZ = cz - tz * halfW;
        var bX = cx + tx * halfW;
        var bZ = cz + tz * halfW;
        var punch = hostThick * 0.55f + 0.02f;
        var path = new SvgPathData()
            .MoveTo(P(aX + nx * punch, aZ + nz * punch, layout))
            .LineTo(P(bX + nx * punch, bZ + nz * punch, layout))
            .LineTo(P(bX - nx * punch, bZ - nz * punch, layout))
            .LineTo(P(aX - nx * punch, aZ - nz * punch, layout))
            .Close();
        g.Path(path, p =>
            p.Fill(Paper).Stroke(ShellStroke).StrokeWidth(0.25).StrokeDashArray([1, 0.6]));
    }

    private static void AppendOverallDims(SvgGroup g, CadBlueprintSheetLayout layout)
    {
        if (layout.SpanX <= 0.05 || layout.SpanY <= 0.05) return;

        var x0 = layout.SternLeft ? layout.OriginX + layout.MinZ * layout.Scale : layout.OriginX + layout.MinX * layout.Scale;
        var x1 = layout.SternLeft ? layout.OriginX + layout.MaxZ * layout.Scale : layout.OriginX + layout.MaxX * layout.Scale;
        var y0 = layout.SternLeft ? layout.OriginY - layout.MaxX * layout.Scale : layout.OriginY - layout.MaxZ * layout.Scale;
        var y1 = layout.SternLeft ? layout.OriginY - layout.MinX * layout.Scale : layout.OriginY - layout.MinZ * layout.Scale;
        var longSpan = layout.SternLeft ? layout.SpanY : layout.SpanX;
        var athwartSpan = layout.SternLeft ? layout.SpanX : layout.SpanY;
        var minX = System.Math.Min(x0, x1);
        var maxX = System.Math.Max(x0, x1);
        var minY = System.Math.Min(y0, y1);
        var maxY = System.Math.Max(y0, y1);
        var dimY = maxY + 14;
        var dimX = maxX + 14;

        void DimLine(double ax, double ay, double bx, double by) =>
            g.Line(ax, ay, bx, by, l => l.Stroke(DimInk).StrokeWidth(0.22));
        void Tick(double x, double y) =>
            g.Line(x - 1.2, y - 1.2, x + 1.2, y + 1.2, l => l.Stroke(DimInk).StrokeWidth(0.35));

        DimLine(minX, maxY + 2, minX, dimY);
        DimLine(maxX, maxY + 2, maxX, dimY);
        DimLine(minX, dimY, maxX, dimY);
        Tick(minX, dimY);
        Tick(maxX, dimY);
        g.Text(new SvgPoint((minX + maxX) * 0.5, dimY + 8), $"{Nice(longSpan)} m", t =>
            t.Fill(Ink).FontFamily(Mono).FontSize(SvgLength.Number(5)).TextAnchor(SvgTextAnchor.Middle));

        DimLine(maxX + 2, minY, dimX, minY);
        DimLine(maxX + 2, maxY, dimX, maxY);
        DimLine(dimX, minY, dimX, maxY);
        Tick(dimX, minY);
        Tick(dimX, maxY);
        g.Text(new SvgPoint(dimX + 6, (minY + maxY) * 0.5), $"{Nice(athwartSpan)} m", t =>
            t.Fill(Ink).FontFamily(Mono).FontSize(SvgLength.Number(5)));
    }

    private static bool TryOpeningAxis(
        CadBlueprintOpening opening,
        CadBlueprint blueprint,
        out float cx,
        out float cz,
        out float tx,
        out float tz,
        out float halfW,
        out float hostThick)
    {
        cx = cz = tx = tz = halfW = 0;
        hostThick = 0.15f;

        // Prefer host wall tangent — footprint AABB alone forces axis-alignment and
        // makes door leaves / arcs skew off the wall.
        if (opening.HostWallId is { } hid)
        {
            var wall = blueprint.Walls.FirstOrDefault(w => w.Id == hid);
            if (wall is { A: { Length: >= 3 }, B: { Length: >= 3 } })
            {
                CadBlueprintSheetLayout.PlanXZ(wall.A, out var ax, out var az);
                CadBlueprintSheetLayout.PlanXZ(wall.B, out var bx, out var bz);
                var wdx = bx - ax;
                var wdz = bz - az;
                var wlen = (float)System.Math.Sqrt(wdx * wdx + wdz * wdz);
                if (wlen > 1e-4f)
                {
                    tx = wdx / wlen;
                    tz = wdz / wlen;
                    hostThick = wall.Thickness > 1e-4f ? wall.Thickness : 0.15f;

                    float midX, midZ;
                    if (opening.Footprint is { Count: >= 2 })
                    {
                        midX = opening.Footprint.Average(p => p[0]);
                        midZ = opening.Footprint.Average(p => p.Length > 2 ? p[2] : p[1]);
                    }
                    else
                    {
                        midX = (ax + bx) * 0.5f;
                        midZ = (az + bz) * 0.5f;
                    }

                    var along = (midX - ax) * tx + (midZ - az) * tz;
                    along = System.Math.Clamp(along, 0f, wlen);
                    cx = ax + tx * along;
                    cz = az + tz * along;

                    halfW = opening.ClearWidth > 1e-3f
                        ? opening.ClearWidth * 0.5f
                        : EstimateHalfWidthFromFootprint(opening, tx, tz);
                    return halfW > 1e-4f;
                }
            }
        }

        if (opening.Footprint is not { Count: >= 2 })
            return false;

        var minX = opening.Footprint.Min(p => p[0]);
        var maxX = opening.Footprint.Max(p => p[0]);
        var minZ = opening.Footprint.Min(p => p.Length > 2 ? p[2] : p[1]);
        var maxZ = opening.Footprint.Max(p => p.Length > 2 ? p[2] : p[1]);
        cx = (minX + maxX) * 0.5f;
        cz = (minZ + maxZ) * 0.5f;
        var sx = maxX - minX;
        var sz = maxZ - minZ;
        if (sx >= sz)
        {
            tx = 1;
            tz = 0;
            halfW = opening.ClearWidth > 1e-3f ? opening.ClearWidth * 0.5f : sx * 0.5f;
            if (hostThick < sz && sz > 1e-3f) hostThick = sz;
        }
        else
        {
            tx = 0;
            tz = 1;
            halfW = opening.ClearWidth > 1e-3f ? opening.ClearWidth * 0.5f : sz * 0.5f;
            if (hostThick < sx && sx > 1e-3f) hostThick = sx;
        }

        return halfW > 1e-4f;
    }

    private static float EstimateHalfWidthFromFootprint(CadBlueprintOpening opening, float tx, float tz)
    {
        if (opening.Footprint is not { Count: >= 2 })
            return 0.4f;
        var min = float.PositiveInfinity;
        var max = float.NegativeInfinity;
        foreach (var p in opening.Footprint)
        {
            var x = p[0];
            var z = p.Length > 2 ? p[2] : p[1];
            var along = x * tx + z * tz;
            if (along < min) min = along;
            if (along > max) max = along;
        }

        return System.Math.Max(0.2f, (max - min) * 0.5f);
    }

    private static SvgPathData RingPath(IReadOnlyList<float[]> ring, CadBlueprintSheetLayout layout)
    {
        var path = new SvgPathData();
        for (var i = 0; i < ring.Count; i++)
        {
            CadBlueprintSheetLayout.PlanXZ(ring[i], out var x, out var z);
            var pt = P(x, z, layout);
            if (i == 0) path.MoveTo(pt);
            else path.LineTo(pt);
        }

        return path.Close();
    }

    private static void AppendStrokes(
        SvgGroup root,
        CadBlueprintSheet sheet,
        CadBlueprintView view,
        CadBlueprintSheetLayout layout,
        bool planOverlays)
    {
        if (view.Strokes.Count == 0 && view.Texts.Count == 0)
            return;

        if (planOverlays)
        {
            AppendStrokeBucket(
                root, sheet, view,
                "overlays.furniture", "overlays/furniture", "overlay",
                s => LayerLooksLike(s.LayerHint, ["FURN", "FIXT", "CASE", "FURNITURE"]),
                layout);
            AppendStrokeBucket(
                root, sheet, view,
                "overlays.dims", "overlays/dims", "overlay",
                s => LayerLooksLike(s.LayerHint, ["DIM", "ANNOT", "TEXT", "NOTE"]),
                layout);
            AppendStrokeBucket(
                root, sheet, view,
                "overlays.grid", "overlays/grid", "overlay",
                s => LayerLooksLike(s.LayerHint, ["GRID", "AXIS"]),
                layout);
            AppendStrokeBucket(
                root, sheet, view,
                "overlays.drawing", "overlays/drawing", "overlay",
                s => !LayerLooksLike(s.LayerHint, ["FURN", "FIXT", "CASE", "FURNITURE", "DIM", "ANNOT", "TEXT", "NOTE", "GRID", "AXIS"])
                     && !LayerLooksLike(s.LayerHint, ["WALL", "A-WALL", "DOOR", "GLAZ", "WINDOW", "OPENING"]),
                layout);
            // Skip wall/opening stroke duplicates on plan — semantic poche already draws them.
        }
        else
        {
            root.Group(g =>
            {
                ResolveLayer(g, sheet, "drawing.strokes", "drawing/strokes", "drawing");
                foreach (var stroke in view.Strokes)
                    DrawStroke(g, stroke, layout);
            });

            if (view.Texts.Count > 0)
            {
                root.Group(g =>
                {
                    ResolveLayer(g, sheet, "drawing.texts", "drawing/texts", "drawing");
                    foreach (var text in view.Texts)
                        DrawViewText(g, text, layout);
                });
            }
        }
    }

    private static void AppendStrokeBucket(
        SvgGroup root,
        CadBlueprintSheet sheet,
        CadBlueprintView view,
        string layerId,
        string layerPath,
        string kind,
        Func<CadBlueprintStroke, bool> predicate,
        CadBlueprintSheetLayout layout)
    {
        var strokes = view.Strokes.Where(predicate).ToList();
        var texts = layerId.Contains("dims", StringComparison.Ordinal)
            ? view.Texts
            : [];
        if (strokes.Count == 0 && texts.Count == 0)
            return;

        root.Group(g =>
        {
            ResolveLayer(g, sheet, layerId, layerPath, kind);
            foreach (var stroke in strokes)
                DrawStroke(g, stroke, layout);
            foreach (var text in texts)
                DrawViewText(g, text, layout);
        });
    }

    private static bool LayerLooksLike(string? layerHint, IReadOnlyList<string> hints)
    {
        if (string.IsNullOrWhiteSpace(layerHint))
            return false;
        foreach (var hint in hints)
        {
            if (layerHint.Contains(hint, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static void DrawStroke(SvgGroup g, CadBlueprintStroke stroke, CadBlueprintSheetLayout layout)
    {
        if (stroke.Points.Count < 2)
            return;

        var path = new SvgPathData();
        CadBlueprintSheetLayout.PlanXZ(stroke.Points[0], out var x0, out var z0);
        path.MoveTo(new SvgPoint(layout.PageX(x0, z0), layout.PageY(x0, z0)));
        for (var i = 1; i < stroke.Points.Count; i++)
        {
            CadBlueprintSheetLayout.PlanXZ(stroke.Points[i], out var x, out var z);
            path.LineTo(new SvgPoint(layout.PageX(x, z), layout.PageY(x, z)));
        }

        if (stroke.Closed)
            path.Close();

        var (paint, width, dashed) = StrokeStyle(stroke.Style);
        g.Path(path, p =>
        {
            p.Fill(SvgPaint.None).Stroke(paint).StrokeWidth(width);
            if (dashed)
                p.StrokeDashArray([1.2, 0.8]);
        });
    }

    private static (SvgPaint Paint, double Width, bool Dashed) StrokeStyle(string style) =>
        style.ToLowerInvariant() switch
        {
            "heavy" => (Ink, 0.55, false),
            "dim" => (DimInk, 0.22, false),
            "dashed" => (Muted, 0.28, true),
            _ => (Ink, 0.35, false),
        };

    private static void DrawViewText(SvgGroup g, CadBlueprintViewText text, CadBlueprintSheetLayout layout)
    {
        if (string.IsNullOrWhiteSpace(text.Text))
            return;
        CadBlueprintSheetLayout.PlanXZ(text.Position, out var x, out var z);
        var fontMm = System.Math.Clamp(text.Height * layout.Scale, 2.5, 8.0);
        g.Text(new SvgPoint(layout.PageX(x, z), layout.PageY(x, z)), text.Text, t =>
            t.Fill(Muted).FontFamily(Mono).FontSize(SvgLength.Number(fontMm)));
    }

    private static void RoomLabel(
        SvgGroup g,
        IReadOnlyList<float[]> ring,
        CadBlueprintSheetLayout layout,
        string name)
    {
        double sx = 0, sy = 0;
        foreach (var p in ring)
        {
            CadBlueprintSheetLayout.PlanXZ(p, out var x, out var z);
            sx += layout.PageX(x, z);
            sy += layout.PageY(x, z);
        }

        g.Text(new SvgPoint(sx / ring.Count, sy / ring.Count), name, t =>
            t.Fill(Ink).FontFamily(Mono).FontSize(SvgLength.Number(7.5)).TextAnchor(SvgTextAnchor.Middle)
                .Style("font-weight:600"));
    }

    private static bool ShouldPlotRoomLabel(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return false;
        // Generic Area/Room placeholders stay quiet on dense DXF lifts.
        if (name.StartsWith("Area ", StringComparison.Ordinal)
            || name.StartsWith("Room ", StringComparison.Ordinal)
            || name.StartsWith("Footprint", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("Space-", StringComparison.OrdinalIgnoreCase))
            return false;
        if (name.Contains('\'') || name.Contains('"'))
            return false;
        if (name.Count(char.IsDigit) >= 3 && name.Count(char.IsLetter) <= 2)
            return false;
        return true;
    }

    private static void ResolveLayer(
        SvgGroup g,
        CadBlueprintSheet sheet,
        string fallbackId,
        string fallbackPath,
        string kind)
    {
        var layer = sheet.Layers.FirstOrDefault(l =>
            string.Equals(l.Id, fallbackId, StringComparison.Ordinal)
            || string.Equals(l.Kind, kind, StringComparison.OrdinalIgnoreCase)
            || l.Path.Contains(kind, StringComparison.OrdinalIgnoreCase));
        LayerMeta(g, layer?.Id ?? fallbackId, layer?.Path ?? fallbackPath, kind, layer?.Plot ?? true);
    }

    private static void LayerMeta(SvgGroup g, string id, string path, string kind, bool plot) =>
        g.Data("nbp-layer", id)
            .Data("nbp-path", path)
            .Data("nbp-kind", kind)
            .Data("nbp-plot", plot ? "true" : "false");

    private static SvgPoint P(float x, float z, CadBlueprintSheetLayout layout) =>
        new(layout.PageX(x, z), layout.PageY(x, z));

    private static double Dist(SvgPoint a, SvgPoint b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return System.Math.Sqrt(dx * dx + dy * dy);
    }

    private static bool IsShipContext(string context) =>
        context is CadBlueprintContexts.SeagoingShip
            or CadBlueprintContexts.Spaceship
            or CadBlueprintContexts.SpaceStation;

    private static string Truncate(string s, int max) =>
        string.IsNullOrEmpty(s) ? "" : s.Length <= max ? s : s[..(max - 1)] + "…";

    private static string Nice(double v) =>
        v.ToString(System.Math.Abs(v) >= 10 ? "0.0" : "0.##", CultureInfo.InvariantCulture);

    private static string F(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);
}

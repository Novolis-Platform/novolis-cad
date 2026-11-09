using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using Novolis.Cad.Primitives;

namespace Novolis.Cad.Blueprint;

/// <summary>
/// Emits smart HTML5 sheets per <see cref="CadBlueprintDom"/> —
/// CSS <c>:has()</c> layer toggles, embedded JSON manifest, plan SVG.
/// </summary>
public static class CadBlueprintHtmlExporter
{
    public static string EmitSheet(CadBlueprint blueprint, CadBlueprintSheet sheet)
    {
        ArgumentNullException.ThrowIfNull(blueprint);
        ArgumentNullException.ThrowIfNull(sheet);

        var size = sheet.Sheet ?? CadBlueprintSheetSize.A1Landscape();
        var w = size.WidthMm;
        var h = size.HeightMm;
        var sternLeft = sheet.Orientation.Contains("stern-left", StringComparison.OrdinalIgnoreCase);

        var bounds = CollectBounds(blueprint, sheet, sternLeft);
        var pad = 36.0;
        var contentW = w - size.BorderLeftMm - size.BorderMm - 2 * pad;
        var contentH = h - 2 * size.BorderMm - 2 * pad - 40;
        // Page axes: north-up uses X/Z; stern-left uses Z/X.
        var pageSpanX = sternLeft ? bounds.SpanY : bounds.SpanX;
        var pageSpanY = sternLeft ? bounds.SpanX : bounds.SpanY;
        var scale = pageSpanX > 1e-6 && pageSpanY > 1e-6
            ? System.Math.Min(contentW / pageSpanX, contentH / pageSpanY) * 0.92
            : 1.0;
        var ox = sternLeft
            ? size.BorderLeftMm + pad + (contentW - pageSpanX * scale) * 0.5 - bounds.MinZ * scale
            : size.BorderLeftMm + pad + (contentW - pageSpanX * scale) * 0.5 - bounds.MinX * scale;
        var oy = sternLeft
            ? size.BorderMm + pad + 28 + (contentH - pageSpanY * scale) * 0.5 + bounds.MaxX * scale
            : size.BorderMm + pad + 28 + (contentH - pageSpanY * scale) * 0.5 + bounds.MaxZ * scale;

        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine(
            $"<html lang=\"en\" {CadBlueprintDom.FormatAttr}=\"{CadBlueprintDom.FormatValue}\" {CadBlueprintDom.SchemaAttr}=\"{blueprint.SchemaVersion}\">");
        sb.AppendLine("<head><meta charset=\"utf-8\"/>");
        sb.AppendLine($"<title>{Esc(sheet.Title)}</title>");
        AppendStyles(sb, sheet);
        sb.AppendLine("</head><body>");

        // Sheet first — drafting document is the SVG; layer UI is optional chrome below.
        sb.AppendLine("<main id=\"nbp-stage\">");
        sb.AppendLine(
            $"<svg id=\"{CadBlueprintDom.SheetElementId}\" xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 {F(w)} {F(h)}\" width=\"100%\" role=\"img\" aria-label=\"{Esc(sheet.Title)}\">");

        // Chrome frame
        var frame = sheet.Layers.FirstOrDefault(l => l.Id is "chrome.frame" || l.Kind is "chrome");
        var frameId = frame?.Id ?? "chrome.frame";
        var framePath = frame?.Path ?? "chrome/frame";
        sb.AppendLine(
            $"<g {CadBlueprintDom.LayerAttr}=\"{Esc(frameId)}\" {CadBlueprintDom.PathAttr}=\"{Esc(framePath)}\" {CadBlueprintDom.KindAttr}=\"chrome\" {CadBlueprintDom.PlotAttr}=\"true\">");
        sb.AppendLine($"<rect x=\"0\" y=\"0\" width=\"{F(w)}\" height=\"{F(h)}\" fill=\"#f7f4ea\"/>");
        sb.AppendLine(
            $"<rect x=\"{F(size.BorderLeftMm)}\" y=\"{F(size.BorderMm)}\" width=\"{F(w - size.BorderLeftMm - size.BorderMm)}\" height=\"{F(h - 2 * size.BorderMm)}\" fill=\"none\" stroke=\"#111\" stroke-width=\"0.7\"/>");
        sb.AppendLine(
            $"<text x=\"{F(size.BorderLeftMm + 8)}\" y=\"{F(size.BorderMm + 16)}\" class=\"room\">{Esc(sheet.Title)}</text>");
        sb.AppendLine(
            $"<text x=\"{F(size.BorderLeftMm + 8)}\" y=\"{F(size.BorderMm + 28)}\" class=\"dimt\">{Esc(blueprint.Context)} · {Esc(sheet.Orientation)} · {Esc(sheet.Units)} · SCALE AS SHOWN</text>");
        sb.AppendLine(
            $"<text x=\"{F(size.BorderLeftMm + 8)}\" y=\"{F(h - size.BorderMm - 6)}\" class=\"classnote\">CLASS-STYLE GA COMPANION (novolis.cad.blueprint) — NOT DNV PLAN APPROVAL. ISO 128-15 stern left when orientation says so.</text>");
        // Mini ISO 7200 block
        var tbX = w - size.BorderMm - 160;
        var tbY = h - size.BorderMm - 48;
        sb.AppendLine($"<rect x=\"{F(tbX)}\" y=\"{F(tbY)}\" width=\"160\" height=\"42\" fill=\"#fff\" stroke=\"#111\" stroke-width=\"0.7\"/>");
        sb.AppendLine($"<text x=\"{F(tbX + 4)}\" y=\"{F(tbY + 10)}\" class=\"dimt\">DRAWING</text>");
        sb.AppendLine($"<text x=\"{F(tbX + 4)}\" y=\"{F(tbY + 20)}\" class=\"room\">{Esc(sheet.Id)}</text>");
        sb.AppendLine($"<text x=\"{F(tbX + 4)}\" y=\"{F(tbY + 32)}\" class=\"dimt\">REV {Esc(sheet.Rev ?? "B")} · A1</text>");
        sb.AppendLine("</g>");

        var views = sheet.Views.Count > 0
            ? sheet.Views
            : [new CadBlueprintView { Id = "plan", Label = "Plan", Kind = "plan" }];

        foreach (var view in views.Where(v => v.Kind is "plan" or "other" || string.IsNullOrEmpty(v.Kind)))
        {
            sb.AppendLine(
                $"<g {CadBlueprintDom.ViewAttr}=\"{Esc(view.Id)}\" data-nbp-view-kind=\"{Esc(view.Kind)}\">");
            AppendPlanGeometry(sb, blueprint, sheet, view.LevelIndex, sternLeft, ox, oy, scale);
            sb.AppendLine("</g>");
        }

        sb.AppendLine("</svg></main>");

        if (sheet.ShowUi)
            AppendUi(sb, blueprint, sheet);

        var manifest = new
        {
            format = blueprint.Format,
            schemaVersion = blueprint.SchemaVersion,
            name = blueprint.Name,
            context = blueprint.Context,
            sheets = new[] { sheet },
        };
        sb.AppendLine($"<script type=\"application/json\" id=\"{CadBlueprintDom.ManifestElementId}\">");
        sb.AppendLine(JsonSerializer.Serialize(manifest, CadJson.Options));
        sb.AppendLine("</script>");
        sb.AppendLine("</body></html>");
        return sb.ToString();
    }

    public static void WriteSheet(CadBlueprint blueprint, CadBlueprintSheet sheet, string path)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        File.WriteAllText(path, EmitSheet(blueprint, sheet));
    }

    private static void AppendStyles(StringBuilder sb, CadBlueprintSheet sheet)
    {
        sb.AppendLine("<style>");
        sb.AppendLine("""
            :root { --nbp-sheet:#fff; --nbp-ink:#111; --nbp-accent:#5a9fd4; }
            @page { size: 841mm 594mm; margin: 0; }
            html, body { margin:0; padding:0; background:#fff; color:#111; font:13px/1.35 system-ui,sans-serif; }
            #nbp-stage { margin:0; padding:0; }
            #nbp-sheet { display:block; width:100%; height:auto; background:var(--nbp-sheet); }
            #nbp-ui { max-width:841px; margin:10px auto 20px; padding:0 12px; }
            #nbp-ui details { border:1px solid #ddd; padding:8px 12px; background:#fafafa; }
            #nbp-ui summary { cursor:pointer; font-weight:600; }
            #nbp-ui fieldset { border:1px solid #ccc; margin:8px 0; padding:8px 10px; }
            #nbp-ui legend { padding:0 4px; color:var(--nbp-accent); font-size:11px; }
            #nbp-ui label { display:flex; gap:8px; align-items:center; margin:4px 0; cursor:pointer; }
            #nbp-ui label.locked { opacity:0.65; cursor:default; }
            .ol { fill:#f4f6f8; stroke:var(--nbp-ink); stroke-width:1.05; }
            .space { fill:#fff; stroke:#111; stroke-width:0.45; }
            .wall { stroke:#222; stroke-width:0.9; fill:none; }
            .opening { fill:#dce8f5; stroke:#1a4a7a; stroke-width:0.45; }
            .dim { stroke:#222; stroke-width:0.25; fill:none; }
            .dimt { font:3px "IBM Plex Mono",Consolas,monospace; fill:#111; }
            .room { font:3.2px "IBM Plex Mono",Consolas,monospace; fill:#111; }
            .classnote { font:2.8px "IBM Plex Mono",Consolas,monospace; fill:#444; }
            @media print {
              #nbp-ui { display:none !important; }
              #nbp-sheet { width:841mm; height:594mm; }
              g[data-nbp-plot="false"] { display:none !important; }
            }
            """);

        foreach (var layer in sheet.Layers.Where(l => !l.Locked))
        {
            var tid = ToggleId(layer.Id);
            sb.AppendLine(
                $"body:has(#{tid}:not(:checked)) g[{CadBlueprintDom.LayerAttr}=\"{Esc(layer.Id)}\"] {{ display:none; }}");
        }

        sb.AppendLine("</style>");
    }

    private static void AppendUi(StringBuilder sb, CadBlueprint blueprint, CadBlueprintSheet sheet)
    {
        sb.AppendLine($"<aside id=\"{CadBlueprintDom.UiElementId}\" aria-label=\"Blueprint layers\">");
        sb.AppendLine("<details>");
        sb.AppendLine($"<summary>Layers · {Esc(sheet.Title)} · {Esc(blueprint.Context)}</summary>");
        sb.AppendLine("<form id=\"nbp-layer-form\">");

        var byFolder = sheet.Folders.Count > 0
            ? sheet.Folders
            : [new CadBlueprintFolder { Path = "layers", Label = "Layers" }];

        foreach (var folder in byFolder)
        {
            var layers = sheet.Layers.Where(l =>
                l.Path.StartsWith(folder.Path, StringComparison.Ordinal)
                || (folder.Path.Length > 0 && l.Path.StartsWith(folder.Path + "/", StringComparison.Ordinal))
                || string.Equals(l.Path.Split('/')[0], folder.Path, StringComparison.Ordinal)).ToList();
            if (layers.Count == 0 && folder.Path is not "chrome")
                continue;

            sb.AppendLine("<fieldset>");
            sb.AppendLine($"<legend>{Esc(folder.Label)}</legend>");
            foreach (var layer in layers.Count > 0 ? layers : sheet.Layers.Where(l => l.Kind is "chrome"))
            {
                var tid = ToggleId(layer.Id);
                if (layer.Locked)
                {
                    sb.AppendLine(
                        $"<label class=\"locked\"><input type=\"checkbox\" checked disabled/> {Esc(layer.Label)}</label>");
                }
                else
                {
                    var check = layer.DefaultVisible ? " checked" : "";
                    sb.AppendLine(
                        $"<label><input id=\"{tid}\" type=\"checkbox\"{check}/> {Esc(layer.Label)}</label>");
                }
            }

            sb.AppendLine("</fieldset>");
        }

        // Orphan layers not matched to a folder
        var claimed = byFolder.SelectMany(f => sheet.Layers.Where(l =>
            l.Path.StartsWith(f.Path, StringComparison.Ordinal)
            || string.Equals(l.Path.Split('/')[0], f.Path, StringComparison.Ordinal))).Select(l => l.Id).ToHashSet();
        var orphans = sheet.Layers.Where(l => !claimed.Contains(l.Id) && l.Kind is not "chrome").ToList();
        if (orphans.Count > 0)
        {
            sb.AppendLine("<fieldset><legend>other</legend>");
            foreach (var layer in orphans)
            {
                var tid = ToggleId(layer.Id);
                var check = layer.DefaultVisible ? " checked" : "";
                sb.AppendLine(
                    $"<label><input id=\"{tid}\" type=\"checkbox\"{check}/> {Esc(layer.Label)}</label>");
            }

            sb.AppendLine("</fieldset>");
        }

        sb.AppendLine("</form>");
        sb.AppendLine("<p class=\"meta\">Toggles use CSS <code>:has()</code>. Print hides UI and non-plot layers.</p>");
        sb.AppendLine("</details></aside>");
    }

    private static void AppendPlanGeometry(
        StringBuilder sb,
        CadBlueprint blueprint,
        CadBlueprintSheet sheet,
        int? levelIndex,
        bool sternLeft,
        double ox,
        double oy,
        double scale)
    {
        string LayerAttrs(string fallbackId, string fallbackPath, string kind)
        {
            var layer = sheet.Layers.FirstOrDefault(l =>
                string.Equals(l.Id, fallbackId, StringComparison.Ordinal)
                || string.Equals(l.Kind, kind, StringComparison.OrdinalIgnoreCase)
                || l.Path.Contains(kind, StringComparison.OrdinalIgnoreCase));
            var id = layer?.Id ?? fallbackId;
            var path = layer?.Path ?? fallbackPath;
            var plot = layer?.Plot ?? true;
            return $"{CadBlueprintDom.LayerAttr}=\"{Esc(id)}\" {CadBlueprintDom.PathAttr}=\"{Esc(path)}\" {CadBlueprintDom.KindAttr}=\"{Esc(kind)}\" {CadBlueprintDom.PlotAttr}=\"{(plot ? "true" : "false")}\"";
        }

        // plan-north-up: page X∝x, Y∝−z. iso128-15-stern-left: page X∝z (bow +), Y∝−x.
        double SX(float x, float z) => sternLeft ? ox + z * scale : ox + x * scale;
        double SY(float x, float z) => sternLeft ? oy - x * scale : oy - z * scale;

        // Shells
        sb.AppendLine($"<g {LayerAttrs("exterior.shell", "exterior/shell", "outline")}>");
        foreach (var shell in blueprint.Shells)
        {
            if (shell.PlanRing is not { Count: >= 3 })
                continue;
            sb.AppendLine($"<path d=\"{RingPath(shell.PlanRing, SX, SY)}\" class=\"ol\"/>");
            sb.AppendLine(
                $"<text x=\"{F(CentroidX(shell.PlanRing, SX, SY))}\" y=\"{F(CentroidY(shell.PlanRing, SX, SY))}\" class=\"room\" text-anchor=\"middle\">{Esc(shell.Name)}</text>");
        }

        sb.AppendLine("</g>");

        // Walls
        sb.AppendLine($"<g {LayerAttrs("structure.walls", "structure/walls", "structure")}>");
        foreach (var wall in blueprint.Walls)
        {
            if (levelIndex is { } li && wall.LevelIndex != li)
                continue;
            if (wall.A is null || wall.B is null || wall.A.Length < 1 || wall.B.Length < 1)
                continue;
            PlanXZ(wall.A, out var ax, out var az);
            PlanXZ(wall.B, out var bx, out var bz);
            sb.AppendLine(
                $"<line x1=\"{F(SX(ax, az))}\" y1=\"{F(SY(ax, az))}\" x2=\"{F(SX(bx, bz))}\" y2=\"{F(SY(bx, bz))}\" class=\"wall\"/>");
        }

        sb.AppendLine("</g>");

        // Spaces
        sb.AppendLine($"<g {LayerAttrs("interior.spaces", "interior/spaces", "space")}>");
        foreach (var space in blueprint.Spaces)
        {
            if (levelIndex is { } li && space.LevelIndex != li)
                continue;
            if (space.Footprint is not { Count: >= 3 })
                continue;
            sb.AppendLine($"<path d=\"{RingPath(space.Footprint, SX, SY)}\" class=\"space\"/>");
            sb.AppendLine(
                $"<text x=\"{F(CentroidX(space.Footprint, SX, SY))}\" y=\"{F(CentroidY(space.Footprint, SX, SY))}\" class=\"room\" text-anchor=\"middle\">{Esc(space.Name)}</text>");
        }

        sb.AppendLine("</g>");

        // Openings
        sb.AppendLine($"<g {LayerAttrs("openings.all", "openings/all", "opening")}>");
        foreach (var opening in blueprint.Openings)
        {
            if (levelIndex is { } li && opening.LevelIndex != li)
                continue;
            if (opening.Footprint is not { Count: >= 3 })
                continue;
            sb.AppendLine($"<path d=\"{RingPath(opening.Footprint, SX, SY)}\" class=\"opening\"/>");
        }

        sb.AppendLine("</g>");

        // Primary dims from overall extents
        sb.AppendLine($"<g {LayerAttrs("dims.primary", "dims/primary", "dimension")}>");
        var b = CollectBounds(blueprint, sheet, sternLeft);
        if (b.SpanX > 0.05 && b.SpanY > 0.05)
        {
            var x0 = sternLeft ? ox + b.MinZ * scale : ox + b.MinX * scale;
            var x1 = sternLeft ? ox + b.MaxZ * scale : ox + b.MaxX * scale;
            var y0 = sternLeft ? oy - b.MaxX * scale : oy - b.MaxZ * scale;
            var y1 = sternLeft ? oy - b.MinX * scale : oy - b.MinZ * scale;
            var longSpan = sternLeft ? b.SpanY : b.SpanX;
            var athwartSpan = sternLeft ? b.SpanX : b.SpanY;
            var dimY = System.Math.Max(y0, y1) + 10;
            var dimX = System.Math.Max(x0, x1) + 10;
            sb.AppendLine($"<line x1=\"{F(System.Math.Min(x0, x1))}\" y1=\"{F(dimY)}\" x2=\"{F(System.Math.Max(x0, x1))}\" y2=\"{F(dimY)}\" class=\"dim\"/>");
            sb.AppendLine(
                $"<text x=\"{F((x0 + x1) * 0.5)}\" y=\"{F(dimY + 8)}\" class=\"dimt\" text-anchor=\"middle\">{Nice(longSpan)} m</text>");
            sb.AppendLine($"<line x1=\"{F(dimX)}\" y1=\"{F(System.Math.Min(y0, y1))}\" x2=\"{F(dimX)}\" y2=\"{F(System.Math.Max(y0, y1))}\" class=\"dim\"/>");
            sb.AppendLine(
                $"<text x=\"{F(dimX + 4)}\" y=\"{F((y0 + y1) * 0.5)}\" class=\"dimt\">{Nice(athwartSpan)} m</text>");
        }

        sb.AppendLine("</g>");
    }

    private static string RingPath(
        IReadOnlyList<float[]> ring,
        Func<float, float, double> sx,
        Func<float, float, double> sy)
    {
        var sb = new StringBuilder("M");
        for (var i = 0; i < ring.Count; i++)
        {
            PlanXZ(ring[i], out var x, out var z);
            sb.Append(i == 0 ? " " : " L ");
            sb.Append($"{F(sx(x, z))} {F(sy(x, z))}");
        }

        sb.Append(" Z");
        return sb.ToString();
    }

    private static double CentroidX(
        IReadOnlyList<float[]> ring,
        Func<float, float, double> sx,
        Func<float, float, double> sy)
    {
        double s = 0;
        foreach (var p in ring)
        {
            PlanXZ(p, out var x, out var z);
            s += sx(x, z);
        }

        return s / ring.Count;
    }

    private static double CentroidY(
        IReadOnlyList<float[]> ring,
        Func<float, float, double> sx,
        Func<float, float, double> sy)
    {
        double s = 0;
        foreach (var p in ring)
        {
            PlanXZ(p, out var x, out var z);
            s += sy(x, z);
        }

        return s / ring.Count;
    }

    private static void PlanXZ(float[] p, out float x, out float z)
    {
        x = p.Length > 0 ? p[0] : 0;
        z = p.Length > 2 ? p[2] : p.Length > 1 ? p[1] : 0;
    }

    private sealed record Bounds(double MinX, double MaxX, double MinZ, double MaxZ)
    {
        public double SpanX => System.Math.Max(0.2, MaxX - MinX);
        public double SpanY => System.Math.Max(0.2, MaxZ - MinZ);
    }

    private static Bounds CollectBounds(CadBlueprint blueprint, CadBlueprintSheet sheet, bool sternLeft)
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

        _ = sheet;
        _ = sternLeft;
        if (!any)
            return new Bounds(0, 10, 0, 10);
        return new Bounds(minX, maxX, minZ, maxZ);
    }

    private static string ToggleId(string layerId)
    {
        var chars = layerId.Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray();
        return "t-" + new string(chars);
    }

    private static string Nice(double v) =>
        v.ToString(System.Math.Abs(v) >= 10 ? "0.0" : "0.##", CultureInfo.InvariantCulture);

    private static string F(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);

    private static string Esc(string s) => WebUtility.HtmlEncode(s);
}

using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using Novolis.Cad.Primitives;

namespace Novolis.Cad.Blueprint;

/// <summary>
/// Emits smart HTML5 sheets per <see cref="CadBlueprintDom"/> —
/// CSS <c>:has()</c> layer toggles, embedded JSON manifest, plan SVG with
/// ISO 5457 / 7200 chrome and architectural plan symbology.
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
        var isShip = IsShipContext(blueprint.Context);

        var bounds = CollectBounds(blueprint, sheet);
        var pad = 28.0;
        var titleBlockW = 180.0;
        var titleBlockH = 56.0;
        var contentW = w - size.BorderLeftMm - size.BorderMm - 2 * pad - 8;
        var contentH = h - 2 * size.BorderMm - 2 * pad - titleBlockH - 12;
        var pageSpanX = sternLeft ? bounds.SpanY : bounds.SpanX;
        var pageSpanY = sternLeft ? bounds.SpanX : bounds.SpanY;
        var scale = pageSpanX > 1e-6 && pageSpanY > 1e-6
            ? System.Math.Min(contentW / pageSpanX, contentH / pageSpanY) * 0.88
            : 1.0;
        var ox = sternLeft
            ? size.BorderLeftMm + pad + (contentW - pageSpanX * scale) * 0.5 - bounds.MinZ * scale
            : size.BorderLeftMm + pad + (contentW - pageSpanX * scale) * 0.5 - bounds.MinX * scale;
        var oy = sternLeft
            ? size.BorderMm + pad + 22 + (contentH - pageSpanY * scale) * 0.5 + bounds.MaxX * scale
            : size.BorderMm + pad + 22 + (contentH - pageSpanY * scale) * 0.5 + bounds.MaxZ * scale;

        var scaleRatio = scale > 1e-6 ? System.Math.Round(1000.0 / scale) : 0;
        if (scaleRatio < 1) scaleRatio = 1;

        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine(
            $"<html lang=\"en\" {CadBlueprintDom.FormatAttr}=\"{CadBlueprintDom.FormatValue}\" {CadBlueprintDom.SchemaAttr}=\"{blueprint.SchemaVersion}\">");
        sb.AppendLine("<head><meta charset=\"utf-8\"/>");
        sb.AppendLine($"<title>{Esc(sheet.Title)}</title>");
        AppendStyles(sb, sheet);
        sb.AppendLine("</head><body>");

        sb.AppendLine("<main id=\"nbp-stage\">");
        sb.AppendLine(
            $"<svg id=\"{CadBlueprintDom.SheetElementId}\" xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 {F(w)} {F(h)}\" width=\"100%\" role=\"img\" aria-label=\"{Esc(sheet.Title)}\">");

        AppendChrome(sb, blueprint, sheet, size, w, h, titleBlockW, titleBlockH, scaleRatio, sternLeft, isShip);

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

    private static void AppendChrome(
        StringBuilder sb,
        CadBlueprint blueprint,
        CadBlueprintSheet sheet,
        CadBlueprintSheetSize size,
        double w,
        double h,
        double titleBlockW,
        double titleBlockH,
        double scaleRatio,
        bool sternLeft,
        bool isShip)
    {
        var frame = sheet.Layers.FirstOrDefault(l => l.Id is "chrome.frame" || l.Kind is "chrome");
        var frameId = frame?.Id ?? "chrome.frame";
        var framePath = frame?.Path ?? "chrome/frame";
        sb.AppendLine(
            $"<g {CadBlueprintDom.LayerAttr}=\"{Esc(frameId)}\" {CadBlueprintDom.PathAttr}=\"{Esc(framePath)}\" {CadBlueprintDom.KindAttr}=\"chrome\" {CadBlueprintDom.PlotAttr}=\"true\">");

        // Paper + ISO 5457 double border
        sb.AppendLine($"<rect x=\"0\" y=\"0\" width=\"{F(w)}\" height=\"{F(h)}\" fill=\"#f7f4ea\"/>");
        var ix = size.BorderLeftMm;
        var iy = size.BorderMm;
        var iw = w - size.BorderLeftMm - size.BorderMm;
        var ih = h - 2 * size.BorderMm;
        sb.AppendLine(
            $"<rect x=\"{F(ix)}\" y=\"{F(iy)}\" width=\"{F(iw)}\" height=\"{F(ih)}\" fill=\"none\" stroke=\"#111\" stroke-width=\"0.9\"/>");
        sb.AppendLine(
            $"<rect x=\"{F(ix + 3)}\" y=\"{F(iy + 3)}\" width=\"{F(iw - 6)}\" height=\"{F(ih - 6)}\" fill=\"none\" stroke=\"#111\" stroke-width=\"0.35\"/>");

        // Drawing title / scale (top-left)
        sb.AppendLine(
            $"<text x=\"{F(ix + 10)}\" y=\"{F(iy + 14)}\" class=\"title\">{Esc(sheet.Title)}</text>");
        var orientNote = sternLeft ? "ISO 128-15 stern left" : "plan north up";
        sb.AppendLine(
            $"<text x=\"{F(ix + 10)}\" y=\"{F(iy + 24)}\" class=\"dimt\">{Esc(blueprint.Context)} · {orientNote} · {Esc(sheet.Units)} · SCALE 1:{F(scaleRatio)}</text>");

        // North arrow (architectural plans)
        if (!sternLeft)
        {
            var nx = ix + iw - 28;
            var ny = iy + 28;
            sb.AppendLine($"<g transform=\"translate({F(nx)} {F(ny)})\" class=\"north\">");
            sb.AppendLine("<polygon points=\"0,-10 3.5,6 -3.5,6\" fill=\"#111\"/>");
            sb.AppendLine("<text x=\"0\" y=\"14\" class=\"dimt\" text-anchor=\"middle\">N</text>");
            sb.AppendLine("</g>");
        }

        // Class note — context-aware (no ship jargon on house sheets)
        var note = isShip
            ? "CLASS-STYLE GA COMPANION (novolis.cad.blueprint) — NOT FOR PLAN APPROVAL. ISO 128-15 / 5457 / 7200."
            : "ARCHITECTURAL GA COMPANION (novolis.cad.blueprint) — NOT FOR BUILDING PERMIT. ISO 5457 / 7200 sheet chrome.";
        sb.AppendLine(
            $"<text x=\"{F(ix + 10)}\" y=\"{F(h - size.BorderMm - 8)}\" class=\"classnote\">{Esc(note)}</text>");

        // ISO 7200 title block (bottom-right)
        var tbX = w - size.BorderMm - titleBlockW - 3;
        var tbY = h - size.BorderMm - titleBlockH - 3;
        AppendIso7200TitleBlock(sb, sheet, blueprint, tbX, tbY, titleBlockW, titleBlockH, scaleRatio, size);

        sb.AppendLine("</g>");
    }

    private static void AppendIso7200TitleBlock(
        StringBuilder sb,
        CadBlueprintSheet sheet,
        CadBlueprint blueprint,
        double x,
        double y,
        double tw,
        double th,
        double scaleRatio,
        CadBlueprintSheetSize size)
    {
        sb.AppendLine($"<g class=\"titleblock\" data-nbp-iso=\"7200\">");
        sb.AppendLine($"<rect x=\"{F(x)}\" y=\"{F(y)}\" width=\"{F(tw)}\" height=\"{F(th)}\" fill=\"#fff\" stroke=\"#111\" stroke-width=\"0.7\"/>");

        // Vertical splits: owner | drawing | meta
        var c1 = x + 52;
        var c2 = x + 118;
        sb.AppendLine($"<line x1=\"{F(c1)}\" y1=\"{F(y)}\" x2=\"{F(c1)}\" y2=\"{F(y + th)}\" stroke=\"#111\" stroke-width=\"0.35\"/>");
        sb.AppendLine($"<line x1=\"{F(c2)}\" y1=\"{F(y)}\" x2=\"{F(c2)}\" y2=\"{F(y + th)}\" stroke=\"#111\" stroke-width=\"0.35\"/>");

        // Horizontal rows
        var r1 = y + 12;
        var r2 = y + 28;
        var r3 = y + 42;
        sb.AppendLine($"<line x1=\"{F(x)}\" y1=\"{F(r1)}\" x2=\"{F(x + tw)}\" y2=\"{F(r1)}\" stroke=\"#111\" stroke-width=\"0.35\"/>");
        sb.AppendLine($"<line x1=\"{F(x)}\" y1=\"{F(r2)}\" x2=\"{F(x + tw)}\" y2=\"{F(r2)}\" stroke=\"#111\" stroke-width=\"0.35\"/>");
        sb.AppendLine($"<line x1=\"{F(x)}\" y1=\"{F(r3)}\" x2=\"{F(x + tw)}\" y2=\"{F(r3)}\" stroke=\"#111\" stroke-width=\"0.35\"/>");

        // Row 0 labels / values
        sb.AppendLine($"<text x=\"{F(x + 2)}\" y=\"{F(y + 5)}\" class=\"tbl\">OWNER / LEGAL</text>");
        sb.AppendLine($"<text x=\"{F(x + 2)}\" y=\"{F(y + 10)}\" class=\"tbv\">Novolis</text>");
        sb.AppendLine($"<text x=\"{F(c1 + 2)}\" y=\"{F(y + 5)}\" class=\"tbl\">DRAWING TITLE</text>");
        sb.AppendLine($"<text x=\"{F(c1 + 2)}\" y=\"{F(y + 10)}\" class=\"tbv\">{Esc(Truncate(sheet.Title, 28))}</text>");
        sb.AppendLine($"<text x=\"{F(c2 + 2)}\" y=\"{F(y + 5)}\" class=\"tbl\">SHEET</text>");
        sb.AppendLine($"<text x=\"{F(c2 + 2)}\" y=\"{F(y + 10)}\" class=\"tbv\">{Esc(size.Size)} {(size.Landscape ? "LANDSCAPE" : "PORTRAIT")}</text>");

        sb.AppendLine($"<text x=\"{F(x + 2)}\" y=\"{F(r1 + 5)}\" class=\"tbl\">PROJECT</text>");
        sb.AppendLine($"<text x=\"{F(x + 2)}\" y=\"{F(r1 + 10)}\" class=\"tbv\">{Esc(Truncate(blueprint.Name, 18))}</text>");
        sb.AppendLine($"<text x=\"{F(c1 + 2)}\" y=\"{F(r1 + 5)}\" class=\"tbl\">DRAWING NO.</text>");
        sb.AppendLine($"<text x=\"{F(c1 + 2)}\" y=\"{F(r1 + 10)}\" class=\"tbv-lg\">{Esc(sheet.Id)}</text>");
        sb.AppendLine($"<text x=\"{F(c2 + 2)}\" y=\"{F(r1 + 5)}\" class=\"tbl\">REV</text>");
        sb.AppendLine($"<text x=\"{F(c2 + 2)}\" y=\"{F(r1 + 10)}\" class=\"tbv-lg\">{Esc(sheet.Rev ?? "A")}</text>");

        sb.AppendLine($"<text x=\"{F(x + 2)}\" y=\"{F(r2 + 5)}\" class=\"tbl\">CONTEXT</text>");
        sb.AppendLine($"<text x=\"{F(x + 2)}\" y=\"{F(r2 + 10)}\" class=\"tbv\">{Esc(blueprint.Context)}</text>");
        sb.AppendLine($"<text x=\"{F(c1 + 2)}\" y=\"{F(r2 + 5)}\" class=\"tbl\">SCALE</text>");
        sb.AppendLine($"<text x=\"{F(c1 + 2)}\" y=\"{F(r2 + 10)}\" class=\"tbv\">1:{F(scaleRatio)}</text>");
        sb.AppendLine($"<text x=\"{F(c2 + 2)}\" y=\"{F(r2 + 5)}\" class=\"tbl\">UNITS</text>");
        sb.AppendLine($"<text x=\"{F(c2 + 2)}\" y=\"{F(r2 + 10)}\" class=\"tbv\">{Esc(sheet.Units)}</text>");

        sb.AppendLine($"<text x=\"{F(x + 2)}\" y=\"{F(r3 + 5)}\" class=\"tbl\">CREATED</text>");
        sb.AppendLine($"<text x=\"{F(x + 2)}\" y=\"{F(r3 + 10)}\" class=\"tbv\">{Esc(Truncate(blueprint.CreatedAt ?? "", 16))}</text>");
        sb.AppendLine($"<text x=\"{F(c1 + 2)}\" y=\"{F(r3 + 5)}\" class=\"tbl\">STANDARD</text>");
        sb.AppendLine($"<text x=\"{F(c1 + 2)}\" y=\"{F(r3 + 10)}\" class=\"tbv\">ISO 5457 / 7200</text>");
        sb.AppendLine($"<text x=\"{F(c2 + 2)}\" y=\"{F(r3 + 5)}\" class=\"tbl\">STATUS</text>");
        sb.AppendLine($"<text x=\"{F(c2 + 2)}\" y=\"{F(r3 + 10)}\" class=\"tbv\">PRELIMINARY</text>");
        sb.AppendLine("</g>");
    }

    private static void AppendStyles(StringBuilder sb, CadBlueprintSheet sheet)
    {
        sb.AppendLine("<style>");
        sb.AppendLine("""
            :root { --nbp-sheet:#fff; --nbp-ink:#111; --nbp-accent:#5a9fd4; --nbp-wall:#2a2a2a; }
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
            .ol { fill:none; stroke:#888; stroke-width:0.45; stroke-dasharray:2 1.2; }
            .space { fill:#fafafa; stroke:none; }
            .wall { fill:#333; stroke:#111; stroke-width:0.2; }
            .wall-out { fill:none; stroke:#111; stroke-width:0.55; }
            .door-leaf { stroke:#111; stroke-width:0.55; fill:none; }
            .door-swing { stroke:#444; stroke-width:0.28; fill:none; stroke-dasharray:1.2 0.8; }
            .window { stroke:#1a4a7a; stroke-width:0.4; fill:none; }
            .window-glass { stroke:#5a9fd4; stroke-width:0.35; fill:none; }
            .dim { stroke:#222; stroke-width:0.22; fill:none; }
            .dim-tick { stroke:#222; stroke-width:0.35; }
            .dimt { font:2.8px "IBM Plex Mono",Consolas,monospace; fill:#111; }
            .title { font:5px "IBM Plex Mono",Consolas,monospace; fill:#111; font-weight:600; }
            .room { font:3.4px "IBM Plex Mono",Consolas,monospace; fill:#111; }
            .classnote { font:2.4px "IBM Plex Mono",Consolas,monospace; fill:#555; }
            .tbl { font:2.1px "IBM Plex Mono",Consolas,monospace; fill:#666; }
            .tbv { font:2.8px "IBM Plex Mono",Consolas,monospace; fill:#111; }
            .tbv-lg { font:3.6px "IBM Plex Mono",Consolas,monospace; fill:#111; font-weight:600; }
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

        double SX(float x, float z) => sternLeft ? ox + z * scale : ox + x * scale;
        double SY(float x, float z) => sternLeft ? oy - x * scale : oy - z * scale;

        var hasSpaces = blueprint.Spaces.Any(s =>
            (!levelIndex.HasValue || s.LevelIndex == levelIndex) && s.Footprint is { Count: >= 3 });

        // Shells — dashed outline only; never stamp "cottage-shell" over rooms
        sb.AppendLine($"<g {LayerAttrs("exterior.shell", "exterior/shell", "outline")}>");
        foreach (var shell in blueprint.Shells)
        {
            if (shell.PlanRing is not { Count: >= 3 })
                continue;
            sb.AppendLine($"<path d=\"{RingPath(shell.PlanRing, SX, SY)}\" class=\"ol\"/>");
            if (!hasSpaces && !string.IsNullOrWhiteSpace(shell.Name))
            {
                sb.AppendLine(
                    $"<text x=\"{F(CentroidX(shell.PlanRing, SX, SY))}\" y=\"{F(CentroidY(shell.PlanRing, SX, SY))}\" class=\"room\" text-anchor=\"middle\">{Esc(shell.Name)}</text>");
            }
        }

        sb.AppendLine("</g>");

        // Spaces — fill only (walls own the strokes)
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

        // Walls — double-line / thickness pouches
        sb.AppendLine($"<g {LayerAttrs("structure.walls", "structure/walls", "structure")}>");
        foreach (var wall in blueprint.Walls)
        {
            if (levelIndex is { } li && wall.LevelIndex != li)
                continue;
            if (wall.A is null || wall.B is null || wall.A.Length < 1 || wall.B.Length < 1)
                continue;
            PlanXZ(wall.A, out var ax, out var az);
            PlanXZ(wall.B, out var bx, out var bz);
            var thick = wall.Thickness > 1e-4f ? wall.Thickness : 0.15f;
            AppendThickWall(sb, ax, az, bx, bz, thick, SX, SY);
        }

        sb.AppendLine("</g>");

        // Openings — door swings / windows (punch + symbol)
        sb.AppendLine($"<g {LayerAttrs("openings.all", "openings/all", "opening")}>");
        foreach (var opening in blueprint.Openings)
        {
            if (levelIndex is { } li && opening.LevelIndex != li)
                continue;
            if (!TryOpeningAxis(opening, blueprint, out var cx, out var cz, out var tx, out var tz, out var halfW, out var hostThick))
                continue;
            var kind = (opening.Kind ?? "door").ToLowerInvariant();
            if (kind is "window")
                AppendWindowSymbol(sb, cx, cz, tx, tz, halfW, hostThick, SX, SY);
            else if (kind is "hatch" or "hole")
                AppendHoleSymbol(sb, cx, cz, tx, tz, halfW, hostThick, SX, SY);
            else
                AppendDoorSymbol(sb, cx, cz, tx, tz, halfW, hostThick, SX, SY);
        }

        sb.AppendLine("</g>");

        // Primary overall dims with ticks
        sb.AppendLine($"<g {LayerAttrs("dims.primary", "dims/primary", "dimension")}>");
        var b = CollectBounds(blueprint, sheet);
        if (b.SpanX > 0.05 && b.SpanY > 0.05)
        {
            var x0 = sternLeft ? ox + b.MinZ * scale : ox + b.MinX * scale;
            var x1 = sternLeft ? ox + b.MaxZ * scale : ox + b.MaxX * scale;
            var y0 = sternLeft ? oy - b.MaxX * scale : oy - b.MaxZ * scale;
            var y1 = sternLeft ? oy - b.MinX * scale : oy - b.MinZ * scale;
            var longSpan = sternLeft ? b.SpanY : b.SpanX;
            var athwartSpan = sternLeft ? b.SpanX : b.SpanY;
            var minX = System.Math.Min(x0, x1);
            var maxX = System.Math.Max(x0, x1);
            var minY = System.Math.Min(y0, y1);
            var maxY = System.Math.Max(y0, y1);
            var dimY = maxY + 14;
            var dimX = maxX + 14;

            // Horizontal overall
            sb.AppendLine($"<line x1=\"{F(minX)}\" y1=\"{F(maxY + 2)}\" x2=\"{F(minX)}\" y2=\"{F(dimY)}\" class=\"dim\"/>");
            sb.AppendLine($"<line x1=\"{F(maxX)}\" y1=\"{F(maxY + 2)}\" x2=\"{F(maxX)}\" y2=\"{F(dimY)}\" class=\"dim\"/>");
            sb.AppendLine($"<line x1=\"{F(minX)}\" y1=\"{F(dimY)}\" x2=\"{F(maxX)}\" y2=\"{F(dimY)}\" class=\"dim\"/>");
            sb.AppendLine($"<line x1=\"{F(minX - 1.2)}\" y1=\"{F(dimY - 1.2)}\" x2=\"{F(minX + 1.2)}\" y2=\"{F(dimY + 1.2)}\" class=\"dim-tick\"/>");
            sb.AppendLine($"<line x1=\"{F(maxX - 1.2)}\" y1=\"{F(dimY - 1.2)}\" x2=\"{F(maxX + 1.2)}\" y2=\"{F(dimY + 1.2)}\" class=\"dim-tick\"/>");
            sb.AppendLine(
                $"<text x=\"{F((minX + maxX) * 0.5)}\" y=\"{F(dimY + 7)}\" class=\"dimt\" text-anchor=\"middle\">{Nice(longSpan)} m</text>");

            // Vertical overall
            sb.AppendLine($"<line x1=\"{F(maxX + 2)}\" y1=\"{F(minY)}\" x2=\"{F(dimX)}\" y2=\"{F(minY)}\" class=\"dim\"/>");
            sb.AppendLine($"<line x1=\"{F(maxX + 2)}\" y1=\"{F(maxY)}\" x2=\"{F(dimX)}\" y2=\"{F(maxY)}\" class=\"dim\"/>");
            sb.AppendLine($"<line x1=\"{F(dimX)}\" y1=\"{F(minY)}\" x2=\"{F(dimX)}\" y2=\"{F(maxY)}\" class=\"dim\"/>");
            sb.AppendLine($"<line x1=\"{F(dimX - 1.2)}\" y1=\"{F(minY - 1.2)}\" x2=\"{F(dimX + 1.2)}\" y2=\"{F(minY + 1.2)}\" class=\"dim-tick\"/>");
            sb.AppendLine($"<line x1=\"{F(dimX - 1.2)}\" y1=\"{F(maxY - 1.2)}\" x2=\"{F(dimX + 1.2)}\" y2=\"{F(maxY + 1.2)}\" class=\"dim-tick\"/>");
            sb.AppendLine(
                $"<text x=\"{F(dimX + 5)}\" y=\"{F((minY + maxY) * 0.5)}\" class=\"dimt\">{Nice(athwartSpan)} m</text>");
        }

        sb.AppendLine("</g>");
    }

    private static void AppendThickWall(
        StringBuilder sb,
        float ax,
        float az,
        float bx,
        float bz,
        float thickness,
        Func<float, float, double> sx,
        Func<float, float, double> sy)
    {
        var dx = bx - ax;
        var dz = bz - az;
        var len = System.Math.Sqrt(dx * dx + dz * dz);
        if (len < 1e-6)
            return;
        var hx = (float)(-dz / len * (thickness * 0.5));
        var hz = (float)(dx / len * (thickness * 0.5));

        var p0x = sx(ax + hx, az + hz);
        var p0y = sy(ax + hx, az + hz);
        var p1x = sx(bx + hx, bz + hz);
        var p1y = sy(bx + hx, bz + hz);
        var p2x = sx(bx - hx, bz - hz);
        var p2y = sy(bx - hx, bz - hz);
        var p3x = sx(ax - hx, az - hz);
        var p3y = sy(ax - hx, az - hz);

        sb.AppendLine(
            $"<path d=\"M {F(p0x)} {F(p0y)} L {F(p1x)} {F(p1y)} L {F(p2x)} {F(p2y)} L {F(p3x)} {F(p3y)} Z\" class=\"wall\"/>");
        sb.AppendLine(
            $"<path d=\"M {F(p0x)} {F(p0y)} L {F(p1x)} {F(p1y)} L {F(p2x)} {F(p2y)} L {F(p3x)} {F(p3y)} Z\" class=\"wall-out\"/>");
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

        if (opening.HostWallId is { } hid)
        {
            var wall = blueprint.Walls.FirstOrDefault(w => w.Id == hid);
            if (wall?.A is not null && wall.B is not null)
            {
                PlanXZ(wall.A, out var ax, out var az);
                PlanXZ(wall.B, out var bx, out var bz);
                hostThick = wall.Thickness > 1e-4f ? wall.Thickness : 0.15f;
            }
        }

        if (opening.Footprint is { Count: >= 2 })
        {
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
                if (hostThick < sz && sz > 1e-3f)
                    hostThick = sz;
            }
            else
            {
                tx = 0;
                tz = 1;
                halfW = opening.ClearWidth > 1e-3f ? opening.ClearWidth * 0.5f : sz * 0.5f;
                if (hostThick < sx && sx > 1e-3f)
                    hostThick = sx;
            }

            return halfW > 1e-4f;
        }

        return false;
    }

    private static void AppendDoorSymbol(
        StringBuilder sb,
        float cx,
        float cz,
        float tx,
        float tz,
        float halfW,
        float hostThick,
        Func<float, float, double> sx,
        Func<float, float, double> sy)
    {
        // Perpendicular into plan (right of tangent)
        var nx = -tz;
        var nz = tx;
        var hingeX = cx - tx * halfW;
        var hingeZ = cz - tz * halfW;
        var latchX = cx + tx * halfW;
        var latchZ = cz + tz * halfW;

        // Punch white gap through wall
        var punch = hostThick * 0.55f + 0.02f;
        var q0x = sx(hingeX + nx * punch, hingeZ + nz * punch);
        var q0y = sy(hingeX + nx * punch, hingeZ + nz * punch);
        var q1x = sx(latchX + nx * punch, latchZ + nz * punch);
        var q1y = sy(latchX + nx * punch, latchZ + nz * punch);
        var q2x = sx(latchX - nx * punch, latchZ - nz * punch);
        var q2y = sy(latchX - nx * punch, latchZ - nz * punch);
        var q3x = sx(hingeX - nx * punch, hingeZ - nz * punch);
        var q3y = sy(hingeX - nx * punch, hingeZ - nz * punch);
        sb.AppendLine(
            $"<path d=\"M {F(q0x)} {F(q0y)} L {F(q1x)} {F(q1y)} L {F(q2x)} {F(q2y)} L {F(q3x)} {F(q3y)} Z\" fill=\"#f7f4ea\" stroke=\"none\"/>");

        // Door leaf closed along wall, swing into +normal
        var leafEndX = hingeX + nx * (halfW * 2);
        var leafEndZ = hingeZ + nz * (halfW * 2);
        sb.AppendLine(
            $"<line x1=\"{F(sx(hingeX, hingeZ))}\" y1=\"{F(sy(hingeX, hingeZ))}\" x2=\"{F(sx(leafEndX, leafEndZ))}\" y2=\"{F(sy(leafEndX, leafEndZ))}\" class=\"door-leaf\"/>");

        // Quarter-circle swing (SVG arc in page space)
        var r = System.Math.Abs(sx(latchX, latchZ) - sx(hingeX, hingeZ))
                + System.Math.Abs(sy(latchX, latchZ) - sy(hingeX, hingeZ));
        // Prefer model-space radius projected roughly
        var pageHingeX = sx(hingeX, hingeZ);
        var pageHingeY = sy(hingeX, hingeZ);
        var pageLatchX = sx(latchX, latchZ);
        var pageLatchY = sy(latchX, latchZ);
        var pageLeafX = sx(leafEndX, leafEndZ);
        var pageLeafY = sy(leafEndX, leafEndZ);
        var radius = System.Math.Sqrt(
            (pageLatchX - pageHingeX) * (pageLatchX - pageHingeX)
            + (pageLatchY - pageHingeY) * (pageLatchY - pageHingeY));
        if (radius < 0.5) radius = System.Math.Sqrt(
            (pageLeafX - pageHingeX) * (pageLeafX - pageHingeX)
            + (pageLeafY - pageHingeY) * (pageLeafY - pageHingeY));

        // Sweep from leaf (open) to latch (closed) — large-arc=0, sweep=1
        sb.AppendLine(
            $"<path d=\"M {F(pageLeafX)} {F(pageLeafY)} A {F(radius)} {F(radius)} 0 0 1 {F(pageLatchX)} {F(pageLatchY)}\" class=\"door-swing\"/>");
        _ = r;
    }

    private static void AppendWindowSymbol(
        StringBuilder sb,
        float cx,
        float cz,
        float tx,
        float tz,
        float halfW,
        float hostThick,
        Func<float, float, double> sx,
        Func<float, float, double> sy)
    {
        var nx = -tz;
        var nz = tx;
        var aX = cx - tx * halfW;
        var aZ = cz - tz * halfW;
        var bX = cx + tx * halfW;
        var bZ = cz + tz * halfW;
        var punch = hostThick * 0.55f + 0.02f;

        // Punch
        var q0x = sx(aX + nx * punch, aZ + nz * punch);
        var q0y = sy(aX + nx * punch, aZ + nz * punch);
        var q1x = sx(bX + nx * punch, bZ + nz * punch);
        var q1y = sy(bX + nx * punch, bZ + nz * punch);
        var q2x = sx(bX - nx * punch, bZ - nz * punch);
        var q2y = sy(bX - nx * punch, bZ - nz * punch);
        var q3x = sx(aX - nx * punch, aZ - nz * punch);
        var q3y = sy(aX - nx * punch, aZ - nz * punch);
        sb.AppendLine(
            $"<path d=\"M {F(q0x)} {F(q0y)} L {F(q1x)} {F(q1y)} L {F(q2x)} {F(q2y)} L {F(q3x)} {F(q3y)} Z\" fill=\"#f7f4ea\" stroke=\"none\"/>");

        // Outer sill lines + glass centerline
        var t = hostThick * 0.35f;
        sb.AppendLine(
            $"<line x1=\"{F(sx(aX + nx * t, aZ + nz * t))}\" y1=\"{F(sy(aX + nx * t, aZ + nz * t))}\" x2=\"{F(sx(bX + nx * t, bZ + nz * t))}\" y2=\"{F(sy(bX + nx * t, bZ + nz * t))}\" class=\"window\"/>");
        sb.AppendLine(
            $"<line x1=\"{F(sx(aX - nx * t, aZ - nz * t))}\" y1=\"{F(sy(aX - nx * t, aZ - nz * t))}\" x2=\"{F(sx(bX - nx * t, bZ - nz * t))}\" y2=\"{F(sy(bX - nx * t, bZ - nz * t))}\" class=\"window\"/>");
        sb.AppendLine(
            $"<line x1=\"{F(sx(aX, aZ))}\" y1=\"{F(sy(aX, aZ))}\" x2=\"{F(sx(bX, bZ))}\" y2=\"{F(sy(bX, bZ))}\" class=\"window-glass\"/>");
    }

    private static void AppendHoleSymbol(
        StringBuilder sb,
        float cx,
        float cz,
        float tx,
        float tz,
        float halfW,
        float hostThick,
        Func<float, float, double> sx,
        Func<float, float, double> sy)
    {
        var nx = -tz;
        var nz = tx;
        var aX = cx - tx * halfW;
        var aZ = cz - tz * halfW;
        var bX = cx + tx * halfW;
        var bZ = cz + tz * halfW;
        var punch = hostThick * 0.55f + 0.02f;
        var q0x = sx(aX + nx * punch, aZ + nz * punch);
        var q0y = sy(aX + nx * punch, aZ + nz * punch);
        var q1x = sx(bX + nx * punch, bZ + nz * punch);
        var q1y = sy(bX + nx * punch, bZ + nz * punch);
        var q2x = sx(bX - nx * punch, bZ - nz * punch);
        var q2y = sy(bX - nx * punch, bZ - nz * punch);
        var q3x = sx(aX - nx * punch, aZ - nz * punch);
        var q3y = sy(aX - nx * punch, aZ - nz * punch);
        sb.AppendLine(
            $"<path d=\"M {F(q0x)} {F(q0y)} L {F(q1x)} {F(q1y)} L {F(q2x)} {F(q2y)} L {F(q3x)} {F(q3y)} Z\" fill=\"#f7f4ea\" stroke=\"#666\" stroke-width=\"0.25\" stroke-dasharray=\"1 0.6\"/>");
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

    private static Bounds CollectBounds(CadBlueprint blueprint, CadBlueprintSheet sheet)
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
        if (!any)
            return new Bounds(0, 10, 0, 10);
        return new Bounds(minX, maxX, minZ, maxZ);
    }

    private static bool IsShipContext(string context) =>
        context is CadBlueprintContexts.SeagoingShip
            or CadBlueprintContexts.Spaceship
            or CadBlueprintContexts.SpaceStation;

    private static string Truncate(string s, int max) =>
        string.IsNullOrEmpty(s) ? "" : s.Length <= max ? s : s[..(max - 1)] + "…";

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

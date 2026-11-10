using System.Text.Json;
using Novolis.Cad.Primitives;

namespace Novolis.Cad.Blueprint;

/// <summary>
/// Projects a full <see cref="CadDocument"/> into a contextual <see cref="CadBlueprint"/>.
/// Only wall / space / opening (and optional hull-like solids tagged as shell) are lifted;
/// sketch noise, modifiers, and meshes stay in the CAD document.
/// </summary>
public static class CadBlueprintProjector
{
    public static CadBlueprint FromCadDocument(CadDocument doc, string? context = null, string? cadDocumentHref = null) =>
        FromCadDocumentWithDiagnostics(doc, context, cadDocumentHref).Blueprint;

    /// <summary>
    /// Same as <see cref="FromCadDocument"/> but surfaces lift diagnostics (skipped kinds, missing footprints).
    /// </summary>
    public static (CadBlueprint Blueprint, IReadOnlyList<CadBlueprintDiagnostic> Diagnostics)
        FromCadDocumentWithDiagnostics(CadDocument doc, string? context = null, string? cadDocumentHref = null)
    {
        ArgumentNullException.ThrowIfNull(doc);
        var diagnostics = new List<CadBlueprintDiagnostic>();

        var bp = new CadBlueprint
        {
            Name = string.IsNullOrWhiteSpace(doc.Name) ? "Blueprint" : $"{doc.Name} blueprint",
            Context = string.IsNullOrWhiteSpace(context) ? InferContext(doc) : context!,
            LinearUnit = doc.LinearUnit,
            UnitScaleMeters = doc.UnitScaleMeters,
            CadDocumentHref = cadDocumentHref,
            CadDocumentName = doc.Name,
            LayerCatalogHref = doc.LayersDocument,
            CreatedAt = doc.CreatedAt,
            ModifiedAt = doc.ModifiedAt,
            Generator = new CadGenerator
            {
                Name = "Novolis.Cad.Blueprint",
                Version = doc.Generator.Version,
            },
        };

        var layerName = doc.Layers.ToDictionary(l => l.Id, l => l.Name);

        var levelIndexes = new SortedSet<int>();
        foreach (var e in doc.Entities)
        {
            if (e.Kind is "wall" or "space" or "opening")
                levelIndexes.Add(e.Deck);
        }

        if (levelIndexes.Count == 0)
            levelIndexes.Add(0);

        foreach (var idx in levelIndexes)
        {
            bp.Levels.Add(new CadBlueprintLevel
            {
                Index = idx,
                Name = LevelName(bp.Context, idx),
            });
        }

        var skipped = 0;
        var strokeEntities = new List<CadEntity>();
        foreach (var e in doc.Entities)
        {
            switch (e.Kind.ToLowerInvariant())
            {
                case "wall":
                    bp.Walls.Add(ToWall(e, layerName));
                    break;
                case "space":
                {
                    var space = ToSpace(e, layerName);
                    if (space.Footprint is null || space.Footprint.Count < 3)
                    {
                        diagnostics.Add(new CadBlueprintDiagnostic(
                            CadBlueprintDiagnosticSeverity.Warning,
                            "BP200",
                            $"Space '{space.Name}' lifted without footprint.",
                            $"entities/{e.Id}"));
                    }

                    bp.Spaces.Add(space);
                    break;
                }
                case "opening":
                    bp.Openings.Add(ToOpening(e, layerName));
                    break;
                case "box" when LooksLikeShell(e):
                    bp.Shells.Add(ToShellFromBox(e));
                    break;
                case "polyline" or "text" when HasViewTag(e):
                    strokeEntities.Add(e);
                    break;
                default:
                    skipped++;
                    break;
            }
        }

        if (skipped > 0)
        {
            diagnostics.Add(new CadBlueprintDiagnostic(
                CadBlueprintDiagnosticSeverity.Warning,
                "BP201",
                $"Skipped {skipped} non-blueprint entity kind(s) (sketch/mesh/etc stay in CadDocument).",
                "entities"));
        }

        CadBlueprintHullLoftShell.TryAddFromDocument(bp, doc, diagnostics);

        var drawingViews = ReadDrawingViewCatalog(doc);
        if (drawingViews.Count > 0)
        {
            foreach (var sheet in CreateSheetsFromDrawingViews(bp, drawingViews, strokeEntities))
                bp.Sheets.Add(sheet);
        }
        else
        {
            bp.Sheets.Add(CreateDefaultSheet(bp));
        }

        return (bp, diagnostics);
    }

    private static bool HasViewTag(CadEntity e) =>
        e.Properties is not null && e.Properties.ContainsKey("nbp.viewId");

    private static List<DrawingViewMeta> ReadDrawingViewCatalog(CadDocument doc)
    {
        if (doc.Properties is null
            || !doc.Properties.TryGetValue("nbp.drawingViews", out var el)
            || el.ValueKind != JsonValueKind.Array)
            return [];

        var list = new List<DrawingViewMeta>();
        foreach (var item in el.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
                continue;
            var id = item.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
            var kind = item.TryGetProperty("kind", out var kindEl) ? kindEl.GetString() : null;
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(kind))
                continue;
            var label = item.TryGetProperty("label", out var labelEl) ? labelEl.GetString() : kind;
            var scaleHint = item.TryGetProperty("scaleHint", out var scaleEl) ? scaleEl.GetString() : null;
            list.Add(new DrawingViewMeta(id!, kind!, label ?? kind!, scaleHint));
        }

        return list;
    }

    private static IEnumerable<CadBlueprintSheet> CreateSheetsFromDrawingViews(
        CadBlueprint bp,
        IReadOnlyList<DrawingViewMeta> views,
        IReadOnlyList<CadEntity> strokeEntities)
    {
        var ordered = views
            .OrderBy(v => v.Kind switch
            {
                "plan" => 0,
                "section" => 1,
                "elevation" => 2,
                _ => 9,
            })
            .ThenBy(v => v.Id, StringComparer.Ordinal)
            .ToList();

        var index = 0;
        foreach (var meta in ordered)
        {
            index++;
            var isPlan = meta.Kind.Equals("plan", StringComparison.OrdinalIgnoreCase);
            var sheet = new CadBlueprintSheet
            {
                Id = $"GA-{index:000}",
                Title = $"{bp.Name} — {meta.Label}",
                Rev = "A",
                Orientation = bp.Context is CadBlueprintContexts.Spaceship
                    or CadBlueprintContexts.SpaceStation
                    or CadBlueprintContexts.SeagoingShip
                    ? "iso128-15-stern-left"
                    : "plan-north-up",
                Folders = isPlan
                    ?
                    [
                        new() { Path = "chrome", Label = "Sheet chrome" },
                        new() { Path = "exterior", Label = "Exteriors" },
                        new() { Path = "structure", Label = "Walls" },
                        new() { Path = "interior", Label = "Interiors" },
                        new() { Path = "openings", Label = "Doors / hatches / holes" },
                        new() { Path = "overlays", Label = "Overlays" },
                        new() { Path = "dims", Label = "Measurements" },
                    ]
                    :
                    [
                        new() { Path = "chrome", Label = "Sheet chrome" },
                        new() { Path = "drawing", Label = "Drawing" },
                    ],
                Layers = isPlan
                    ?
                    [
                        Layer("chrome.frame", "chrome/frame", "Frame", "chrome", locked: true),
                        Layer("exterior.shell", "exterior/shell", "Shell / facade", "outline"),
                        Layer("structure.walls", "structure/walls", "Walls", "structure"),
                        Layer("interior.spaces", "interior/spaces", "Spaces", "space"),
                        Layer("openings.all", "openings/all", "Openings", "opening"),
                        Layer("overlays.furniture", "overlays/furniture", "Furniture", "overlay", plot: true),
                        Layer("overlays.dims", "overlays/dims", "Source dims", "overlay", plot: true),
                        Layer("overlays.grid", "overlays/grid", "Grid", "overlay", plot: true),
                        Layer("overlays.drawing", "overlays/drawing", "Drawing strokes", "overlay", plot: true),
                        Layer("dims.primary", "dims/primary", "Primary dims", "dimension"),
                    ]
                    :
                    [
                        Layer("chrome.frame", "chrome/frame", "Frame", "chrome", locked: true),
                        Layer("drawing.strokes", "drawing/strokes", "Linework", "drawing"),
                        Layer("drawing.texts", "drawing/texts", "Labels", "drawing"),
                    ],
            };

            var view = new CadBlueprintView
            {
                Id = meta.Id,
                Label = meta.Label,
                Kind = meta.Kind,
                Scale = meta.ScaleHint,
                LevelIndex = isPlan ? bp.Levels.FirstOrDefault()?.Index ?? 0 : null,
            };
            AttachStrokes(view, strokeEntities, meta.Id);
            sheet.Views.Add(view);

            sheet.Presets.Add(new CadBlueprintPreset
            {
                Id = "print",
                Label = "Print",
                ForPlot = true,
                VisibleLayerIds = sheet.Layers.Where(l => l.Plot).Select(l => l.Id).ToList(),
            });
            sheet.DefaultPresetId = "print";
            yield return sheet;
        }
    }

    private static void AttachStrokes(CadBlueprintView view, IReadOnlyList<CadEntity> strokeEntities, string viewId)
    {
        foreach (var e in strokeEntities)
        {
            if (e.Properties is null
                || !e.Properties.TryGetValue("nbp.viewId", out var idEl)
                || !string.Equals(idEl.GetString(), viewId, StringComparison.Ordinal))
                continue;

            var style = e.Properties.TryGetValue("nbp.stroke", out var strokeEl)
                ? strokeEl.GetString() ?? "solid"
                : "solid";
            var layerHint = e.Properties.TryGetValue("nbp.layer", out var layerEl)
                ? layerEl.GetString()
                : null;

            if (e.Kind.Equals("text", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(e.Name) || e.Center is null)
                    continue;
                view.Texts.Add(new CadBlueprintViewText
                {
                    Text = e.Name!,
                    Position = e.Center,
                    Height = e.Height > 0 ? e.Height : 0.2f,
                });
                continue;
            }

            if (e.Points is not { Count: >= 2 })
                continue;

            view.Strokes.Add(new CadBlueprintStroke
            {
                Style = style,
                LayerHint = layerHint,
                Closed = e.Closed,
                Points = e.Points.Select(p => (float[])p.Clone()).ToList(),
            });
        }
    }

    private readonly record struct DrawingViewMeta(string Id, string Kind, string Label, string? ScaleHint);

    private static bool LooksLikeShell(CadEntity e) =>
        e.Name is not null
        && (e.Name.Contains("hull", StringComparison.OrdinalIgnoreCase)
            || e.Name.Contains("shell", StringComparison.OrdinalIgnoreCase)
            || e.Name.Contains("facade", StringComparison.OrdinalIgnoreCase)
            || e.Name.Contains("envelope", StringComparison.OrdinalIgnoreCase));

    private static string InferContext(CadDocument doc)
    {
        if (CadVec.LooksLikeShipDocument(doc))
            return CadBlueprintContexts.SeagoingShip;
        return CadBlueprintContexts.Generic;
    }

    private static string LevelName(string context, int index) =>
        context is CadBlueprintContexts.Spaceship
            or CadBlueprintContexts.SpaceStation
            or CadBlueprintContexts.SeagoingShip
            ? $"Deck {index}"
            : index == 0 ? "Ground" : $"Level {index}";

    private static CadBlueprintWall ToWall(CadEntity e, Dictionary<Guid, string> layerName) => new()
    {
        Id = e.Id,
        Name = e.Name,
        LevelIndex = e.Deck,
        A = e.A,
        B = e.B,
        Thickness = e.Thickness,
        Height = e.Height,
        LayerId = e.LayerId,
        LayerName = e.LayerId is { } id && layerName.TryGetValue(id, out var n) ? n : null,
        SourceEntityId = e.Id,
    };

    private static CadBlueprintSpace ToSpace(CadEntity e, Dictionary<Guid, string> layerName) => new()
    {
        Id = e.Id,
        Name = e.Name ?? "Space",
        LevelIndex = e.Deck,
        Kind = InferSpaceKind(e),
        Footprint = e.Footprint ?? e.Points,
        Height = e.Height,
        LayerId = e.LayerId,
        LayerName = e.LayerId is { } id && layerName.TryGetValue(id, out var n) ? n : null,
        SourceEntityId = e.Id,
    };

    private static string InferSpaceKind(CadEntity e)
    {
        var name = e.Name ?? "";
        if (name.Contains("corr", StringComparison.OrdinalIgnoreCase)
            || name.Contains("hall", StringComparison.OrdinalIgnoreCase))
            return "circulation";
        if (name.Contains("cargo", StringComparison.OrdinalIgnoreCase)
            || name.Contains("hold", StringComparison.OrdinalIgnoreCase))
            return "cargo";
        if (name.Contains("galley", StringComparison.OrdinalIgnoreCase)
            || name.Contains("store", StringComparison.OrdinalIgnoreCase)
            || name.Contains("util", StringComparison.OrdinalIgnoreCase))
            return "service";
        return "interior";
    }

    private static CadBlueprintOpening ToOpening(CadEntity e, Dictionary<Guid, string> layerName)
    {
        var kind = (e.OpeningType ?? "door").ToLowerInvariant() switch
        {
            "hatch" => "hatch",
            "hole" or "void" => "hole",
            "window" => "window",
            _ => "door",
        };

        return new CadBlueprintOpening
        {
            Id = e.Id,
            Name = e.Name,
            LevelIndex = e.Deck,
            Kind = kind,
            ClearWidth = EstimateClearWidth(e),
            ClearHeight = e.Height > 0 ? e.Height : 2.1f,
            Footprint = e.Footprint,
            HostWallId = e.HostWallId,
            LayerId = e.LayerId,
            LayerName = e.LayerId is { } id && layerName.TryGetValue(id, out var n) ? n : null,
            SourceEntityId = e.Id,
        };
    }

    private static float EstimateClearWidth(CadEntity e)
    {
        // DXF importer stores clear width in Thickness for openings.
        if (e.Thickness >= 0.4f)
            return e.Thickness;
        if (e.Footprint is not { Count: >= 2 })
            return e.Thickness > 0 ? e.Thickness : 1f;
        var minX = e.Footprint.Min(p => p[0]);
        var maxX = e.Footprint.Max(p => p[0]);
        var minZ = e.Footprint.Min(p => p.Length > 2 ? p[2] : p[1]);
        var maxZ = e.Footprint.Max(p => p.Length > 2 ? p[2] : p[1]);
        // Clear is the long AABB side; short side is wall thickness.
        return System.Math.Max(maxX - minX, maxZ - minZ);
    }

    private static CadBlueprintShell ToShellFromBox(CadEntity e)
    {
        if (!CadShipGeometry.TryGetBox(e, out var center, out var he))
        {
            return new CadBlueprintShell
            {
                Id = e.Id,
                Name = e.Name ?? "Shell",
                Kind = "exterior",
                SourceEntityId = e.Id,
            };
        }

        var x0 = center.X - he.X;
        var x1 = center.X + he.X;
        var z0 = center.Z - he.Z;
        var z1 = center.Z + he.Z;
        return new CadBlueprintShell
        {
            Id = e.Id,
            Name = e.Name ?? "Shell",
            Kind = "hull",
            Height = he.Y * 2,
            PlanRing =
            [
                [x0, 0, z0],
                [x1, 0, z0],
                [x1, 0, z1],
                [x0, 0, z1],
            ],
            SourceEntityId = e.Id,
        };
    }

    private static CadBlueprintSheet CreateDefaultSheet(CadBlueprint bp)
    {
        var sheet = new CadBlueprintSheet
        {
            Id = "GA-001",
            Title = bp.Name,
            Rev = "B",
            Orientation = bp.Context is CadBlueprintContexts.Spaceship
                or CadBlueprintContexts.SpaceStation
                or CadBlueprintContexts.SeagoingShip
                ? "iso128-15-stern-left"
                : "plan-north-up",
            Folders =
            [
                new() { Path = "chrome", Label = "Sheet chrome" },
                new() { Path = "exterior", Label = "Exteriors" },
                new() { Path = "structure", Label = "Walls" },
                new() { Path = "interior", Label = "Interiors" },
                new() { Path = "openings", Label = "Doors / hatches / holes" },
                new() { Path = "dims", Label = "Measurements" },
            ],
            Layers =
            [
                Layer("chrome.frame", "chrome/frame", "Frame", "chrome", locked: true),
                Layer("exterior.shell", "exterior/shell", "Shell / facade", "outline"),
                Layer("structure.walls", "structure/walls", "Walls", "structure"),
                Layer("interior.spaces", "interior/spaces", "Spaces", "space"),
                Layer("openings.all", "openings/all", "Openings", "opening"),
                Layer("dims.primary", "dims/primary", "Primary dims", "dimension"),
            ],
        };

        foreach (var level in bp.Levels)
        {
            sheet.Views.Add(new CadBlueprintView
            {
                Id = $"level-{level.Index}",
                Label = level.Name,
                Kind = "plan",
                LevelIndex = level.Index,
            });
        }

        sheet.Presets.Add(new CadBlueprintPreset
        {
            Id = "print",
            Label = "Print",
            ForPlot = true,
            VisibleLayerIds = sheet.Layers.Where(l => l.Plot).Select(l => l.Id).ToList(),
        });
        sheet.DefaultPresetId = "print";
        return sheet;
    }

    private static CadBlueprintLayer Layer(
        string id,
        string path,
        string label,
        string kind,
        bool locked = false,
        bool? plot = null) =>
        new()
        {
            Id = id,
            Path = path,
            Label = label,
            Kind = kind,
            Locked = locked,
            DefaultVisible = true,
            Plot = plot ?? kind is not "overlay",
        };
}

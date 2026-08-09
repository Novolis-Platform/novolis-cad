using Novolis.Cad.Primitives;

namespace Novolis.Cad.Blueprint;

/// <summary>
/// Projects a full <see cref="CadDocument"/> into a contextual <see cref="CadBlueprint"/>.
/// Only wall / space / opening (and optional hull-like solids tagged as shell) are lifted;
/// sketch noise, modifiers, and meshes stay in the CAD document.
/// </summary>
public static class CadBlueprintProjector
{
    public static CadBlueprint FromCadDocument(CadDocument doc, string? context = null, string? cadDocumentHref = null)
    {
        ArgumentNullException.ThrowIfNull(doc);

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

        foreach (var e in doc.Entities)
        {
            switch (e.Kind.ToLowerInvariant())
            {
                case "wall":
                    bp.Walls.Add(ToWall(e, layerName));
                    break;
                case "space":
                    bp.Spaces.Add(ToSpace(e, layerName));
                    break;
                case "opening":
                    bp.Openings.Add(ToOpening(e, layerName));
                    break;
                case "box" when LooksLikeShell(e):
                    bp.Shells.Add(ToShellFromBox(e));
                    break;
            }
        }

        bp.Sheets.Add(CreateDefaultSheet(bp));
        return bp;
    }

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
        if (e.Footprint is not { Count: >= 2 })
            return e.Thickness > 0 ? e.Thickness : 1f;
        var minX = e.Footprint.Min(p => p[0]);
        var maxX = e.Footprint.Max(p => p[0]);
        var minZ = e.Footprint.Min(p => p.Length > 2 ? p[2] : p[1]);
        var maxZ = e.Footprint.Max(p => p.Length > 2 ? p[2] : p[1]);
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

    private static CadBlueprintLayer Layer(string id, string path, string label, string kind, bool locked = false) =>
        new()
        {
            Id = id,
            Path = path,
            Label = label,
            Kind = kind,
            Locked = locked,
            DefaultVisible = true,
            Plot = kind is not "overlay",
        };
}

namespace Novolis.Cad.Blueprint;

/// <summary>Schema-level checks for <see cref="CadBlueprint"/> companion documents.</summary>
public static class CadBlueprintValidator
{
    public static IReadOnlyList<CadBlueprintDiagnostic> Validate(CadBlueprint blueprint)
    {
        ArgumentNullException.ThrowIfNull(blueprint);
        var list = new List<CadBlueprintDiagnostic>();

        if (!string.Equals(blueprint.Format, CadBlueprintJson.FormatId, StringComparison.Ordinal))
        {
            list.Add(Err("BP001", $"Format must be '{CadBlueprintJson.FormatId}', got '{blueprint.Format}'.", "format"));
        }

        if (blueprint.SchemaVersion < 1)
            list.Add(Err("BP002", $"SchemaVersion must be >= 1, got {blueprint.SchemaVersion}.", "schemaVersion"));

        if (string.IsNullOrWhiteSpace(blueprint.Name))
            list.Add(Warn("BP003", "Blueprint name is empty.", "name"));

        if (blueprint.UnitScaleMeters <= 0)
            list.Add(Err("BP004", "UnitScaleMeters must be > 0.", "unitScaleMeters"));

        var levelIndexes = blueprint.Levels.Select(l => l.Index).ToHashSet();
        if (blueprint.Levels.Count == 0)
            list.Add(Warn("BP010", "No levels defined.", "levels"));

        var levelIdDup = blueprint.Levels.GroupBy(l => l.Index).Where(g => g.Count() > 1).Select(g => g.Key);
        foreach (var idx in levelIdDup)
            list.Add(Err("BP011", $"Duplicate level index {idx}.", "levels"));

        CheckGuidDupes(list, blueprint.Walls.Select(w => w.Id), "walls", "BP020");
        CheckGuidDupes(list, blueprint.Spaces.Select(s => s.Id), "spaces", "BP021");
        CheckGuidDupes(list, blueprint.Openings.Select(o => o.Id), "openings", "BP022");
        CheckGuidDupes(list, blueprint.Shells.Select(s => s.Id), "shells", "BP023");

        var wallIds = blueprint.Walls.Select(w => w.Id).ToHashSet();
        var shellIds = blueprint.Shells.Select(s => s.Id).ToHashSet();

        foreach (var wall in blueprint.Walls)
        {
            if (!levelIndexes.Contains(wall.LevelIndex) && blueprint.Levels.Count > 0)
                list.Add(Warn("BP030", $"Wall '{wall.Name ?? wall.Id.ToString()}' level {wall.LevelIndex} not in levels[].", $"walls/{wall.Id}"));
            if (wall.A is null || wall.B is null)
                list.Add(Warn("BP031", $"Wall '{wall.Name ?? wall.Id.ToString()}' missing A/B endpoints.", $"walls/{wall.Id}"));
        }

        foreach (var space in blueprint.Spaces)
        {
            if (!levelIndexes.Contains(space.LevelIndex) && blueprint.Levels.Count > 0)
                list.Add(Warn("BP040", $"Space '{space.Name}' level {space.LevelIndex} not in levels[].", $"spaces/{space.Id}"));
            if (space.Footprint is null || space.Footprint.Count < 3)
                list.Add(Warn("BP041", $"Space '{space.Name}' missing plan footprint.", $"spaces/{space.Id}"));
        }

        foreach (var opening in blueprint.Openings)
        {
            if (!levelIndexes.Contains(opening.LevelIndex) && blueprint.Levels.Count > 0)
                list.Add(Warn("BP050", $"Opening '{opening.Name ?? opening.Id.ToString()}' level {opening.LevelIndex} not in levels[].", $"openings/{opening.Id}"));
            // Host refs are soft: CAD may lift openings before walls/shells exist in the companion.
            if (opening.HostWallId is { } hw && !wallIds.Contains(hw))
                list.Add(Warn("BP051", $"Opening '{opening.Name ?? opening.Id.ToString()}' hostWallId {hw} not found in walls[].", $"openings/{opening.Id}"));
            if (opening.HostShellId is { } hs && !shellIds.Contains(hs))
                list.Add(Warn("BP052", $"Opening '{opening.Name ?? opening.Id.ToString()}' hostShellId {hs} not found in shells[].", $"openings/{opening.Id}"));
            if (opening.HostWallId is null && opening.HostShellId is null)
                list.Add(Warn("BP053", $"Opening '{opening.Name ?? opening.Id.ToString()}' has no host wall/shell.", $"openings/{opening.Id}"));
        }

        foreach (var shell in blueprint.Shells)
        {
            if (shell.PlanRing is null || shell.PlanRing.Count < 3)
                list.Add(Warn("BP060", $"Shell '{shell.Name}' missing plan ring.", $"shells/{shell.Id}"));
        }

        if (blueprint.Walls.Count == 0 && blueprint.Spaces.Count == 0 && blueprint.Shells.Count == 0)
            list.Add(Warn("BP070", "Blueprint has no walls, spaces, or shells.", null));

        var sheetIds = new HashSet<string>(StringComparer.Ordinal);
        for (var si = 0; si < blueprint.Sheets.Count; si++)
        {
            var sheet = blueprint.Sheets[si];
            var prefix = $"sheets[{si}]";
            if (string.IsNullOrWhiteSpace(sheet.Id))
            {
                list.Add(Err("BP080", "Sheet id is empty.", prefix));
                continue;
            }

            if (!sheetIds.Add(sheet.Id))
                list.Add(Err("BP081", $"Duplicate sheet id '{sheet.Id}'.", prefix));

            var layerIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var layer in sheet.Layers)
            {
                if (string.IsNullOrWhiteSpace(layer.Id))
                {
                    list.Add(Err("BP082", $"Sheet '{sheet.Id}' has a layer with empty id.", $"{prefix}/layers"));
                    continue;
                }

                if (!layerIds.Add(layer.Id))
                    list.Add(Err("BP083", $"Sheet '{sheet.Id}' duplicate layer id '{layer.Id}'.", $"{prefix}/layers"));
            }

            var folderPaths = sheet.Folders.Select(f => f.Path).ToHashSet(StringComparer.Ordinal);
            foreach (var layer in sheet.Layers)
            {
                if (string.IsNullOrWhiteSpace(layer.Path))
                    continue;
                var root = layer.Path.Split('/')[0];
                if (folderPaths.Count > 0 && !folderPaths.Contains(root) && !folderPaths.Contains(layer.Path))
                    list.Add(Warn("BP084", $"Sheet '{sheet.Id}' layer '{layer.Id}' path root '{root}' not in folders[].", $"{prefix}/layers"));
            }

            var presetIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var preset in sheet.Presets)
            {
                if (string.IsNullOrWhiteSpace(preset.Id))
                {
                    list.Add(Err("BP085", $"Sheet '{sheet.Id}' has a preset with empty id.", $"{prefix}/presets"));
                    continue;
                }

                if (!presetIds.Add(preset.Id))
                    list.Add(Err("BP086", $"Sheet '{sheet.Id}' duplicate preset id '{preset.Id}'.", $"{prefix}/presets"));

                foreach (var lid in preset.VisibleLayerIds)
                {
                    if (!layerIds.Contains(lid))
                        list.Add(Err("BP087", $"Sheet '{sheet.Id}' preset '{preset.Id}' references unknown layer '{lid}'.", $"{prefix}/presets"));
                }
            }

            if (sheet.DefaultPresetId is { } def && !presetIds.Contains(def) && sheet.Presets.Count > 0)
                list.Add(Err("BP088", $"Sheet '{sheet.Id}' defaultPresetId '{def}' not found.", prefix));
        }

        return list;
    }

    public static bool HasErrors(IEnumerable<CadBlueprintDiagnostic> diagnostics) =>
        diagnostics.Any(d => d.Severity == CadBlueprintDiagnosticSeverity.Error);

    private static void CheckGuidDupes(
        List<CadBlueprintDiagnostic> list,
        IEnumerable<Guid> ids,
        string collection,
        string code)
    {
        foreach (var g in ids.GroupBy(x => x).Where(g => g.Count() > 1 && g.Key != Guid.Empty))
            list.Add(Err(code, $"Duplicate id {g.Key} in {collection}.", collection));
    }

    private static CadBlueprintDiagnostic Err(string code, string message, string? path) =>
        new(CadBlueprintDiagnosticSeverity.Error, code, message, path);

    private static CadBlueprintDiagnostic Warn(string code, string message, string? path) =>
        new(CadBlueprintDiagnosticSeverity.Warning, code, message, path);
}

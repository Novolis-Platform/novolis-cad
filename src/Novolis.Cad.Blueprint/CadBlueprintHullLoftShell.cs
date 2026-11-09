using System.Text.Json;
using Novolis.Cad.Primitives;

namespace Novolis.Cad.Blueprint;

/// <summary>
/// Optional hull outline lift from <c>CadDocument.Properties["hullLoft"]</c>
/// (stations with <c>zFromStem</c> + <c>halfBeam</c>) — used by seagoing-ship companions.
/// </summary>
public static class CadBlueprintHullLoftShell
{
    public static bool TryAddFromDocument(CadBlueprint blueprint, CadDocument doc, List<CadBlueprintDiagnostic>? diagnostics = null)
    {
        ArgumentNullException.ThrowIfNull(blueprint);
        ArgumentNullException.ThrowIfNull(doc);

        if (blueprint.Shells.Count > 0)
            return false;

        if (doc.Properties is null || !doc.Properties.TryGetValue("hullLoft", out var hl))
            return false;

        if (!TryParseStations(hl, out var stations) || stations.Count < 2)
        {
            diagnostics?.Add(new CadBlueprintDiagnostic(
                CadBlueprintDiagnosticSeverity.Warning,
                "BP210",
                "hullLoft present but stations could not be parsed for shell outline.",
                "properties/hullLoft"));
            return false;
        }

        var loa = ResolveLoa(doc, stations);
        var ring = new List<float[]>(stations.Count * 2);
        // CAD: +X starboard, +Z bow-ward from midships (Z = LOA/2 − zFromStem).
        foreach (var st in stations)
        {
            var worldZ = loa * 0.5f - st.ZFromStem;
            ring.Add([st.HalfBeam, 0f, worldZ]);
        }

        for (var i = stations.Count - 1; i >= 0; i--)
        {
            var st = stations[i];
            var worldZ = loa * 0.5f - st.ZFromStem;
            ring.Add([-st.HalfBeam, 0f, worldZ]);
        }

        blueprint.Shells.Add(new CadBlueprintShell
        {
            Id = Guid.Parse("a1000000-0000-4000-8000-0000000000a1"),
            Name = "Hull OML (loft)",
            Kind = "hull",
            PlanRing = ring,
            Height = ResolveOah(doc),
        });

        diagnostics?.Add(new CadBlueprintDiagnostic(
            CadBlueprintDiagnosticSeverity.Warning,
            "BP211",
            $"Lifted hull shell outline from hullLoft ({stations.Count} stations).",
            "shells"));
        return true;
    }

    private static float ResolveLoa(CadDocument doc, IReadOnlyList<LoftSample> stations)
    {
        if (doc.Properties is not null && doc.Properties.TryGetValue("loa", out var loaEl)
            && TryFloat(loaEl, out var loa) && loa > 1f)
            return loa;
        return stations.Max(s => s.ZFromStem);
    }

    private static float? ResolveOah(CadDocument doc)
    {
        if (doc.Properties is not null && doc.Properties.TryGetValue("oah", out var oahEl)
            && TryFloat(oahEl, out var oah) && oah > 0f)
            return oah;
        return null;
    }

    private static bool TryParseStations(JsonElement hl, out List<LoftSample> stations)
    {
        stations = [];
        if (hl.ValueKind != JsonValueKind.Object)
            return false;
        if (!hl.TryGetProperty("stations", out var sts) || sts.ValueKind != JsonValueKind.Array)
            return false;

        foreach (var st in sts.EnumerateArray())
        {
            if (!st.TryGetProperty("zFromStem", out var zEl) || !TryFloat(zEl, out var z))
                continue;
            if (!st.TryGetProperty("halfBeam", out var hbEl) || !TryFloat(hbEl, out var hb))
                continue;
            stations.Add(new LoftSample(z, hb));
        }

        stations.Sort((a, b) => a.ZFromStem.CompareTo(b.ZFromStem));
        return stations.Count >= 2;
    }

    private static bool TryFloat(JsonElement el, out float value)
    {
        if (el.ValueKind == JsonValueKind.Number && el.TryGetDouble(out var d))
        {
            value = (float)d;
            return true;
        }

        value = 0;
        return false;
    }

    private readonly record struct LoftSample(float ZFromStem, float HalfBeam);
}

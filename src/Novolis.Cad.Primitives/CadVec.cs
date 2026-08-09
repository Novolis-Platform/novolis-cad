using System.Numerics;
using Novolis.Math.Geometry;

namespace Novolis.Cad.Primitives;

public static class CadVec
{
    public static float[] Xz(float x, float z) => [x, 0f, z];

    public static float[] Xyz(float x, float y, float z) => [x, y, z];

    public static float[] Plan(float x, float z, float elevation) => [x, elevation, z];

    public static float[] From(Vector3 v) => [v.X, v.Y, v.Z];

    public static Vector3 To(float[]? v, Vector3 fallback = default) =>
        v is { Length: >= 3 } ? new Vector3(v[0], v[1], v[2]) : fallback;

    public static Vector3 OnPlane(float x, float z, float elevation) => new(x, elevation, z);

    public const float DeckHeightMeters = 3.6f;

    /// <summary>True when the document uses decked walls/spaces (ship CAD).</summary>
    public static bool LooksLikeShipDocument(CadDocument document) =>
        document.Entities.Count > 0
        && document.Entities.Count(e => e.Kind is "wall" or "space") >= System.Math.Min(8, document.Entities.Count);

    /// <summary>Deck index implied by drawing elevation (0, ±3.6, … → deck 0, ±1, …).</summary>
    public static int DeckFromElevation(float elevation) =>
        (int)MathF.Round(elevation / DeckHeightMeters);

    /// <summary>Representative elevation (world Y) for level filtering.</summary>
    public static float ElevationOf(CadEntity entity) => entity.Kind.ToLowerInvariant() switch
    {
        "wall" or "space" or "opening" => entity.Deck * DeckHeightMeters,
        "line" => To(entity.A).Y,
        "rect" => To(entity.A).Y,
        "circle" or "arc" => To(entity.Center).Y,
        "spline" when entity.FitPoints is { Count: > 0 } => To(entity.FitPoints[0]).Y,
        "spline" when entity.ControlPoints is { Count: > 0 } => To(entity.ControlPoints[0]).Y,
        "box" when CadShipGeometry.TryGetBox(entity, out var boxCenter, out _) =>
            // Prefer deck banding for stamped ship boxes (incl. deck 0). Analytic CAD boxes keep geometric Y.
            IsDeckBandedBox(entity)
                ? entity.Deck * DeckHeightMeters
                : boxCenter.Y,
        "box" or "wedge" or "cylinder" or "cone" or "sphere" => To(entity.Center).Y,
        _ => To(entity.Center).Y,
    };

    public static bool MatchesLevel(CadEntity entity, float elevation, float tolerance)
    {
        var kind = entity.Kind.ToLowerInvariant();
        if (kind is "wall" or "space" or "opening" || (kind == "box" && IsDeckBandedBox(entity)))
        {
            return entity.Deck == DeckFromElevation(elevation);
        }

        return MathF.Abs(ElevationOf(entity) - elevation) <= System.Math.Max(0.001f, tolerance);
    }

    /// <summary>
    /// Ship volumes stamp <see cref="CadEntity.Points"/> and/or opening metadata so deck 0 is not
    /// mistaken for an unset deck (geometric center Y would fail IsolateLevel at elevation 0).
    /// </summary>
    public static bool IsDeckBandedBox(CadEntity entity) =>
        entity.Points is { Count: >= 2 }
        || !string.IsNullOrWhiteSpace(entity.OpeningType)
        || (entity.Properties is not null && entity.Properties.ContainsKey("shipDeckBanded"));

    public static void Translate(float[]? v, float dx, float dy, float dz)
    {
        if (v is not { Length: >= 3 })
            return;
        v[0] += dx;
        v[1] += dy;
        v[2] += dz;
    }

    public static void TranslateAll(IList<float[]>? points, float dx, float dy, float dz)
    {
        if (points is null)
            return;
        foreach (var p in points)
            Translate(p, dx, dy, dz);
    }

    public static Guid EnsureDefaultLayer(CadDocument doc)
    {
        if (doc.Layers.Count == 0)
        {
            doc.Layers.Add(new CadLayer
            {
                Id = Guid.Parse("a0000000-0000-4000-8000-000000000001"),
                Name = "0",
                Visible = true,
                Color = [0.8f, 0.8f, 0.8f],
            });
        }

        return doc.Layers[0].Id;
    }

    public static CadEntity WithLayer(this CadEntity entity, CadDocument doc)
    {
        entity.LayerId ??= EnsureDefaultLayer(doc);
        return entity;
    }

    public static IEnumerable<Vector3> EnumerateWorldPoints(CadEntity entity)
    {
        switch (entity.Kind.ToLowerInvariant())
        {
            case "line":
                yield return To(entity.A);
                yield return To(entity.B);
                break;
            case "circle" or "sphere" or "arc":
                var c = To(entity.Center);
                yield return c + new Vector3(entity.Radius, 0, 0);
                yield return c - new Vector3(entity.Radius, 0, 0);
                yield return c + new Vector3(0, 0, entity.Radius);
                yield return c - new Vector3(0, 0, entity.Radius);
                if (entity.Kind is "sphere" or "cylinder" or "cone")
                {
                    yield return c + new Vector3(0, entity.Radius, 0);
                    yield return c - new Vector3(0, entity.Radius, 0);
                }

                break;
            case "rect":
                if (entity.A is not null && entity.B is not null)
                {
                    yield return To(entity.A);
                    yield return To(entity.B);
                }
                else if (entity.Min is not null && entity.Max is not null)
                {
                    yield return To(entity.Min);
                    yield return To(entity.Max);
                }
                else if (entity.Center is not null && entity.HalfExtents is { Length: >= 2 })
                {
                    var center = To(entity.Center);
                    var hx = entity.HalfExtents[0];
                    var hz = entity.HalfExtents.Length > 2 ? entity.HalfExtents[2] : entity.HalfExtents[1];
                    yield return center + new Vector3(hx, 0, hz);
                    yield return center - new Vector3(hx, 0, hz);
                }

                break;
            case "spline":
                if (entity.ControlPoints is { Count: > 0 } cps && entity.Knots is { Length: > 0 })
                {
                    foreach (var p in NurbsCurve.Tessellate(
                                 entity.Degree <= 0 ? 3 : entity.Degree,
                                 cps.Select(p => To(p)).ToArray(),
                                 entity.Knots,
                                 entity.Weights,
                                 sampleCount: 32))
                        yield return p;
                }
                else if (entity.FitPoints is { Count: > 0 })
                {
                    foreach (var p in entity.FitPoints)
                        yield return To(p);
                }

                break;
            case "box" or "wedge":
                if (CadShipGeometry.TryGetBox(entity, out var boxCenter, out var he))
                {
                    yield return boxCenter + he;
                    yield return boxCenter - he;
                }

                break;
            case "wall":
                if (entity.A is not null)
                    yield return To(entity.A);
                if (entity.B is not null)
                    yield return To(entity.B) + new Vector3(0, System.Math.Max(0f, entity.Height), 0);
                if (entity.Points is not null)
                {
                    foreach (var p in entity.Points)
                        yield return To(p);
                }

                break;
            case "space":
                if (entity.Points is not null)
                {
                    foreach (var p in entity.Points)
                    {
                        var v = To(p);
                        yield return v;
                        yield return v + new Vector3(0, System.Math.Max(0f, entity.Height), 0);
                    }
                }

                break;
            case "opening":
                if (entity.Footprint is not null)
                {
                    foreach (var p in entity.Footprint)
                        yield return To(p);
                }
                else if (entity.Points is not null)
                {
                    foreach (var p in entity.Points)
                        yield return To(p);
                }
                else if (entity.A is not null && entity.B is not null)
                {
                    yield return To(entity.A);
                    yield return To(entity.B);
                }

                break;
            case "cylinder" or "cone":
                if (entity.Center is not null)
                {
                    var center = To(entity.Center);
                    var hy = entity.Height * 0.5f;
                    yield return center + new Vector3(entity.Radius, hy, entity.Radius);
                    yield return center - new Vector3(entity.Radius, hy, entity.Radius);
                }

                break;
            case "polyline" when entity.Points is not null:
                foreach (var p in entity.Points)
                    yield return To(p);
                break;
        }
    }

    public static void TranslateEntity(CadEntity entity, float dx, float dy, float dz)
    {
        Translate(entity.A, dx, dy, dz);
        Translate(entity.B, dx, dy, dz);
        Translate(entity.Center, dx, dy, dz);
        Translate(entity.Min, dx, dy, dz);
        Translate(entity.Max, dx, dy, dz);
        TranslateAll(entity.Points, dx, dy, dz);
        TranslateAll(entity.ControlPoints, dx, dy, dz);
        TranslateAll(entity.FitPoints, dx, dy, dz);
    }
}
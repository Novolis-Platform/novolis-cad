using System.Numerics;

namespace Novolis.Cad.Primitives;

/// <summary>
/// Splits wall baselines around opening footprints so doors/hatches persist as real gaps in .cadjson.
/// </summary>
public static class OpeningDerivation
{
    public static void Apply(List<CadEntity> entities)
    {
        var openings = entities.Where(e => e.Kind == "opening" && e.Footprint is { Count: >= 3 }).ToList();
        if (openings.Count == 0)
            return;

        var walls = entities.Where(e => e.Kind == "wall" && e.A is { Length: >= 3 } && e.B is { Length: >= 3 }).ToList();
        var toAdd = new List<CadEntity>();
        var toRemove = new HashSet<Guid>();

        foreach (var opening in openings)
        {
            var host = ResolveHost(opening, walls);
            if (host is null)
                continue;

            opening.HostWallId = host.Id;
            if (!TrySplitWall(host, opening, out var left, out var right))
                continue;

            toRemove.Add(host.Id);
            if (left is not null)
                toAdd.Add(left);
            if (right is not null)
                toAdd.Add(right);

            // Retarget subsequent openings that pointed at the removed wall.
            walls.Remove(host);
            if (left is not null)
                walls.Add(left);
            if (right is not null)
                walls.Add(right);
        }

        if (toRemove.Count == 0)
            return;

        entities.RemoveAll(e => toRemove.Contains(e.Id));
        entities.AddRange(toAdd);
    }

    private static CadEntity? ResolveHost(CadEntity opening, List<CadEntity> walls)
    {
        if (opening.HostWallId is { } id)
            return walls.FirstOrDefault(w => w.Id == id);

        var c = FootprintCenter(opening);
        CadEntity? best = null;
        var bestDist = float.MaxValue;
        foreach (var wall in walls.Where(w => w.Deck == opening.Deck))
        {
            var a = CadVec.To(wall.A);
            var b = CadVec.To(wall.B);
            var d = PointSegmentDistance(c, a, b);
            if (d < bestDist)
            {
                bestDist = d;
                best = wall;
            }
        }

        return bestDist < 2.5f ? best : null;
    }

    private static bool TrySplitWall(CadEntity wall, CadEntity opening, out CadEntity? left, out CadEntity? right)
    {
        left = null;
        right = null;
        var a = CadVec.To(wall.A);
        var b = CadVec.To(wall.B);
        var dir = b - a;
        dir.Y = 0;
        var len = dir.Length();
        if (len < 0.4f)
            return false;
        dir /= len;

        var c = FootprintCenter(opening);
        var t = Vector3.Dot(c - a, dir);
        t = System.Math.Clamp(t, 0f, len);

        var halfGap = EstimateHalfGap(opening, dir);
        halfGap = System.Math.Clamp(halfGap, 0.4f, System.Math.Min(2.2f, len * 0.45f));

        var t0 = System.Math.Max(0f, t - halfGap);
        var t1 = System.Math.Min(len, t + halfGap);
        if (t1 - t0 < 0.3f)
            return false;

        if (t0 > 0.15f)
        {
            left = CloneWall(wall, $"{wall.Name}-L", a, a + dir * t0);
        }

        if (len - t1 > 0.15f)
        {
            right = CloneWall(wall, $"{wall.Name}-R", a + dir * t1, b);
        }

        return left is not null || right is not null;
    }

    private static float EstimateHalfGap(CadEntity opening, Vector3 wallDir)
    {
        var fp = opening.Footprint!;
        var pts = fp.Select(p => CadVec.To(p)).ToArray();
        var min = pts[0];
        var max = pts[0];
        foreach (var p in pts)
        {
            min = Vector3.Min(min, p);
            max = Vector3.Max(max, p);
        }

        var size = max - min;
        // Project footprint extent onto wall direction.
        var along = System.Math.Abs(wallDir.X) >= System.Math.Abs(wallDir.Z) ? size.X : size.Z;
        return System.Math.Max(0.5f, along * 0.5f);
    }

    private static CadEntity CloneWall(CadEntity src, string name, Vector3 a, Vector3 b) =>
        new()
        {
            Id = Guid.NewGuid(),
            Kind = "wall",
            Name = name,
            LayerId = src.LayerId,
            Deck = src.Deck,
            Thickness = src.Thickness,
            Height = src.Height,
            A = CadVec.From(a),
            B = CadVec.From(b),
            Sides = src.Sides,
            Properties = src.Properties,
            ParentId = src.Id,
        };

    private static Vector3 FootprintCenter(CadEntity opening)
    {
        var sum = Vector3.Zero;
        foreach (var p in opening.Footprint!)
            sum += CadVec.To(p);
        return sum / opening.Footprint.Count;
    }

    private static float PointSegmentDistance(Vector3 p, Vector3 a, Vector3 b)
    {
        var ab = b - a;
        ab.Y = 0;
        var len2 = ab.LengthSquared();
        if (len2 < 1e-8f)
            return Vector3.Distance(new Vector3(p.X, 0, p.Z), new Vector3(a.X, 0, a.Z));
        var t = System.Math.Clamp(Vector3.Dot(p - a, ab) / len2, 0f, 1f);
        var proj = a + ab * t;
        return Vector3.Distance(new Vector3(p.X, 0, p.Z), new Vector3(proj.X, 0, proj.Z));
    }
}

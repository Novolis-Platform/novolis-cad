using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Novolis.Cad.Primitives;

/// <summary>Shared helpers for Draft Studio / ship entity encodings.</summary>
public static class CadShipGeometry
{
    /// <summary>
    /// Box pose: either analytic <c>center</c>+<c>halfExtents</c>, or ship
    /// <c>points[0]=center</c> + <c>points[1]=halfExtents</c> (with thickness/height fallback).
    /// </summary>
    public static bool TryGetBox(CadEntity entity, out Vector3 center, out Vector3 halfExtents)
    {
        center = default;
        halfExtents = default;
        if (entity.Center is not null && entity.HalfExtents is { Length: >= 3 })
        {
            center = CadVec.To(entity.Center);
            halfExtents = CadVec.To(entity.HalfExtents);
            return true;
        }

        if (entity.Points is { Count: >= 2 })
        {
            center = CadVec.To(entity.Points[0]);
            halfExtents = CadVec.To(entity.Points[1]);
            if (halfExtents.LengthSquared() < 1e-8f)
            {
                var hx = entity.Thickness > 0 ? entity.Thickness * 0.5f : 0.5f;
                var hy = entity.Height > 0 ? entity.Height * 0.5f : hx;
                halfExtents = new Vector3(hx, hy, hx);
            }

            return true;
        }

        return false;
    }
}

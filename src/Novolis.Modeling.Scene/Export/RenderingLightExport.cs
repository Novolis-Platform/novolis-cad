using System.Numerics;

namespace Novolis.Modeling.Scene;

/// <summary>Optional bridge toward <c>Novolis.Rendering.Scene.LightDefinition</c> (Directional/Point).</summary>
public static class RenderingLightExport
{
    public readonly record struct ExportedLight(
        string Kind,
        Vector3 DirectionOrPosition,
        Vector3 Color,
        float Intensity);

    /// <summary>
    /// Maps modeling lights to a Rendering-compatible bag.
    /// Omni → Point; Infinite → Directional; Spot/Area approximate as Point until Rendering gains kinds.
    /// </summary>
    public static IReadOnlyList<ExportedLight> Export(LookCache cache)
    {
        ArgumentNullException.ThrowIfNull(cache);
        var list = new List<ExportedLight>();
        foreach (var ev in cache.Lights)
        {
            if (ev.Source is not LightNode light || !light.Enabled)
                continue;

            var color = new Vector3(light.Color[0], light.Color[1], light.Color[2]);
            switch (light.LightKind)
            {
                case LightKind.Infinite:
                {
                    var forward = Vector3.TransformNormal(-Vector3.UnitZ, ev.WorldMatrix);
                    if (forward.LengthSquared() < 1e-8f)
                        forward = -Vector3.UnitY;
                    else
                        forward = Vector3.Normalize(forward);
                    list.Add(new ExportedLight("Directional", forward, color, light.Intensity));
                    break;
                }
                default:
                    list.Add(new ExportedLight("Point", ev.WorldPosition, color, light.Intensity));
                    break;
            }
        }

        return list;
    }
}

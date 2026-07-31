using Novolis.Cad.Primitives;
using Novolis.Math.Geometry;
using Novolis.Cad.SceneBridge.Tessellation;

namespace Novolis.Cad.SceneBridge;

/// <summary>Dispatches entity kind to the appropriate tessellator.</summary>
public static class CadEntityTessellator
{
    public static EditableMesh? TryTessellate(CadEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        return entity.Kind.ToLowerInvariant() switch
        {
            "wall" => CadWallTessellator.TryTessellate(entity),
            "space" => CadSpaceTessellator.TryTessellate(entity),
            _ => CadSolidTessellator.TryTessellate(entity),
        };
    }
}

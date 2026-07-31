namespace Novolis.Modeling.Import;

/// <summary>Options for Assimp mesh import and optional scene framing helpers.</summary>
public sealed class MeshImportOptions
{
    /// <summary>When set (&gt; 0), uniform-scale so the longest AABB axis equals this length (meters).</summary>
    public float? TargetLengthMeters { get; init; }

    /// <summary>Translate so AABB center sits at the origin after scale/orient.</summary>
    public bool CenterAtOrigin { get; init; } = true;

    /// <summary>
    /// After normalize, rotate so the longest AABB axis aligns with +Z (SceneLab ship forward).
    /// </summary>
    public bool LongestAxisToPositiveZ { get; init; }

    /// <summary>
    /// Flatten node transforms into mesh vertices (recommended for FBX hierarchies).
    /// </summary>
    public bool PreTransformVertices { get; init; } = true;

    /// <summary>Generate missing normals.</summary>
    public bool GenerateNormals { get; init; } = true;

    /// <summary>Assimp mesh optimize pass.</summary>
    public bool OptimizeMeshes { get; init; } = true;
}

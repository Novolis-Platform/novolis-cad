using System.Numerics;
using Novolis.Cad.Primitives;
using Novolis.Math.Geometry;
using Novolis.Cad.SceneBridge.Tessellation;

namespace Novolis.Cad.Evaluation;

public sealed class CadEvaluationCache
{
    public Dictionary<Guid, EditableMesh> CadMeshes { get; } = new();

    public Dictionary<Guid, EditableMesh> ModeledMeshes { get; } = new();

    public List<EvaluatedInstance> Instances { get; } = [];

    public List<CadEntity> Lights { get; } = [];

    public List<CadEntity> Cameras { get; } = [];

    public List<CadEntity> Materials { get; } = [];

    public int CadRevision { get; set; }

    public int MeshRevision { get; set; }

    public int PreviewRevision { get; set; }
}

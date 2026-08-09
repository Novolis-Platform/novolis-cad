using System.Numerics;
using Novolis.Cad.Primitives;
using Novolis.Math.Geometry;
using Novolis.Cad.SceneBridge.Tessellation;

namespace Novolis.Cad.Evaluation;

public enum CadEvalStage
{
    Cad,
    Mesh,
    Modeling,
    Scene,
    Preview,
}

public sealed record EvaluatedInstance(Guid SourceId, Matrix4x4 Transform, EditableMesh? Mesh);

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

/// <summary>Staged evaluator: CAD solids → MeshFromSolid → modifiers → instances → preview nodes.</summary>
public sealed class CadModelEvaluator
{
    private readonly CadEvaluationCache _cache = new();
    private int _docStamp = -1;
    private CadEvalStage _dirtyFrom = CadEvalStage.Cad;

    public CadEvaluationCache Cache => _cache;

    public void Invalidate(CadEvalStage from = CadEvalStage.Cad) =>
        _dirtyFrom = (CadEvalStage)System.Math.Min((int)_dirtyFrom, (int)from);

    public CadEvaluationCache Evaluate(CadDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var stamp = document.Entities.Count;
        unchecked
        {
            stamp ^= document.ModifiedAt?.GetHashCode() ?? 0;
            foreach (var e in document.Entities)
                stamp ^= e.Id.GetHashCode();
        }
        if (stamp != _docStamp)
        {
            _dirtyFrom = CadEvalStage.Cad;
            _docStamp = stamp;
        }

        if (_dirtyFrom <= CadEvalStage.Cad)
            RebuildCad(document);
        if (_dirtyFrom <= CadEvalStage.Mesh || _dirtyFrom <= CadEvalStage.Modeling)
            RebuildMeshAndModifiers(document);
        if (_dirtyFrom <= CadEvalStage.Scene)
            RebuildScene(document);
        if (_dirtyFrom <= CadEvalStage.Preview)
            RebuildPreview(document);

        _dirtyFrom = (CadEvalStage)999;
        return _cache;
    }

    private void RebuildCad(CadDocument document)
    {
        _cache.CadMeshes.Clear();
        var byId = document.Entities.ToDictionary(e => e.Id);

        // Leaf solids first, then generators in dependency order (nested boolean/symmetry/connect).
        foreach (var entity in OrderForCadEvaluation(document.Entities))
        {
            var kind = entity.Kind.ToLowerInvariant();
            if (kind is "boolean")
            {
                var leftId = entity.TargetId ?? entity.LeftId;
                var rightId = entity.CutterId ?? entity.RightId;
                if (leftId is null || rightId is null)
                    continue;
                if (!TryResolveMesh(byId, leftId.Value, out var left) || !TryResolveMesh(byId, rightId.Value, out var right))
                    continue;
                var op = (entity.Operation ?? "union").ToLowerInvariant() switch
                {
                    "subtract" or "difference" => MeshBooleanKind.Difference,
                    "intersect" or "intersection" => MeshBooleanKind.Intersection,
                    _ => MeshBooleanKind.Union,
                };
                _cache.CadMeshes[entity.Id] = MeshBoolean.ApplySolid(left!, right!, op);
                continue;
            }

            if (kind is "symmetry")
            {
                var sourceId = entity.SourceId ?? entity.PrototypeId;
                if (sourceId is null || !TryResolveMesh(byId, sourceId.Value, out var source))
                    continue;
                var plane = ResolvePlane(entity);
                var mirrored = source!.Mirror(plane);
                if (entity.MergeAtPlane)
                {
                    var fused = MeshBoolean.Concat(source, mirrored);
                    if (entity.MergeTolerance > 0)
                        MeshWeld.Apply(fused, new WeldOptions(entity.MergeTolerance));
                    _cache.CadMeshes[entity.Id] = fused;
                }
                else
                    _cache.CadMeshes[entity.Id] = MeshBoolean.Concat(source, mirrored);
                continue;
            }

            if (kind is "split")
            {
                var sourceId = entity.SourceId ?? entity.LeftId;
                if (sourceId is null || !TryResolveMesh(byId, sourceId.Value, out var source))
                    continue;
                var plane = ResolvePlane(entity);
                var split = MeshPlaneSplit.Split(source!, plane);
                // Keep positive half as the evaluated result of the split node
                _cache.CadMeshes[entity.Id] = split.Positive;
                continue;
            }

            if (kind is "connect")
            {
                var mode = (entity.Mode ?? "group").ToLowerInvariant();
                if (mode is "group")
                    continue;
                var members = ResolveMembers(entity, byId);
                if (members.Count == 0)
                    continue;
                EditableMesh? acc = null;
                foreach (var m in members)
                    acc = acc is null ? m.Clone() : MeshBoolean.Concat(acc, m);

                if (acc is not null && mode is "fusesolid")
                    MeshWeld.Apply(acc, new WeldOptions(entity.TouchEpsilonMeters ?? 1e-4f));

                if (acc is not null)
                    _cache.CadMeshes[entity.Id] = acc;
                continue;
            }

            var tess = CadSolidTessellator.TryTessellate(entity);
            if (tess is not null)
                _cache.CadMeshes[entity.Id] = tess;
        }

        _cache.CadRevision++;
    }

    /// <summary>Solids first, then CAD generators ordered so operands evaluate before consumers.</summary>
    public static List<CadEntity> OrderForCadEvaluation(IReadOnlyList<CadEntity> entities)
    {
        var byId = entities.ToDictionary(e => e.Id);
        var isGenerator = new HashSet<Guid>();
        var deps = new Dictionary<Guid, HashSet<Guid>>();

        foreach (var entity in entities)
        {
            var kind = entity.Kind.ToLowerInvariant();
            if (kind is not ("boolean" or "symmetry" or "split" or "connect"))
                continue;
            isGenerator.Add(entity.Id);
            var set = new HashSet<Guid>();
            void Add(Guid? id)
            {
                if (id is { } g && byId.ContainsKey(g))
                    set.Add(g);
            }

            if (kind is "boolean")
            {
                Add(entity.TargetId ?? entity.LeftId);
                Add(entity.CutterId ?? entity.RightId);
            }
            else if (kind is "symmetry" or "split")
            {
                Add(entity.SourceId ?? entity.PrototypeId ?? entity.LeftId);
            }
            else if (kind is "connect" && entity.MemberIds is not null)
            {
                foreach (var mid in entity.MemberIds)
                    Add(mid);
            }

            deps[entity.Id] = set;
        }

        var ordered = new List<CadEntity>(entities.Count);
        foreach (var entity in entities)
        {
            if (!isGenerator.Contains(entity.Id))
                ordered.Add(entity);
        }

        var pending = entities.Where(e => isGenerator.Contains(e.Id)).ToList();
        var done = new HashSet<Guid>(ordered.Select(e => e.Id));
        while (pending.Count > 0)
        {
            var progressed = false;
            for (var i = 0; i < pending.Count; i++)
            {
                var e = pending[i];
                if (!deps[e.Id].All(d => done.Contains(d) || !isGenerator.Contains(d)))
                    continue;
                ordered.Add(e);
                done.Add(e.Id);
                pending.RemoveAt(i);
                progressed = true;
                break;
            }

            if (!progressed)
            {
                // Cycle / missing dep — append remaining in document order
                ordered.AddRange(pending);
                break;
            }
        }

        return ordered;
    }

    private void RebuildMeshAndModifiers(CadDocument document)
    {
        _cache.ModeledMeshes.Clear();
        var byId = document.Entities.ToDictionary(e => e.Id);

        // Seed modeled from CAD meshes and baked/detached stores
        foreach (var entity in document.Entities)
        {
            var kind = entity.Kind.ToLowerInvariant();
            if (kind is "meshfromsolid")
            {
                var sourceId = entity.SourceId ?? entity.InputId;
                var link = (entity.LinkMode ?? "linked").ToLowerInvariant();
                if ((link is "baked" or "detached") && entity.MeshVertices is { Count: > 0 })
                {
                    _cache.ModeledMeshes[entity.Id] = CadSolidTessellator.FromStored(entity);
                    continue;
                }

                if (sourceId is not null && _cache.CadMeshes.TryGetValue(sourceId.Value, out var src))
                {
                    var copy = src.Clone();
                    _cache.ModeledMeshes[entity.Id] = copy;
                    if (link is "detached")
                        CadSolidTessellator.StoreOnEntity(entity, copy);
                }

                continue;
            }

            if (_cache.CadMeshes.TryGetValue(entity.Id, out var cadMesh))
                _cache.ModeledMeshes[entity.Id] = cadMesh;
        }

        // Apply modifier stack nodes (inputId → result on node id)
        foreach (var entity in document.Entities.OrderBy(Depth))
        {
            var kind = entity.Kind.ToLowerInvariant();
            var inputId = entity.InputId ?? entity.SourceId;
            if (inputId is null)
                continue;
            if (!_cache.ModeledMeshes.TryGetValue(inputId.Value, out var input)
                && !_cache.CadMeshes.TryGetValue(inputId.Value, out input))
                continue;

            EditableMesh? result = kind switch
            {
                "weld" => ApplyWeld(input!.Clone(), entity),
                "optimize" => MeshOptimize.Apply(input!.Clone()).Mesh,
                "bridge" => input, // bridge needs explicit loops via session action
                _ => null,
            };
            if (result is not null)
                _cache.ModeledMeshes[entity.Id] = result;
        }

        _cache.MeshRevision++;

        int Depth(CadEntity e)
        {
            var d = 0;
            var cur = e.InputId ?? e.SourceId ?? e.ParentId;
            while (cur is { } id && byId.TryGetValue(id, out var p) && d < 64)
            {
                d++;
                cur = p.InputId ?? p.SourceId ?? p.ParentId;
            }

            return d;
        }
    }

    private void RebuildScene(CadDocument document)
    {
        _cache.Instances.Clear();
        var byId = document.Entities.ToDictionary(e => e.Id);

        foreach (var entity in document.Entities)
        {
            var kind = entity.Kind.ToLowerInvariant();
            if (kind is "instance")
            {
                if (entity.PrototypeId is null)
                    continue;
                var xf = ToMatrix(entity.Transform);
                _cache.ModeledMeshes.TryGetValue(entity.PrototypeId.Value, out var mesh);
                if (mesh is null)
                    _cache.CadMeshes.TryGetValue(entity.PrototypeId.Value, out mesh);
                _cache.Instances.Add(new EvaluatedInstance(entity.PrototypeId.Value, xf, mesh));
                continue;
            }

            if (kind is "arrayinstance" or "clone")
            {
                if (entity.PrototypeId is null && entity.SourceId is null)
                    continue;
                var sourceId = entity.PrototypeId ?? entity.SourceId!.Value;
                _cache.ModeledMeshes.TryGetValue(sourceId, out var mesh);
                if (mesh is null)
                    _cache.CadMeshes.TryGetValue(sourceId, out mesh);

                var realization = (entity.Realization ?? "instances").ToLowerInvariant();
                var transforms = ExpandPattern(entity);
                if (realization is "fusedsolid" && mesh is not null)
                {
                    EditableMesh? fused = null;
                    foreach (var xf in transforms)
                    {
                        var copy = mesh.Clone();
                        copy.Transform(xf);
                        fused = fused is null ? copy : MeshBoolean.Concat(fused, copy);
                    }

                    if (fused is not null)
                        _cache.ModeledMeshes[entity.Id] = fused;
                }
                else if (realization is "separatecopies" && mesh is not null)
                {
                    // Distinct mesh per transform (not a shared instance reference).
                    EditableMesh? compound = null;
                    foreach (var xf in transforms)
                    {
                        var copy = mesh.Clone();
                        copy.Transform(xf);
                        _cache.Instances.Add(new EvaluatedInstance(sourceId, Matrix4x4.Identity, copy));
                        compound = compound is null ? copy.Clone() : MeshBoolean.Concat(compound, copy);
                    }

                    if (compound is not null)
                        _cache.ModeledMeshes[entity.Id] = compound;
                }
                else
                {
                    foreach (var xf in transforms)
                        _cache.Instances.Add(new EvaluatedInstance(sourceId, xf, mesh));
                }
            }
        }

        _ = byId;
    }

    private void RebuildPreview(CadDocument document)
    {
        _cache.Lights.Clear();
        _cache.Cameras.Clear();
        _cache.Materials.Clear();
        foreach (var entity in document.Entities)
        {
            var kind = entity.Kind.ToLowerInvariant();
            switch (kind)
            {
                case "light":
                    _cache.Lights.Add(entity);
                    break;
                case "camera":
                    _cache.Cameras.Add(entity);
                    break;
                case "material":
                    _cache.Materials.Add(entity);
                    break;
            }
        }

        _cache.PreviewRevision++;
    }

    private bool TryResolveMesh(Dictionary<Guid, CadEntity> byId, Guid id, out EditableMesh? mesh)
    {
        if (_cache.CadMeshes.TryGetValue(id, out mesh))
            return true;
        if (byId.TryGetValue(id, out var entity))
        {
            mesh = CadSolidTessellator.TryTessellate(entity);
            if (mesh is not null)
            {
                _cache.CadMeshes[id] = mesh;
                return true;
            }
        }

        mesh = null;
        return false;
    }

    private List<EditableMesh> ResolveMembers(CadEntity entity, Dictionary<Guid, CadEntity> byId)
    {
        var list = new List<EditableMesh>();
        if (entity.MemberIds is null)
            return list;
        foreach (var id in entity.MemberIds)
        {
            if (TryResolveMesh(byId, id, out var m) && m is not null)
                list.Add(m);
        }

        return list;
    }

    private static EditableMesh ApplyWeld(EditableMesh mesh, CadEntity entity)
    {
        var tol = entity.TouchEpsilonMeters ?? 1e-4f;
        MeshWeld.Apply(mesh, new WeldOptions(tol));
        return mesh;
    }

    private static Plane ResolvePlane(CadEntity entity)
    {
        var n = entity.Normal is { Length: >= 3 }
            ? Vector3.Normalize(CadVec.To(entity.Normal))
            : Vector3.UnitX;
        var p = entity.PlanePoint is { Length: >= 3 }
            ? CadVec.To(entity.PlanePoint)
            : entity.Center is { Length: >= 3 }
                ? CadVec.To(entity.Center)
                : Vector3.Zero;
        return new Plane(n, -Vector3.Dot(n, p));
    }

    private static List<Matrix4x4> ExpandPattern(CadEntity entity)
    {
        var list = new List<Matrix4x4>();
        var baseXf = ToMatrix(entity.BaseTransform ?? entity.Transform);

        if (entity.Axis is { Length: >= 3 } && entity.StepRadians is { } step && entity.Counts is { Length: >= 1 })
        {
            var axis = Vector3.Normalize(CadVec.To(entity.Axis));
            var count = System.Math.Max(1, entity.Counts[0]);
            for (var i = 0; i < count; i++)
            {
                var rot = Matrix4x4.CreateFromAxisAngle(axis, step * i);
                list.Add(rot * baseXf);
            }

            return list;
        }

        var counts = entity.Counts is { Length: >= 3 } ? entity.Counts : [1, 1, 1];
        var spacing = entity.Spacing is { Length: >= 3 } ? CadVec.To(entity.Spacing) : Vector3.UnitX;
        for (var z = 0; z < counts[2]; z++)
        for (var y = 0; y < counts[1]; y++)
        for (var x = 0; x < counts[0]; x++)
        {
            if (x == 0 && y == 0 && z == 0 && counts[0] * counts[1] * counts[2] > 1)
            {
                // still include origin instance
            }

            var offset = new Vector3(spacing.X * x, spacing.Y * y, spacing.Z * z);
            list.Add(Matrix4x4.CreateTranslation(offset) * baseXf);
        }

        return list;
    }

    private static Matrix4x4 ToMatrix(CadTransform? t)
    {
        if (t is null)
            return Matrix4x4.Identity;
        var m = Matrix4x4.Identity;
        if (t.Scale is { Length: >= 3 })
            m *= Matrix4x4.CreateScale(CadVec.To(t.Scale));
        if (t.RotationQuat is { Length: >= 4 })
        {
            var q = new Quaternion(t.RotationQuat[0], t.RotationQuat[1], t.RotationQuat[2], t.RotationQuat[3]);
            m *= Matrix4x4.CreateFromQuaternion(q);
        }
        else if (t.RotationY is { } ry)
            m *= Matrix4x4.CreateRotationY(ry);

        if (t.Center is { Length: >= 3 })
            m *= Matrix4x4.CreateTranslation(CadVec.To(t.Center));
        return m;
    }
}

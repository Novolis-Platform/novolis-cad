using System.Numerics;
using Novolis.Math.Geometry;

namespace Novolis.Modeling.Scene;

/// <summary>Evaluates generators and modifiers into renderable triangle meshes.</summary>
public static class MeshStackEvaluator
{
    public static IReadOnlyList<EvaluatedMesh> EvaluateDocument(
        SceneDocument document,
        IReadOnlyDictionary<Guid, Matrix4x4> worlds)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(worlds);

        var baseMeshes = new Dictionary<Guid, EditableMesh>();
        foreach (var meshNode in document.Nodes.OfType<MeshNode>())
        {
            if (!meshNode.Visible)
                continue;
            baseMeshes[meshNode.Id] = PrimitiveMesher.Tessellate(meshNode);
        }

        var results = new List<EvaluatedMesh>();
        var consumedAsOperand = new HashSet<Guid>();

        foreach (var gen in document.Nodes.OfType<GeneratorNode>().Where(g => g.Visible))
        {
            switch (gen.Generator)
            {
                case GeneratorKind.Cloner:
                {
                    if (gen.SourceId is not { } sid || !baseMeshes.TryGetValue(sid, out var source))
                        break;
                    worlds.TryGetValue(sid, out var srcWorld);
                    var offset = new Vector3(
                        gen.Offset.Length > 0 ? gen.Offset[0] : 0,
                        gen.Offset.Length > 1 ? gen.Offset[1] : 0,
                        gen.Offset.Length > 2 ? gen.Offset[2] : 0);
                    for (var i = 0; i < System.Math.Max(1, gen.Count); i++)
                    {
                        var instance = source.Clone();
                        var world = Matrix4x4.CreateTranslation(offset * i) * srcWorld;
                        results.Add(EvaluatedMesh.FromEditable(gen.Id, instance, world));
                    }

                    consumedAsOperand.Add(sid);
                    break;
                }
                case GeneratorKind.Symmetry:
                {
                    if (gen.SourceId is not { } sid || !baseMeshes.TryGetValue(sid, out var source))
                        break;
                    worlds.TryGetValue(sid, out var srcWorld);
                    results.Add(EvaluatedMesh.FromEditable(gen.Id, source.Clone(), srcWorld));
                    var mirror = gen.Axis.ToLowerInvariant() switch
                    {
                        "y" => Matrix4x4.CreateScale(1, -1, 1),
                        "z" => Matrix4x4.CreateScale(1, 1, -1),
                        _ => Matrix4x4.CreateScale(-1, 1, 1),
                    };
                    var mirrored = source.Clone();
                    results.Add(EvaluatedMesh.FromEditable(gen.Id, mirrored, mirror * srcWorld));
                    consumedAsOperand.Add(sid);
                    break;
                }
                case GeneratorKind.Boole:
                {
                    var targetId = gen.TargetId ?? gen.SourceId;
                    var cutterId = gen.CutterId;
                    if (targetId is null || cutterId is null)
                        break;
                    if (!baseMeshes.TryGetValue(targetId.Value, out var target)
                        || !baseMeshes.TryGetValue(cutterId.Value, out var cutter))
                        break;

                    worlds.TryGetValue(targetId.Value, out var tw);
                    worlds.TryGetValue(cutterId.Value, out var cw);
                    var left = target.Clone();
                    left.Transform(tw);
                    var right = cutter.Clone();
                    right.Transform(cw);
                    var kind = gen.BooleanKind switch
                    {
                        BooleanKind.Union => MeshBooleanKind.Union,
                        BooleanKind.Intersection => MeshBooleanKind.Intersection,
                        _ => MeshBooleanKind.Difference,
                    };
                    var result = MeshBoolean.Apply(left, right, kind);
                    results.Add(EvaluatedMesh.FromEditable(gen.Id, result, Matrix4x4.Identity));
                    consumedAsOperand.Add(targetId.Value);
                    consumedAsOperand.Add(cutterId.Value);
                    break;
                }
            }
        }

        // Modifier stacks: start from input mesh (or base), apply chain by InputId depth-first.
        foreach (var meshId in baseMeshes.Keys)
        {
            if (consumedAsOperand.Contains(meshId))
                continue;

            var stack = CollectModifierStack(document, meshId);
            var work = baseMeshes[meshId].Clone();
            foreach (var mod in stack)
                work = ApplyModifier(work, mod);

            worlds.TryGetValue(meshId, out var world);
            results.Add(EvaluatedMesh.FromEditable(meshId, work, world));
        }

        // Orphan modifiers whose InputId is a generator result are skipped in v1.
        return results;
    }

    private static List<ModifierNode> CollectModifierStack(SceneDocument document, Guid meshId)
    {
        // Modifiers that point at this mesh, ordered by Levels ascending as a simple stack order.
        return document.Nodes.OfType<ModifierNode>()
            .Where(m => m.Visible && m.InputId == meshId)
            .OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static EditableMesh ApplyModifier(EditableMesh mesh, ModifierNode mod) =>
        mod.Modifier switch
        {
            ModifierKind.Weld => MeshWeld.Apply(mesh, new WeldOptions(mod.Tolerance)),
            ModifierKind.Optimize => MeshOptimize.Apply(mesh).Mesh,
            ModifierKind.Subdivision => MeshShaping.Subdivide(mesh, mod.Levels <= 0 ? 1 : mod.Levels),
            ModifierKind.Extrude => MeshShaping.Extrude(mesh, mod.Distance),
            ModifierKind.Bevel => MeshShaping.BevelLite(mesh, mod.Distance),
            ModifierKind.Inset => MeshComponentOps.InsetFaces(
                mesh,
                Enumerable.Range(0, mesh.TriangleCount).ToArray(),
                mod.Distance),
            ModifierKind.Dissolve => mesh.Clone(),
            ModifierKind.Knife => MeshComponentOps.Knife(mesh, new Plane(Vector3.UnitY, 0)),
            ModifierKind.Bridge =>
                mesh.FindBoundaryLoops() is { Count: >= 2 } loops && loops[0].Count == loops[1].Count
                    ? MeshBridge.Apply(mesh, loops[0], loops[1])
                    : mesh.Clone(),
            _ => mesh.Clone(),
        };
}

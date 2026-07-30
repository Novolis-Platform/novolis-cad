using System.Numerics;
using Novolis.Math.Geometry;

namespace Novolis.Modeling.Scene;

/// <summary>Bake evaluated / procedural mesh into editable vertex/index soup (C4D Make Editable).</summary>
public static class MeshEditBake
{
    public static bool MakeEditable(SceneDocument document, SceneEvaluator evaluator, Guid nodeId)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(evaluator);

        var node = document.Find(nodeId);
        if (node is MeshNode mesh)
            return BakeMeshNode(document, evaluator, mesh);

        if (node is GeneratorNode or ModifierNode)
            return BakeGeneratorOrModifier(document, evaluator, node);

        return false;
    }

    public static void WriteBaked(MeshNode mesh, EditableMesh editable)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        ArgumentNullException.ThrowIfNull(editable);
        var verts = new float[editable.VertexCount * 3];
        for (var i = 0; i < editable.VertexCount; i++)
        {
            var v = editable.Vertices[i];
            verts[i * 3] = v.X;
            verts[i * 3 + 1] = v.Y;
            verts[i * 3 + 2] = v.Z;
        }

        mesh.Vertices = verts;
        mesh.Indices = editable.Indices.ToArray();
    }

    public static EditableMesh ReadBakedOrTessellate(MeshNode mesh) => PrimitiveMesher.Tessellate(mesh);

    private static bool BakeMeshNode(SceneDocument document, SceneEvaluator evaluator, MeshNode mesh)
    {
        if (mesh.Vertices is { Length: > 0 } && mesh.Indices is { Length: > 0 })
        {
            document.Edit.EditMeshId = mesh.Id;
            return true;
        }

        var worlds = SceneEvaluator.BuildWorldMatrices(document);
        var evaluated = MeshStackEvaluator.EvaluateDocument(document, worlds)
            .FirstOrDefault(m => m.SourceId == mesh.Id);
        EditableMesh local;
        if (evaluated is not null && Matrix4x4.Invert(evaluated.World, out var inv))
        {
            local = evaluated.ToEditableMesh();
            local.Transform(inv);
        }
        else
        {
            local = PrimitiveMesher.Tessellate(mesh);
        }

        WriteBaked(mesh, local);
        if (!mesh.Name.Contains("Editable", StringComparison.OrdinalIgnoreCase))
            mesh.Name = $"{mesh.Name} (Editable)";
        document.Edit.EditMeshId = mesh.Id;
        evaluator.NotifyNodeChanged(mesh);
        return true;
    }

    private static bool BakeGeneratorOrModifier(SceneDocument document, SceneEvaluator evaluator, SceneNode node)
    {
        var worlds = SceneEvaluator.BuildWorldMatrices(document);
        var evaluated = MeshStackEvaluator.EvaluateDocument(document, worlds)
            .FirstOrDefault(m => m.SourceId == node.Id);
        if (evaluated is null)
            return false;

        EditableMesh local = evaluated.ToEditableMesh();
        if (Matrix4x4.Invert(evaluated.World, out var inv))
            local.Transform(inv);

        var mesh = new MeshNode
        {
            Name = $"{node.Name} (Editable)",
            ParentId = node.ParentId,
            Primitive = MeshPrimitiveKind.Box,
            Transform = node.Transform.Clone(),
        };
        WriteBaked(mesh, local);
        document.Nodes.Add(mesh);
        document.SelectionId = mesh.Id;
        document.Edit.EditMeshId = mesh.Id;
        document.Edit.ClearComponents();
        evaluator.InvalidateAll();
        return true;
    }
}

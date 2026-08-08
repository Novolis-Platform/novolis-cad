using System.Numerics;
using Novolis.Math.Geometry;
using Novolis.Modeling.Scene;

namespace Novolis.Cad.Unit;

public sealed class ModelingSceneCoveragePushTests
{
    [Test]
    public async Task MeshEditState_polygon_selection_count()
    {
        var edit = new MeshEditState { Mode = SceneEditMode.Polygon };
        edit.SelectedFaces.Add(0);
        edit.SelectedFaces.Add(1);
        await Assert.That(edit.SelectionCount).IsEqualTo(2);
        edit.Mode = SceneEditMode.Object;
        await Assert.That(edit.SelectionCount).IsEqualTo(0);
    }

    [Test]
    public async Task SceneDocument_try_remove_missing_and_reparent()
    {
        var doc = SceneDocument.CreateEmpty("E");
        await Assert.That(doc.TryRemove(Guid.NewGuid())).IsFalse();

        var parent = doc.Roots().OfType<GroupNode>().First();
        var child = new GroupNode { Name = "Child", ParentId = parent.Id };
        var leaf = new MeshNode
        {
            Name = "Leaf",
            ParentId = child.Id,
            Primitive = MeshPrimitiveKind.Box,
            Size = [1, 1, 1],
        };
        doc.Nodes.Add(child);
        doc.Nodes.Add(leaf);
        doc.SelectionId = child.Id;
        doc.ActiveCameraId = child.Id;
        await Assert.That(doc.TryRemove(child.Id)).IsTrue();
        await Assert.That(leaf.ParentId).IsEqualTo(parent.Id);
        await Assert.That(doc.SelectionId).IsNull();
        await Assert.That(doc.ActiveCameraId).IsNull();
    }

    [Test]
    public async Task MeshComponentOps_empty_inset_invalid_bevel_and_bridge()
    {
        var box = PrimitiveMesher.Box(1, 1, 1);
        await Assert.That(MeshComponentOps.InsetFaces(box, [], 0.1f).TriangleCount).IsEqualTo(box.TriangleCount);
        await Assert.That(MeshComponentOps.InsetFaces(box, [-1, 999], 0.1f).TriangleCount).IsEqualTo(box.TriangleCount);

        var beveled = MeshComponentOps.BevelEdges(box, [(-1, 0), (0, 999)], 0.2f);
        await Assert.That(beveled.VertexCount).IsEqualTo(box.VertexCount);

        await Assert.That(MeshComponentOps.DissolveEdges(box, [(-1, 0), (0, 0), (0, 999)]).VertexCount)
            .IsGreaterThan(0);

        var bridged = MeshComponentOps.BridgeSelectedEdges(box, [(0, 1)]);
        await Assert.That(bridged.VertexCount).IsEqualTo(box.VertexCount);

        var open = new EditableMesh(
            [Vector3.Zero, Vector3.UnitX, Vector3.UnitY, new Vector3(1, 1, 0), new Vector3(0, 0, 1), new Vector3(1, 0, 1), new Vector3(0, 1, 1), new Vector3(1, 1, 1)],
            [0, 1, 2, 4, 5, 6]);
        var bridgedOpen = MeshComponentOps.BridgeSelectedEdges(open, [(0, 1), (4, 5)]);
        await Assert.That(bridgedOpen.TriangleCount).IsGreaterThanOrEqualTo(open.TriangleCount);

        await Assert.That(MeshComponentOps.MoveFaces(box, [-1, 999], Vector3.UnitY).Vertices[0])
            .IsEqualTo(box.Vertices[0]);
    }

    [Test]
    public async Task MeshShaping_zero_iterations_and_extrude_clamp()
    {
        var box = PrimitiveMesher.Box(1, 1, 1);
        await Assert.That(MeshShaping.Subdivide(box, 0).TriangleCount).IsEqualTo(box.TriangleCount);
        await Assert.That(MeshShaping.Extrude(box, 0f).VertexCount).IsEqualTo(box.VertexCount);
        await Assert.That(MeshShaping.BevelLite(box, 0f).VertexCount).IsEqualTo(box.VertexCount);
    }

    [Test]
    public async Task RenderingLightExport_skips_disabled_and_handles_degenerate_forward()
    {
        var doc = new SceneDocument { Name = "Lights" };
        var root = new GroupNode { Name = "Scene" };
        doc.Nodes.Add(root);
        doc.Nodes.Add(new LightNode
        {
            Name = "Off",
            ParentId = root.Id,
            LightKind = LightKind.Omni,
            Enabled = false,
            Color = [1, 1, 1],
            Intensity = 1,
        });
        doc.Nodes.Add(new LightNode
        {
            Name = "Inf",
            ParentId = root.Id,
            LightKind = LightKind.Infinite,
            Enabled = true,
            Color = [1, 0, 0],
            Intensity = 2,
            Transform = new SceneTransform { Scale = [0, 0, 0] },
        });
        var eval = new SceneEvaluator();
        eval.Bind(doc);
        var exported = RenderingLightExport.Export(eval.Cache);
        await Assert.That(exported.Count).IsEqualTo(1);
        await Assert.That(exported[0].Kind).IsEqualTo("Directional");
    }

    [Test]
    public async Task MeshPicker_miss_and_null_cache()
    {
        var doc = SceneDocument.CreateEditBox();
        var eval = new SceneEvaluator();
        eval.Bind(doc);
        var miss = MeshPicker.ScreenRay(
            new Vector3(100, 100, 100),
            new Vector3(101, 100, 100),
            Vector3.UnitY,
            45f,
            1f,
            0,
            0);
        await Assert.That(MeshPicker.Pick(eval.Cache.EvaluatedMeshes, miss, SceneEditMode.Polygon)).IsNull();
    }

    [Test]
    public async Task MeshStackEvaluator_axis_variants_and_missing_boole()
    {
        foreach (var axis in new[] { "x", "y", "X" })
        {
            var doc = new SceneDocument { Name = "Sym" };
            var root = new GroupNode { Name = "Scene" };
            doc.Nodes.Add(root);
            var source = new MeshNode
            {
                Name = "Source",
                ParentId = root.Id,
                Primitive = MeshPrimitiveKind.Box,
                Size = [1, 1, 1],
            };
            doc.Nodes.Add(source);
            doc.Nodes.Add(new GeneratorNode
            {
                Name = "Mirror",
                ParentId = root.Id,
                Generator = GeneratorKind.Symmetry,
                SourceId = source.Id,
                Axis = axis,
            });
            var eval = new SceneEvaluator();
            eval.Bind(doc);
            await Assert.That(eval.Cache.EvaluatedMeshes.Count).IsEqualTo(2);
        }

        var badBoole = new SceneDocument { Name = "BadBoole" };
        var r = new GroupNode { Name = "Scene" };
        badBoole.Nodes.Add(r);
        badBoole.Nodes.Add(new GeneratorNode
        {
            Name = "Boole",
            ParentId = r.Id,
            Generator = GeneratorKind.Boole,
            SourceId = Guid.NewGuid(),
            TargetId = Guid.NewGuid(),
            CutterId = Guid.NewGuid(),
        });
        var booleEval = new SceneEvaluator();
        booleEval.Bind(badBoole);
        await Assert.That(booleEval.Cache.EvaluatedMeshes.Count).IsEqualTo(0);
    }

    [Test]
    public async Task SceneEvaluator_material_and_camera_lookups()
    {
        var doc = SceneDocument.CreateLookSetup();
        var eval = new SceneEvaluator();
        eval.Bind(doc);
        await Assert.That(eval.Cache.Lights.Count).IsGreaterThan(0);
        await Assert.That(eval.Cache.Cameras.Count).IsGreaterThan(0);

        var orphanMat = new SceneDocument { Name = "Mat" };
        var root = new GroupNode { Name = "Scene" };
        orphanMat.Nodes.Add(root);
        orphanMat.Nodes.Add(new MaterialNode { Name = "M", ParentId = root.Id });
        orphanMat.Nodes.Add(new MeshNode
        {
            Name = "Box",
            ParentId = root.Id,
            Primitive = MeshPrimitiveKind.Box,
            Size = [1, 1, 1],
            MaterialId = Guid.NewGuid(),
        });
        var matEval = new SceneEvaluator();
        matEval.Bind(orphanMat);
        await Assert.That(matEval.Cache.EvaluatedMeshes.Count).IsEqualTo(1);
    }

    [Test]
    public async Task PrimitiveMesher_custom_segments_and_scene_serializer_null()
    {
        var cylinder = new MeshNode { Primitive = MeshPrimitiveKind.Cylinder, Size = [1, 2, 1], Segments = 3 };
        await Assert.That(PrimitiveMesher.Tessellate(cylinder).TriangleCount).IsGreaterThan(0);
        var cone = new MeshNode { Primitive = MeshPrimitiveKind.Cone, Size = [1, 2, 1], Segments = 4 };
        await Assert.That(PrimitiveMesher.Tessellate(cone).TriangleCount).IsGreaterThan(0);
        var sphere = new MeshNode { Primitive = MeshPrimitiveKind.Sphere, Size = [1, 1, 1], Segments = 4 };
        await Assert.That(PrimitiveMesher.Tessellate(sphere).TriangleCount).IsGreaterThan(0);

        await Assert.That(() => SceneSerializer.Deserialize(null!)).Throws<ArgumentNullException>();

        var shortSize = new MeshNode { Primitive = MeshPrimitiveKind.Box, Size = [], Segments = 0 };
        await Assert.That(PrimitiveMesher.Tessellate(shortSize).VertexCount).IsGreaterThan(0);
        var one = new MeshNode { Primitive = MeshPrimitiveKind.Box, Size = [2], Segments = -1 };
        await Assert.That(PrimitiveMesher.Tessellate(one).VertexCount).IsGreaterThan(0);
        var two = new MeshNode { Primitive = MeshPrimitiveKind.Box, Size = [2, 3], Segments = 16 };
        await Assert.That(PrimitiveMesher.Tessellate(two).VertexCount).IsGreaterThan(0);

        var emptyMesh = new EditableMesh([], []);
        await Assert.That(MeshShaping.Extrude(emptyMesh, 1f).TriangleCount).IsEqualTo(0);
        await Assert.That(MeshShaping.BevelLite(emptyMesh, 1f).TriangleCount).IsEqualTo(0);
    }
}

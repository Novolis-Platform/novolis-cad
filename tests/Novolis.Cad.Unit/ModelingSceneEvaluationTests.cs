using System.Numerics;
using Novolis.Math.Geometry;
using Novolis.Modeling.Scene;

namespace Novolis.Cad.Unit;

public sealed class ModelingSceneEvaluationTests
{
    [Test]
    public async Task SceneSerializer_RoundTripAndValidation()
    {
        var doc = SceneDocument.CreatePrimitiveStage("Stage");
        var json = SceneSerializer.Serialize(doc);
        var loaded = SceneSerializer.Deserialize(json);
        await Assert.That(loaded.Name).IsEqualTo("Stage");
        await Assert.That(loaded.Format).IsEqualTo("novolis.scene");

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await Task.Yield();
            SceneSerializer.Deserialize("""{"format":"other"}""");
        });
    }

    [Test]
    public async Task SceneDocument_FactoryPresetsAndTryRemove()
    {
        var cloner = SceneDocument.CreateClonerRow();
        await Assert.That(cloner.Nodes.OfType<GeneratorNode>().Any(g => g.Generator == GeneratorKind.Cloner)).IsTrue();

        var boole = SceneDocument.CreateBooleCut();
        await Assert.That(boole.Nodes.OfType<GeneratorNode>().Any(g => g.Generator == GeneratorKind.Boole)).IsTrue();

        var look = SceneDocument.CreateLookSetup();
        await Assert.That(look.Nodes.OfType<LightNode>().Count()).IsGreaterThanOrEqualTo(3);

        var edit = SceneDocument.CreateEditBox();
        await Assert.That(edit.Edit.EditMeshId).IsNotNull();
        await Assert.That(edit.Edit.SelectedFaces.Count).IsEqualTo(1);

        var gallery = SceneDocument.CreatePrimitiveGallery();
        await Assert.That(gallery.Nodes.OfType<MeshNode>().Count()).IsGreaterThanOrEqualTo(Enum.GetValues<MeshPrimitiveKind>().Length);

        var camId = look.ActiveCameraId!.Value;
        await Assert.That(look.TryRemove(camId)).IsTrue();
        await Assert.That(look.ActiveCameraId).IsNull();
    }

    [Test]
    public async Task SceneTransform_ToMatrixAndClone()
    {
        var t = new SceneTransform
        {
            Position = [1, 2, 3],
            RotationDeg = [0, 90, 0],
            Scale = [2, 2, 2],
        };
        var clone = t.Clone();
        await Assert.That(clone.Position[0]).IsEqualTo(1);
        var m = t.ToMatrix();
        await Assert.That(m.M41).IsEqualTo(1);
        await Assert.That(m.M42).IsEqualTo(2);
        await Assert.That(m.M43).IsEqualTo(3);
    }

    [Test]
    public async Task PrimitiveMesher_TessellatesAllPrimitiveKinds()
    {
        foreach (var kind in Enum.GetValues<MeshPrimitiveKind>())
        {
            var node = new MeshNode { Primitive = kind, Size = [1, 1, 1], Segments = 8 };
            var mesh = PrimitiveMesher.Tessellate(node);
            await Assert.That(mesh.VertexCount).IsGreaterThan(0);
            await Assert.That(mesh.TriangleCount).IsGreaterThan(0);
        }

        var baked = new MeshNode
        {
            Vertices = [-1, 0, -1, 1, 0, -1, 0, 1, 0],
            Indices = [0, 1, 2],
        };
        await Assert.That(PrimitiveMesher.Tessellate(baked).TriangleCount).IsEqualTo(1);
    }

    [Test]
    public async Task MeshStackEvaluator_ClonerSymmetryAndModifiers()
    {
        var clonerDoc = SceneDocument.CreateClonerRow();
        var clonerEval = new SceneEvaluator();
        clonerEval.Bind(clonerDoc);
        await Assert.That(clonerEval.Cache.EvaluatedMeshes.Count).IsGreaterThanOrEqualTo(5);

        var symDoc = new SceneDocument { Name = "Sym" };
        var root = new GroupNode { Name = "Scene" };
        symDoc.Nodes.Add(root);
        var source = new MeshNode
        {
            Name = "Source",
            ParentId = root.Id,
            Primitive = MeshPrimitiveKind.Box,
            Size = [1, 1, 1],
        };
        symDoc.Nodes.Add(source);
        symDoc.Nodes.Add(new GeneratorNode
        {
            Name = "MirrorY",
            ParentId = root.Id,
            Generator = GeneratorKind.Symmetry,
            SourceId = source.Id,
            Axis = "y",
        });
        var symEval = new SceneEvaluator();
        symEval.Bind(symDoc);
        await Assert.That(symEval.Cache.EvaluatedMeshes.Count).IsEqualTo(2);

        var modDoc = SceneDocument.CreateEditBox();
        var box = modDoc.Nodes.OfType<MeshNode>().First(m => m.Vertices is { Length: > 0 });
        modDoc.Nodes.Add(new ModifierNode
        {
            Name = "01 Weld",
            InputId = box.Id,
            Modifier = ModifierKind.Weld,
            Tolerance = 0.001f,
        });
        var modEval = new SceneEvaluator();
        modEval.Bind(modDoc);
        await Assert.That(modEval.Cache.EvaluatedMeshes.Single(m => m.SourceId == box.Id).Vertices.Length).IsGreaterThan(0);
    }

    [Test]
    public async Task SceneEvaluator_InvalidatesAndCaches()
    {
        var doc = SceneDocument.CreateLookSetup();
        var eval = new SceneEvaluator();
        eval.Bind(doc);

        var first = eval.Cache;
        await Assert.That(first.Lights.Count).IsGreaterThan(0);
        await Assert.That(first.Meshes.Count).IsGreaterThan(0);
        await Assert.That(eval.MeshGeneration).IsEqualTo(1);
        await Assert.That(eval.LookGeneration).IsEqualTo(1);

        var second = eval.Cache;
        await Assert.That(ReferenceEquals(first, second)).IsTrue();

        var light = doc.Nodes.OfType<LightNode>().First();
        eval.NotifyNodeChanged(light);
        await Assert.That(eval.LookGeneration).IsEqualTo(2);
        await Assert.That(ReferenceEquals(first, eval.Cache)).IsFalse();

        var mesh = doc.Nodes.OfType<MeshNode>().First(m => m.Primitive != MeshPrimitiveKind.Plane);
        eval.NotifyNodeChanged(mesh);
        await Assert.That(eval.MeshGeneration).IsEqualTo(2);
        await Assert.That(eval.LookGeneration).IsEqualTo(3);
    }

    [Test]
    public async Task MeshShaping_SubdivideExtrudeBevel()
    {
        var box = PrimitiveMesher.Box(1, 1, 1);
        var subdivided = MeshShaping.Subdivide(box, 1);
        await Assert.That(subdivided.TriangleCount).IsGreaterThan(box.TriangleCount);

        var extruded = MeshShaping.Extrude(box, 0.1f);
        await Assert.That(extruded.VertexCount).IsEqualTo(box.VertexCount);

        var beveled = MeshShaping.BevelLite(box, 0.2f);
        await Assert.That(beveled.VertexCount).IsEqualTo(box.VertexCount);
    }

    [Test]
    public async Task MeshComponentOps_ExtrudeInsetMoveAndKnife()
    {
        var box = PrimitiveMesher.Box(1, 1, 1);
        var extruded = MeshComponentOps.ExtrudeFaces(box, [0], 0.2f);
        await Assert.That(extruded.TriangleCount).IsGreaterThan(box.TriangleCount);

        var inset = MeshComponentOps.InsetFaces(box, [0], 0.1f);
        await Assert.That(inset.TriangleCount).IsGreaterThan(box.TriangleCount);

        var moved = MeshComponentOps.MoveVertices(box, [0], new Vector3(0, 0.5f, 0));
        await Assert.That(moved.Vertices[0].Y).IsEqualTo(-0.5f + 0.5f);

        var knifed = MeshComponentOps.Knife(box, new Plane(Vector3.UnitY, 0));
        await Assert.That(knifed.TriangleCount).IsGreaterThan(0);

        var beveled = MeshComponentOps.BevelEdges(box, [(0, 1)], 0.2f);
        await Assert.That(beveled.VertexCount).IsEqualTo(box.VertexCount);

        var dissolved = MeshComponentOps.DissolveFaces(box, [0]);
        await Assert.That(dissolved.TriangleCount).IsLessThan(box.TriangleCount);

        var edgeDissolved = MeshComponentOps.DissolveEdges(box, [(0, 1)]);
        await Assert.That(edgeDissolved.VertexCount).IsGreaterThan(0);

        var movedFace = MeshComponentOps.MoveFaces(box, [0], new Vector3(0, 0.1f, 0));
        await Assert.That(movedFace.Vertices[0].Y).IsNotEqualTo(box.Vertices[0].Y);

        var movedEdge = MeshComponentOps.MoveEdges(box, [(0, 1)], new Vector3(0.05f, 0, 0));
        await Assert.That(movedEdge.Vertices[0].X).IsNotEqualTo(box.Vertices[0].X);

        await Assert.That(MeshComponentOps.ExtrudeFaces(box, [], 0.1f).TriangleCount).IsEqualTo(box.TriangleCount);
        await Assert.That(MeshComponentOps.ExtrudeFaces(box, [999], 0.1f).TriangleCount).IsEqualTo(box.TriangleCount);
        await Assert.That(MeshComponentOps.BevelEdges(box, [], 0.2f).VertexCount).IsEqualTo(box.VertexCount);
        await Assert.That(MeshComponentOps.DissolveFaces(box, []).TriangleCount).IsEqualTo(box.TriangleCount);
        await Assert.That(MeshComponentOps.MoveVertices(box, [999], Vector3.UnitY).Vertices[0]).IsEqualTo(box.Vertices[0]);
    }

    [Test]
    public async Task MeshEditState_TracksSelection()
    {
        var edit = new MeshEditState { Mode = SceneEditMode.Edge };
        edit.SelectedEdges.Add((0, 1));
        edit.SelectedVertices.Add(2);
        await Assert.That(edit.SelectionCount).IsEqualTo(1);

        edit.Mode = SceneEditMode.Point;
        await Assert.That(edit.SelectionCount).IsEqualTo(1);

        edit.ClearComponents();
        edit.ClearAll();
        await Assert.That(edit.EditMeshId).IsNull();
        await Assert.That(edit.Mode).IsEqualTo(SceneEditMode.Object);
    }

    [Test]
    public async Task MeshEditBake_MakesProceduralMeshEditable()
    {
        var doc = SceneDocument.CreatePrimitiveStage("Bake");
        var eval = new SceneEvaluator();
        eval.Bind(doc);
        var mesh = doc.Nodes.OfType<MeshNode>().First(m => m.Primitive == MeshPrimitiveKind.Box);

        await Assert.That(MeshEditBake.MakeEditable(doc, eval, mesh.Id)).IsTrue();
        await Assert.That(mesh.Vertices).IsNotNull();
        await Assert.That(mesh.Indices).IsNotNull();
        await Assert.That(doc.Edit.EditMeshId).IsEqualTo(mesh.Id);
    }

    [Test]
    public async Task MeshPicker_FindsFaceUnderRay()
    {
        var doc = SceneDocument.CreateEditBox();
        var eval = new SceneEvaluator();
        eval.Bind(doc);
        var ray = MeshPicker.ScreenRay(
            new Vector3(0, 2, 5),
            new Vector3(0, 0.75f, 0),
            Vector3.UnitY,
            45f,
            1f,
            0,
            0);

        var hit = MeshPicker.Pick(eval.Cache.EvaluatedMeshes, ray, SceneEditMode.Polygon);
        await Assert.That(hit).IsNotNull();
        await Assert.That(hit!.Value.Mode).IsEqualTo(SceneEditMode.Polygon);
    }

    [Test]
    public async Task RenderingLightExport_MapsInfiniteToDirectional()
    {
        var doc = SceneDocument.CreateLookSetup();
        var eval = new SceneEvaluator();
        eval.Bind(doc);
        var exported = RenderingLightExport.Export(eval.Cache);
        await Assert.That(exported.Count).IsGreaterThan(0);
        await Assert.That(exported.Any(l => l.Kind == "Directional" || l.Kind == "Point")).IsTrue();
    }

    [Test]
    public async Task MeshStackEvaluator_Boole_and_symmetry_z()
    {
        var booleDoc = SceneDocument.CreateBooleCut();
        var booleEval = new SceneEvaluator();
        booleEval.Bind(booleDoc);
        await Assert.That(booleEval.Cache.EvaluatedMeshes.Count).IsGreaterThanOrEqualTo(1);
        await Assert.That(booleEval.Cache.EvaluatedMeshes[0].Indices.Length).IsGreaterThan(0);

        var symDoc = new SceneDocument { Name = "SymZ" };
        var root = new GroupNode { Name = "Scene" };
        symDoc.Nodes.Add(root);
        var source = new MeshNode
        {
            Name = "Source",
            ParentId = root.Id,
            Primitive = MeshPrimitiveKind.Box,
            Size = [1, 1, 1],
        };
        symDoc.Nodes.Add(source);
        symDoc.Nodes.Add(new GeneratorNode
        {
            Name = "MirrorZ",
            ParentId = root.Id,
            Generator = GeneratorKind.Symmetry,
            SourceId = source.Id,
            Axis = "z",
        });
        var symEval = new SceneEvaluator();
        symEval.Bind(symDoc);
        await Assert.That(symEval.Cache.EvaluatedMeshes.Count).IsEqualTo(2);
    }

    [Test]
    public async Task MeshStackEvaluator_skips_invisible_and_missing_cloner_source()
    {
        var doc = new SceneDocument { Name = "Skip" };
        var root = new GroupNode { Name = "Scene" };
        doc.Nodes.Add(root);
        doc.Nodes.Add(new MeshNode
        {
            Name = "Hidden",
            ParentId = root.Id,
            Primitive = MeshPrimitiveKind.Box,
            Visible = false,
        });
        doc.Nodes.Add(new GeneratorNode
        {
            Name = "BadCloner",
            ParentId = root.Id,
            Generator = GeneratorKind.Cloner,
            SourceId = Guid.NewGuid(),
            Count = 3,
        });
        var eval = new SceneEvaluator();
        eval.Bind(doc);
        await Assert.That(eval.Cache.EvaluatedMeshes.Count).IsEqualTo(0);
    }

    [Test]
    public async Task MeshEditBake_generator_and_read_baked()
    {
        var doc = SceneDocument.CreateBooleCut();
        var eval = new SceneEvaluator();
        eval.Bind(doc);
        var generator = doc.Nodes.OfType<GeneratorNode>().First(g => g.Generator == GeneratorKind.Boole);
        await Assert.That(MeshEditBake.MakeEditable(doc, eval, generator.Id)).IsTrue();
        await Assert.That(doc.Nodes.OfType<MeshNode>().Any(m => m.Name.Contains("Editable"))).IsTrue();

        var baked = doc.Nodes.OfType<MeshNode>().First(m => m.Vertices is { Length: > 0 });
        var editable = MeshEditBake.ReadBakedOrTessellate(baked);
        await Assert.That(editable.VertexCount).IsGreaterThan(0);
    }

    [Test]
    public async Task MeshEditBake_already_baked_sets_edit_mesh_id()
    {
        var doc = SceneDocument.CreateEditBox();
        var eval = new SceneEvaluator();
        eval.Bind(doc);
        var mesh = doc.Nodes.OfType<MeshNode>().First(m => m.Vertices is { Length: > 0 });
        var beforeCount = doc.Nodes.Count;
        await Assert.That(MeshEditBake.MakeEditable(doc, eval, mesh.Id)).IsTrue();
        await Assert.That(doc.Edit.EditMeshId).IsEqualTo(mesh.Id);
        await Assert.That(doc.Nodes.Count).IsEqualTo(beforeCount);
    }

    [Test]
    public async Task MeshEditBake_unknown_node_returns_false()
    {
        var doc = SceneDocument.CreateEditBox();
        var eval = new SceneEvaluator();
        eval.Bind(doc);
        await Assert.That(MeshEditBake.MakeEditable(doc, eval, Guid.NewGuid())).IsFalse();
    }

    [Test]
    public async Task MeshPicker_object_vertex_and_edge_modes()
    {
        var doc = SceneDocument.CreateEditBox();
        var eval = new SceneEvaluator();
        eval.Bind(doc);
        var mesh = eval.Cache.EvaluatedMeshes[0];
        var center = mesh.Vertices[mesh.Indices[0]];
        var worldCenter = Vector3.Transform(center, mesh.World);
        var ray = new Novolis.Modeling.Scene.Ray(worldCenter + new Vector3(0, 2, 0), Vector3.Normalize(worldCenter - (worldCenter + new Vector3(0, 2, 0))));

        var objectHit = MeshPicker.Pick(eval.Cache.EvaluatedMeshes, ray, SceneEditMode.Object);
        await Assert.That(objectHit).IsNotNull();

        var vertexHit = MeshPicker.Pick(eval.Cache.EvaluatedMeshes, ray, SceneEditMode.Point, maxDistance: 500f, pointPixelTolerance: 5f);
        await Assert.That(vertexHit).IsNotNull();

        var edgeRay = MeshPicker.ScreenRay(
            new Vector3(0, 2, 5),
            new Vector3(0, 0.75f, 0),
            Vector3.UnitY,
            45f,
            1f,
            0,
            0);
        var edgeHit = MeshPicker.Pick(eval.Cache.EvaluatedMeshes, edgeRay, SceneEditMode.Edge, edgePixelTolerance: 5f);
        await Assert.That(edgeHit).IsNotNull();

        var dir = edgeRay.Direction;
        await Assert.That(dir.Length()).IsEqualTo(1f);
    }

    [Test]
    public async Task NullNode_round_trips_in_scene()
    {
        var doc = new SceneDocument { Name = "Null" };
        var root = new GroupNode { Name = "Scene" };
        doc.Nodes.Add(root);
        var nullNode = new NullNode { Name = "Empty", ParentId = root.Id };
        doc.Nodes.Add(nullNode);
        var loaded = SceneSerializer.Deserialize(SceneSerializer.Serialize(doc));
        await Assert.That(loaded.Nodes.OfType<NullNode>().Single().Name).IsEqualTo("Empty");
    }

    [Test]
    public async Task MeshStackEvaluator_applies_modifier_kinds()
    {
        foreach (var kind in Enum.GetValues<ModifierKind>())
        {
            var doc = SceneDocument.CreateEditBox();
            var box = doc.Nodes.OfType<MeshNode>().First(m => m.Vertices is { Length: > 0 });
            doc.Nodes.Add(new ModifierNode
            {
                Name = $"Mod {kind}",
                InputId = box.Id,
                Modifier = kind,
                Tolerance = 0.001f,
                Distance = 0.05f,
                Levels = 1,
            });
            var eval = new SceneEvaluator();
            eval.Bind(doc);
            await Assert.That(eval.Cache.EvaluatedMeshes.Count).IsGreaterThan(0);
        }
    }
}

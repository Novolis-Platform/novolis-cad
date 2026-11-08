using System.Numerics;
using Novolis.Cad.Drawing;
using Novolis.Cad.Hierarchy;
using Novolis.Cad.Primitives;

namespace Novolis.Cad.Unit;

public class GenericDrawingTests
{
    [Test]
    public async Task Cube_Assembly_Projects_Without_Domain_Types()
    {
        var doc = new CadDocument();
        var boxId = Guid.NewGuid();
        doc.Entities.Add(new CadEntity
        {
            Id = boxId,
            Kind = "box",
            Name = "cube-1",
            Points = [[0f, 1f, 0f], [0.5f, 0.5f, 0.5f]],
        });

        var reg = new CadHierarchyRegistry { RootId = "DOC" };
        reg.RegisterAssembly(new CadHierarchyAssembly(AssemblyId.Of("DOC"), "Document"));
        reg.RegisterAssembly(new CadHierarchyAssembly(
            AssemblyId.Of("ASM"), "Assembly", ParentAssemblyId: AssemblyId.Of("DOC")));
        reg.RegisterPart(new CadHierarchyPart(
            PartId.Of("PART-CUBE"), AssemblyId.Of("ASM"), "Cube", EntityId: boxId));

        var request = new DrawingRequest(
            "ASM",
            [new DrawingViewDefinition("A", DrawingViewKind.Orthographic, StandardViewOrientation.PlanSternLeft)],
            new DrawingReferenceSet(DrawingReferenceFlags.None),
            new DimensionPolicy(DimensionPolicyKind.OverallExtents));

        var result = UniversalDrawingEngine.Render(request, doc, reg);
        await Assert.That(result.Resolved.HasDrawableGeometry).IsTrue();
        await Assert.That(result.Manifest.Ok).IsTrue();
        await Assert.That(result.PrimaryProjection).IsNotNull();
        await Assert.That(result.Manifest.Measurements.Count).IsGreaterThan(0);
    }

    [Test]
    public async Task Section_Intersects_Box_Mesh()
    {
        var doc = new CadDocument();
        var boxId = Guid.NewGuid();
        doc.Entities.Add(new CadEntity
        {
            Id = boxId,
            Kind = "box",
            Name = "slab",
            Points = [[0f, 1f, 5f], [2f, 1f, 2f]],
        });

        var reg = new CadHierarchyRegistry { RootId = "DOC" };
        reg.RegisterAssembly(new CadHierarchyAssembly(AssemblyId.Of("DOC"), "Document"));
        reg.RegisterPart(new CadHierarchyPart(
            PartId.Of("P1"), AssemblyId.Of("DOC"), "Slab", EntityId: boxId));

        var resolved = SubjectGeometryResolver.Resolve("DOC", reg, doc);
        var edges = MeshSectioner.Section(
            resolved.Geometry,
            DrawingPlane.FromPointNormal(new Vector3(0, 0, 5), Vector3.UnitZ));
        await Assert.That(edges.Count).IsGreaterThan(0);
    }
}

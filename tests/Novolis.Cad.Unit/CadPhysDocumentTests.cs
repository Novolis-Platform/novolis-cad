using System.Text.Json;
using Novolis.Cad.Primitives;

namespace Novolis.Cad.Unit;

public sealed class CadPhysDocumentTests
{
    [Test]
    public async Task CadPhysDocument_JsonRoundTrip_PreservesMeshesAndColliders()
    {
        var meshId = Guid.NewGuid();
        var entityId = Guid.NewGuid();
        var doc = new CadPhysDocument
        {
            Name = "PhysRoundTrip",
            UnitScaleMeters = 0.001f,
            LinearUnit = "millimeter",
            Meshes =
            [
                new CadMesh
                {
                    Id = meshId,
                    EntityId = entityId,
                    Name = "Hull",
                    Vertices = [[0, 0, 0], [1, 0, 0], [0, 1, 0]],
                    Indices = [0, 1, 2],
                    Normals = [[0, 1, 0]],
                    Winding = "ccw",
                    Space = "local",
                    Material = "steel",
                },
            ],
            Colliders =
            [
                new CadCollider
                {
                    EntityId = entityId,
                    Kind = "box",
                    Center = [0, 0.5f, 0],
                    HalfExtents = [1, 0.5f, 1],
                    IsTrigger = false,
                    Body = new CadColliderBody
                    {
                        Mass = 1200f,
                        InertiaDiagonal = [100, 200, 300],
                        Kinematic = true,
                    },
                },
                new CadCollider
                {
                    Kind = "mesh",
                    MeshId = meshId,
                    IsTrigger = true,
                    Radius = 0.5f,
                },
            ],
        };

        var json = JsonSerializer.Serialize(doc);
        var loaded = JsonSerializer.Deserialize<CadPhysDocument>(json);
        await Assert.That(loaded).IsNotNull();
        await Assert.That(loaded!.Format).IsEqualTo("novolis.cad.phys");
        await Assert.That(loaded.SchemaVersion).IsEqualTo(1);
        await Assert.That(loaded.Name).IsEqualTo("PhysRoundTrip");
        await Assert.That(loaded.UpAxis).IsEqualTo("y");
        await Assert.That(loaded.Meshes.Count).IsEqualTo(1);
        await Assert.That(loaded.Meshes[0].Id).IsEqualTo(meshId);
        await Assert.That(loaded.Meshes[0].Indices).IsEquivalentTo([0, 1, 2]);
        await Assert.That(loaded.Colliders.Count).IsEqualTo(2);
        await Assert.That(loaded.Colliders[0].Body!.Kinematic).IsTrue();
        await Assert.That(loaded.Colliders[1].MeshId).IsEqualTo(meshId);
    }

    [Test]
    public async Task CadMesh_and_CadCollider_property_defaults()
    {
        var mesh = new CadMesh();
        await Assert.That(mesh.Winding).IsEqualTo("ccw");
        await Assert.That(mesh.Space).IsEqualTo("local");

        var collider = new CadCollider();
        await Assert.That(collider.Kind).IsEqualTo("box");
        await Assert.That(collider.Body).IsNull();

        var body = new CadColliderBody();
        await Assert.That(body.Mass).IsEqualTo(1f);
        await Assert.That(body.InertiaDiagonal).IsEquivalentTo(new float[] { 1f, 1f, 1f });
    }
}

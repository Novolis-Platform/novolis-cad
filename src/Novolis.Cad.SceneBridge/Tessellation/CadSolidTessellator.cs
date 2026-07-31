using System.Numerics;
using Novolis.Cad.Primitives;
using Novolis.Math.Geometry;

namespace Novolis.Cad.SceneBridge.Tessellation;

/// <summary>Tessellates analytic CAD solids into <see cref="EditableMesh"/>.</summary>
public static class CadSolidTessellator
{
    public static EditableMesh? TryTessellate(CadEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        switch (entity.Kind.ToLowerInvariant())
        {
            case "box" when CadShipGeometry.TryGetBox(entity, out var center, out var he):
            {
                var mesh = Box(he);
                mesh.Transform(Matrix4x4.CreateTranslation(center));
                return mesh;
            }
            case "sphere" when entity.Center is not null:
            {
                var mesh = Sphere(entity.Radius, 12, 16);
                mesh.Transform(Matrix4x4.CreateTranslation(CadVec.To(entity.Center)));
                return mesh;
            }
            case "cylinder" when entity.Center is not null:
            {
                var mesh = Cylinder(entity.Radius, entity.Height, 24);
                mesh.Transform(Matrix4x4.CreateTranslation(CadVec.To(entity.Center)));
                return mesh;
            }
            case "mesh" when entity.MeshVertices is { Count: > 0 } && entity.MeshIndices is { Count: > 0 }:
                return FromStored(entity);
            default:
                return null;
        }
    }

    public static EditableMesh FromStored(CadEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        var verts = entity.MeshVertices!.Select(v => CadVec.To(v)).ToList();
        return new EditableMesh(verts, entity.MeshIndices!);
    }

    public static void StoreOnEntity(CadEntity entity, EditableMesh mesh)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentNullException.ThrowIfNull(mesh);
        entity.MeshVertices = mesh.Vertices.Select(v => CadVec.From(v)).ToList();
        entity.MeshIndices = mesh.Indices.ToList();
    }

    public static EditableMesh Box(Vector3 he)
    {
        var verts = new Vector3[]
        {
            new(-he.X, -he.Y, -he.Z), new(he.X, -he.Y, -he.Z), new(he.X, he.Y, -he.Z), new(-he.X, he.Y, -he.Z),
            new(-he.X, -he.Y, he.Z), new(he.X, -he.Y, he.Z), new(he.X, he.Y, he.Z), new(-he.X, he.Y, he.Z),
        };
        int[] inds =
        [
            0, 1, 2, 0, 2, 3,
            4, 6, 5, 4, 7, 6,
            0, 4, 5, 0, 5, 1,
            2, 6, 7, 2, 7, 3,
            0, 3, 7, 0, 7, 4,
            1, 5, 6, 1, 6, 2,
        ];
        return new EditableMesh(verts, inds);
    }

    public static EditableMesh Sphere(float radius, int rings, int slices)
    {
        var verts = new List<Vector3>();
        var inds = new List<int>();
        for (var y = 0; y <= rings; y++)
        {
            var v = y / (float)rings;
            var phi = v * MathF.PI;
            for (var x = 0; x <= slices; x++)
            {
                var u = x / (float)slices;
                var theta = u * MathF.PI * 2f;
                verts.Add(new Vector3(
                    MathF.Sin(phi) * MathF.Cos(theta) * radius,
                    MathF.Cos(phi) * radius,
                    MathF.Sin(phi) * MathF.Sin(theta) * radius));
            }
        }

        for (var y = 0; y < rings; y++)
        {
            for (var x = 0; x < slices; x++)
            {
                var i0 = y * (slices + 1) + x;
                var i1 = i0 + slices + 1;
                inds.Add(i0);
                inds.Add(i1);
                inds.Add(i0 + 1);
                inds.Add(i0 + 1);
                inds.Add(i1);
                inds.Add(i1 + 1);
            }
        }

        return new EditableMesh(verts, inds);
    }

    public static EditableMesh Cylinder(float radius, float height, int slices)
    {
        var verts = new List<Vector3>();
        var inds = new List<int>();
        var hy = height * 0.5f;
        for (var i = 0; i <= slices; i++)
        {
            var a = i / (float)slices * MathF.PI * 2f;
            var x = MathF.Cos(a) * radius;
            var z = MathF.Sin(a) * radius;
            verts.Add(new Vector3(x, -hy, z));
            verts.Add(new Vector3(x, hy, z));
        }

        for (var i = 0; i < slices; i++)
        {
            var i0 = i * 2;
            inds.Add(i0);
            inds.Add(i0 + 1);
            inds.Add(i0 + 2);
            inds.Add(i0 + 2);
            inds.Add(i0 + 1);
            inds.Add(i0 + 3);
        }

        return new EditableMesh(verts, inds);
    }
}

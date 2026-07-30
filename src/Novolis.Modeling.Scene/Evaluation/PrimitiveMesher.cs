using System.Numerics;
using Novolis.Math.Geometry;

namespace Novolis.Modeling.Scene;

/// <summary>Tessellates mesh primitives into <see cref="EditableMesh"/>.</summary>
public static class PrimitiveMesher
{
    public static EditableMesh Tessellate(MeshNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        if (node.Vertices is { Length: > 0 } && node.Indices is { Length: > 0 })
        {
            var verts = new Vector3[node.Vertices.Length / 3];
            for (var i = 0; i < verts.Length; i++)
                verts[i] = new Vector3(node.Vertices[i * 3], node.Vertices[i * 3 + 1], node.Vertices[i * 3 + 2]);
            return new EditableMesh(verts, node.Indices);
        }

        var sx = MathF.Max(0.01f, node.Size.Length > 0 ? node.Size[0] : 1f);
        var sy = MathF.Max(0.01f, node.Size.Length > 1 ? node.Size[1] : 1f);
        var sz = MathF.Max(0.01f, node.Size.Length > 2 ? node.Size[2] : 1f);
        var segments = System.Math.Clamp(node.Segments <= 0 ? 16 : node.Segments, 4, 64);

        return node.Primitive switch
        {
            MeshPrimitiveKind.Sphere => Sphere(MathF.Max(sx, MathF.Max(sy, sz)) * 0.5f, segments),
            MeshPrimitiveKind.Plane => Plane(sx, sz),
            MeshPrimitiveKind.Cylinder => Cylinder(sx * 0.5f, sy, segments),
            MeshPrimitiveKind.Cone => Cone(sx * 0.5f, sy, segments),
            MeshPrimitiveKind.Capsule => Capsule(sx * 0.5f, sy, segments),
            MeshPrimitiveKind.Torus => Torus(sx * 0.5f, MathF.Max(0.05f, sz * 0.25f), segments, System.Math.Max(6, segments / 2)),
            MeshPrimitiveKind.Pyramid => Pyramid(sx, sy, sz),
            MeshPrimitiveKind.Disc => Disc(sx * 0.5f, segments),
            MeshPrimitiveKind.Tube => Tube(sx * 0.5f, MathF.Max(0.05f, sx * 0.25f), sy, segments),
            MeshPrimitiveKind.PlatonicTetra => PlatonicTetra(MathF.Max(sx, MathF.Max(sy, sz)) * 0.5f),
            MeshPrimitiveKind.PlatonicOcta => PlatonicOcta(MathF.Max(sx, MathF.Max(sy, sz)) * 0.5f),
            MeshPrimitiveKind.PlatonicIcosa => PlatonicIcosa(MathF.Max(sx, MathF.Max(sy, sz)) * 0.5f),
            MeshPrimitiveKind.PlatonicDodeca => PlatonicDodeca(MathF.Max(sx, MathF.Max(sy, sz)) * 0.5f),
            MeshPrimitiveKind.Landscape => Landscape(sx, sz, System.Math.Clamp(segments, 4, 32)),
            _ => Box(sx, sy, sz),
        };
    }

    public static EditableMesh Box(float sx, float sy, float sz)
    {
        var hx = sx * 0.5f;
        var hy = sy * 0.5f;
        var hz = sz * 0.5f;
        Vector3[] v =
        [
            new(-hx, -hy, -hz), new(hx, -hy, -hz), new(hx, hy, -hz), new(-hx, hy, -hz),
            new(-hx, -hy, hz), new(hx, -hy, hz), new(hx, hy, hz), new(-hx, hy, hz),
        ];
        int[] i =
        [
            0, 1, 2, 0, 2, 3,
            4, 6, 5, 4, 7, 6,
            0, 4, 5, 0, 5, 1,
            2, 6, 7, 2, 7, 3,
            0, 3, 7, 0, 7, 4,
            1, 5, 6, 1, 6, 2,
        ];
        return new EditableMesh(v, i);
    }

    public static EditableMesh Plane(float sx, float sz)
    {
        var hx = sx * 0.5f;
        var hz = sz * 0.5f;
        Vector3[] v =
        [
            new(-hx, 0, -hz), new(hx, 0, -hz), new(hx, 0, hz), new(-hx, 0, hz),
        ];
        int[] i = [0, 1, 2, 0, 2, 3];
        return new EditableMesh(v, i);
    }

    public static EditableMesh Sphere(float radius, int segments)
    {
        var mesh = new EditableMesh();
        var rings = segments;
        var slices = segments * 2;
        for (var y = 0; y <= rings; y++)
        {
            var v = y / (float)rings;
            var phi = v * MathF.PI;
            for (var x = 0; x <= slices; x++)
            {
                var u = x / (float)slices;
                var theta = u * MathF.PI * 2f;
                var px = radius * MathF.Sin(phi) * MathF.Cos(theta);
                var py = radius * MathF.Cos(phi);
                var pz = radius * MathF.Sin(phi) * MathF.Sin(theta);
                mesh.AddVertex(new Vector3(px, py, pz));
            }
        }

        for (var y = 0; y < rings; y++)
        {
            for (var x = 0; x < slices; x++)
            {
                var i0 = y * (slices + 1) + x;
                var i1 = i0 + 1;
                var i2 = i0 + (slices + 1);
                var i3 = i2 + 1;
                mesh.AddTriangle(i0, i2, i1);
                mesh.AddTriangle(i1, i2, i3);
            }
        }

        return mesh;
    }

    public static EditableMesh Cylinder(float radius, float height, int segments)
    {
        var mesh = new EditableMesh();
        var hy = height * 0.5f;
        mesh.AddVertex(new Vector3(0, -hy, 0));
        mesh.AddVertex(new Vector3(0, hy, 0));
        for (var i = 0; i < segments; i++)
        {
            var a = i / (float)segments * MathF.PI * 2f;
            var x = MathF.Cos(a) * radius;
            var z = MathF.Sin(a) * radius;
            mesh.AddVertex(new Vector3(x, -hy, z));
            mesh.AddVertex(new Vector3(x, hy, z));
        }

        for (var i = 0; i < segments; i++)
        {
            var i0 = 2 + i * 2;
            var i1 = 2 + ((i + 1) % segments) * 2;
            var i2 = i0 + 1;
            var i3 = i1 + 1;
            mesh.AddTriangle(0, i1, i0);
            mesh.AddTriangle(1, i2, i3);
            mesh.AddTriangle(i0, i1, i3);
            mesh.AddTriangle(i0, i3, i2);
        }

        return mesh;
    }

    public static EditableMesh Cone(float radius, float height, int segments)
    {
        var mesh = new EditableMesh();
        var hy = height * 0.5f;
        mesh.AddVertex(new Vector3(0, -hy, 0));
        mesh.AddVertex(new Vector3(0, hy, 0));
        for (var i = 0; i < segments; i++)
        {
            var a = i / (float)segments * MathF.PI * 2f;
            mesh.AddVertex(new Vector3(MathF.Cos(a) * radius, -hy, MathF.Sin(a) * radius));
        }

        for (var i = 0; i < segments; i++)
        {
            var i0 = 2 + i;
            var i1 = 2 + (i + 1) % segments;
            mesh.AddTriangle(0, i1, i0);
            mesh.AddTriangle(1, i0, i1);
        }

        return mesh;
    }

    public static EditableMesh Capsule(float radius, float height, int segments)
    {
        var cylH = MathF.Max(0.01f, height - radius * 2f);
        var cyl = Cylinder(radius, cylH, segments);
        var top = Sphere(radius, System.Math.Max(8, segments / 2));
        top.Transform(Matrix4x4.CreateTranslation(0, cylH * 0.5f, 0));
        var bottom = Sphere(radius, System.Math.Max(8, segments / 2));
        bottom.Transform(Matrix4x4.CreateTranslation(0, -cylH * 0.5f, 0));
        return MeshBoolean.Concat(MeshBoolean.Concat(cyl, top), bottom);
    }

    public static EditableMesh Torus(float major, float minor, int segments, int tubeSegments)
    {
        var mesh = new EditableMesh();
        for (var i = 0; i <= segments; i++)
        {
            var u = i / (float)segments * MathF.PI * 2f;
            for (var j = 0; j <= tubeSegments; j++)
            {
                var v = j / (float)tubeSegments * MathF.PI * 2f;
                var cx = MathF.Cos(u) * major;
                var cz = MathF.Sin(u) * major;
                var x = cx + MathF.Cos(u) * MathF.Cos(v) * minor;
                var y = MathF.Sin(v) * minor;
                var z = cz + MathF.Sin(u) * MathF.Cos(v) * minor;
                mesh.AddVertex(new Vector3(x, y, z));
            }
        }

        for (var i = 0; i < segments; i++)
        {
            for (var j = 0; j < tubeSegments; j++)
            {
                var i0 = i * (tubeSegments + 1) + j;
                var i1 = i0 + 1;
                var i2 = i0 + (tubeSegments + 1);
                var i3 = i2 + 1;
                mesh.AddTriangle(i0, i2, i1);
                mesh.AddTriangle(i1, i2, i3);
            }
        }

        return mesh;
    }

    public static EditableMesh Pyramid(float sx, float sy, float sz)
    {
        var hx = sx * 0.5f;
        var hy = sy * 0.5f;
        var hz = sz * 0.5f;
        Vector3[] v =
        [
            new(-hx, -hy, -hz), new(hx, -hy, -hz), new(hx, -hy, hz), new(-hx, -hy, hz),
            new(0, hy, 0),
        ];
        int[] i =
        [
            0, 1, 2, 0, 2, 3,
            0, 1, 4, 1, 2, 4, 2, 3, 4, 3, 0, 4,
        ];
        return new EditableMesh(v, i);
    }

    public static EditableMesh Disc(float radius, int segments)
    {
        var mesh = new EditableMesh();
        mesh.AddVertex(Vector3.Zero);
        for (var i = 0; i < segments; i++)
        {
            var a = i / (float)segments * MathF.PI * 2f;
            mesh.AddVertex(new Vector3(MathF.Cos(a) * radius, 0, MathF.Sin(a) * radius));
        }

        for (var i = 0; i < segments; i++)
            mesh.AddTriangle(0, 1 + i, 1 + (i + 1) % segments);
        return mesh;
    }

    public static EditableMesh Tube(float outerRadius, float innerRadius, float height, int segments)
    {
        var mesh = new EditableMesh();
        var hy = height * 0.5f;
        innerRadius = MathF.Min(innerRadius, outerRadius * 0.9f);
        for (var i = 0; i < segments; i++)
        {
            var a = i / (float)segments * MathF.PI * 2f;
            var c = MathF.Cos(a);
            var s = MathF.Sin(a);
            mesh.AddVertex(new Vector3(c * outerRadius, -hy, s * outerRadius));
            mesh.AddVertex(new Vector3(c * outerRadius, hy, s * outerRadius));
            mesh.AddVertex(new Vector3(c * innerRadius, -hy, s * innerRadius));
            mesh.AddVertex(new Vector3(c * innerRadius, hy, s * innerRadius));
        }

        for (var i = 0; i < segments; i++)
        {
            var i0 = i * 4;
            var i1 = ((i + 1) % segments) * 4;
            // outer wall
            mesh.AddTriangle(i0, i1, i1 + 1);
            mesh.AddTriangle(i0, i1 + 1, i0 + 1);
            // inner wall
            mesh.AddTriangle(i0 + 2, i0 + 3, i1 + 3);
            mesh.AddTriangle(i0 + 2, i1 + 3, i1 + 2);
            // bottom ring
            mesh.AddTriangle(i0, i0 + 2, i1 + 2);
            mesh.AddTriangle(i0, i1 + 2, i1);
            // top ring
            mesh.AddTriangle(i0 + 1, i1 + 1, i1 + 3);
            mesh.AddTriangle(i0 + 1, i1 + 3, i0 + 3);
        }

        return mesh;
    }

    public static EditableMesh PlatonicTetra(float radius)
    {
        var a = radius / MathF.Sqrt(3f);
        Vector3[] v =
        [
            new(a, a, a), new(a, -a, -a), new(-a, a, -a), new(-a, -a, a),
        ];
        int[] i = [0, 1, 2, 0, 3, 1, 0, 2, 3, 1, 3, 2];
        return new EditableMesh(v, i);
    }

    public static EditableMesh PlatonicOcta(float radius)
    {
        Vector3[] v =
        [
            new(radius, 0, 0), new(-radius, 0, 0),
            new(0, radius, 0), new(0, -radius, 0),
            new(0, 0, radius), new(0, 0, -radius),
        ];
        int[] i =
        [
            0, 2, 4, 0, 4, 3, 0, 3, 5, 0, 5, 2,
            1, 4, 2, 1, 3, 4, 1, 5, 3, 1, 2, 5,
        ];
        return new EditableMesh(v, i);
    }

    public static EditableMesh PlatonicIcosa(float radius)
    {
        var phi = (1f + MathF.Sqrt(5f)) * 0.5f;
        var verts = new List<Vector3>
        {
            new(-1, phi, 0), new(1, phi, 0), new(-1, -phi, 0), new(1, -phi, 0),
            new(0, -1, phi), new(0, 1, phi), new(0, -1, -phi), new(0, 1, -phi),
            new(phi, 0, -1), new(phi, 0, 1), new(-phi, 0, -1), new(-phi, 0, 1),
        };
        for (var i = 0; i < verts.Count; i++)
            verts[i] = Vector3.Normalize(verts[i]) * radius;

        int[] faces =
        [
            0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11,
            1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
            3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9,
            4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
        ];
        return new EditableMesh(verts, faces);
    }

    public static EditableMesh PlatonicDodeca(float radius)
    {
        // Approximate dodecahedron as subdivided cube corners (C4D-lite stand-in).
        var mesh = Box(radius * 1.2f, radius * 1.2f, radius * 1.2f);
        return MeshShaping.BevelLite(mesh, 0.35f);
    }

    public static EditableMesh Landscape(float sx, float sz, int divisions)
    {
        var mesh = new EditableMesh();
        var hx = sx * 0.5f;
        var hz = sz * 0.5f;
        for (var z = 0; z <= divisions; z++)
        {
            for (var x = 0; x <= divisions; x++)
            {
                var u = x / (float)divisions;
                var v = z / (float)divisions;
                var px = -hx + u * sx;
                var pz = -hz + v * sz;
                var h = 0.15f * MathF.Sin(u * 6f) * MathF.Cos(v * 5f)
                        + 0.08f * MathF.Sin(u * 14f + v * 9f);
                mesh.AddVertex(new Vector3(px, h, pz));
            }
        }

        var stride = divisions + 1;
        for (var z = 0; z < divisions; z++)
        {
            for (var x = 0; x < divisions; x++)
            {
                var i0 = z * stride + x;
                var i1 = i0 + 1;
                var i2 = i0 + stride;
                var i3 = i2 + 1;
                mesh.AddTriangle(i0, i2, i1);
                mesh.AddTriangle(i1, i2, i3);
            }
        }

        return mesh;
    }
}

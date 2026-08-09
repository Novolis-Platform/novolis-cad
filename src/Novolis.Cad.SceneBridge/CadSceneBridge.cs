using System.Numerics;
using Novolis.Cad.Primitives;
using Novolis.Cad.SceneBridge.Tessellation;
using Novolis.Math.Geometry;
using Novolis._3D;

namespace Novolis.Cad.SceneBridge;

/// <summary>Converts a <see cref="CadDocument"/> into a mesh <see cref="SceneDocument"/>.</summary>
public static class CadSceneBridge
{
    public static SceneDocument ToSceneDocument(CadDocument cad, CadSceneBridgeOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(cad);
        options ??= new CadSceneBridgeOptions();

        var doc = CreateBlank(cad.Name);
        var root = doc.Roots().OfType<GroupNode>().First();
        var materials = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);

        foreach (var entity in cad.Entities)
        {
            var kind = entity.Kind.ToLowerInvariant();
            if (kind is "camera")
            {
                AddCamera(doc, root.Id, entity);
                continue;
            }

            if (kind is "light")
            {
                AddLight(doc, root.Id, entity);
                continue;
            }

            if (kind is "material")
            {
                EnsureMaterial(doc, root.Id, materials, entity.Name ?? entity.Id.ToString(), ColorFromEntity(entity));
                continue;
            }

            EditableMesh? mesh = kind == "space"
                ? CadSpaceTessellator.TryTessellate(entity, options.IncludeSpaceCeilings)
                : CadEntityTessellator.TryTessellate(entity);
            if (mesh is null)
                continue;

            var meshNode = new MeshNode
            {
                Name = string.IsNullOrWhiteSpace(entity.Name) ? entity.Kind : entity.Name,
                ParentId = root.Id,
                Primitive = MeshPrimitiveKind.Box,
            };
            MeshEditBake.WriteBaked(meshNode, mesh);

            var materialKey = ResolveMaterialKey(entity);
            if (!string.IsNullOrWhiteSpace(materialKey))
            {
                var matId = EnsureMaterial(doc, root.Id, materials, materialKey!, ColorFromMaterialName(materialKey!));
                meshNode.MaterialId = matId;
            }

            ApplyWallSideMaterials(doc, root.Id, materials, entity, meshNode);
            doc.Nodes.Add(meshNode);
        }

        if (options.EnsureStudioLights && !doc.Nodes.OfType<LightNode>().Any())
            EnsureStudioLights(doc, root.Id);

        if (doc.ActiveCameraId is null)
        {
            var cam = doc.Nodes.OfType<CameraNode>().FirstOrDefault();
            if (cam is not null)
                doc.ActiveCameraId = cam.Id;
        }

        doc.ModifiedAt = DateTimeOffset.UtcNow;
        return doc;
    }

    public static void SaveNov3dJson(CadDocument cad, string path, CadSceneBridgeOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var scene = ToSceneDocument(cad, options);
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        SceneSerializer.Save(scene, path);
    }

    private static SceneDocument CreateBlank(string? name)
    {
        var doc = new SceneDocument
        {
            Name = string.IsNullOrWhiteSpace(name) ? "Cad Bridge" : name!,
            CreatedAt = DateTimeOffset.UtcNow,
            ModifiedAt = DateTimeOffset.UtcNow,
            Generator = "Novolis.Cad.SceneBridge",
        };
        doc.Nodes.Add(new GroupNode { Name = "Scene" });
        return doc;
    }

    private static string? ResolveMaterialKey(CadEntity entity)
    {
        if (!string.IsNullOrWhiteSpace(entity.Material))
            return entity.Material;
        return null;
    }

    private static void ApplyWallSideMaterials(
        SceneDocument doc,
        Guid rootId,
        Dictionary<string, Guid> materials,
        CadEntity entity,
        MeshNode meshNode)
    {
        if (!string.Equals(entity.Kind, "wall", StringComparison.OrdinalIgnoreCase) || entity.Sides is null)
            return;

        // Side shapes map to materials; mesh uses Side A when present, else B, else entity.Material.
        var shapeId = entity.Sides.A?.ShapeId ?? entity.Sides.B?.ShapeId;
        if (shapeId is null)
            return;

        var key = shapeId.Value.ToString("N");
        meshNode.MaterialId = EnsureMaterial(doc, rootId, materials, key, ColorFromMaterialName(key));
    }

    private static Guid EnsureMaterial(
        SceneDocument doc,
        Guid rootId,
        Dictionary<string, Guid> materials,
        string key,
        float[] color)
    {
        if (materials.TryGetValue(key, out var existing))
            return existing;

        var mat = new MaterialNode
        {
            Name = key,
            ParentId = rootId,
            Color = color,
        };
        doc.Nodes.Add(mat);
        materials[key] = mat.Id;
        return mat.Id;
    }

    private static float[] ColorFromEntity(CadEntity entity)
    {
        if (entity.Color is { Length: >= 3 })
            return [entity.Color[0], entity.Color[1], entity.Color[2]];
        return ColorFromMaterialName(entity.Name ?? "Material");
    }

    private static float[] ColorFromMaterialName(string name)
    {
        var hash = name.GetHashCode(StringComparison.OrdinalIgnoreCase);
        var r = ((hash >> 16) & 0xFF) / 255f;
        var g = ((hash >> 8) & 0xFF) / 255f;
        var b = (hash & 0xFF) / 255f;
        return
        [
            0.35f + r * 0.5f,
            0.35f + g * 0.5f,
            0.35f + b * 0.5f,
        ];
    }

    private static void AddCamera(SceneDocument doc, Guid rootId, CadEntity entity)
    {
        var pos = entity.Center is not null
            ? CadVec.To(entity.Center)
            : entity.A is not null
                ? CadVec.To(entity.A)
                : new Vector3(5, 3.5f, 7);
        var target = entity.B is not null ? CadVec.To(entity.B) : new Vector3(0, 0.5f, 0);
        var cam = new CameraNode
        {
            Name = string.IsNullOrWhiteSpace(entity.Name) ? "Camera" : entity.Name!,
            ParentId = rootId,
            Transform = new SceneTransform { Position = [pos.X, pos.Y, pos.Z] },
            Target = [target.X, target.Y, target.Z],
            FovDeg = 45f,
        };
        doc.Nodes.Add(cam);
        doc.ActiveCameraId ??= cam.Id;
    }

    private static void AddLight(SceneDocument doc, Guid rootId, CadEntity entity)
    {
        var pos = entity.Center is not null ? CadVec.To(entity.Center) : new Vector3(2, 4, 2);
        var kind = (entity.LightType ?? "omni").ToLowerInvariant() switch
        {
            "spot" => LightKind.Spot,
            "infinite" or "directional" => LightKind.Infinite,
            "area" => LightKind.Area,
            _ => LightKind.Omni,
        };
        doc.Nodes.Add(new LightNode
        {
            Name = string.IsNullOrWhiteSpace(entity.Name) ? "Light" : entity.Name!,
            ParentId = rootId,
            LightKind = kind,
            Intensity = entity.Intensity > 0 ? entity.Intensity : 1f,
            Transform = new SceneTransform { Position = [pos.X, pos.Y, pos.Z] },
        });
    }

    private static void EnsureStudioLights(SceneDocument doc, Guid rootId)
    {
        doc.Nodes.Add(new LightNode
        {
            Name = "Key",
            ParentId = rootId,
            LightKind = LightKind.Spot,
            Intensity = 3.8f,
            ConeAngleDeg = 40f,
            Transform = new SceneTransform { Position = [22f, 16f, 18f], RotationDeg = [40f, -35f, 0f] },
        });
        doc.Nodes.Add(new LightNode
        {
            Name = "Fill",
            ParentId = rootId,
            LightKind = LightKind.Omni,
            Intensity = 2f,
            Transform = new SceneTransform { Position = [0f, 2f, -18f] },
        });
        doc.Nodes.Add(new LightNode
        {
            Name = "Rim",
            ParentId = rootId,
            LightKind = LightKind.Infinite,
            Intensity = 0.55f,
            Transform = new SceneTransform { RotationDeg = [-55f, 30f, 0f] },
        });
    }
}

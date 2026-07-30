using System.Text.Json.Serialization;

namespace Novolis.Modeling.Scene;

/// <summary>Authoring document persisted as <c>.nov3djson</c>.</summary>
public sealed class SceneDocument
{
    public string Format { get; set; } = "novolis.scene";
    public int SchemaVersion { get; set; } = 1;
    public string Name { get; set; } = "Untitled";
    public string? Generator { get; set; }
    public DateTimeOffset? CreatedAt { get; set; }
    public DateTimeOffset? ModifiedAt { get; set; }
    public float UnitScaleMeters { get; set; } = 1f;
    public List<SceneNode> Nodes { get; set; } = [];
    public Guid? ActiveCameraId { get; set; }
    public Guid? SelectionId { get; set; }
    public Dictionary<string, string>? Properties { get; set; }

    /// <summary>Runtime mesh-edit / display state (not written to .nov3djson).</summary>
    [JsonIgnore]
    public MeshEditState Edit { get; } = new();

    public SceneNode? Find(Guid id) => Nodes.FirstOrDefault(n => n.Id == id);

    public IEnumerable<SceneNode> ChildrenOf(Guid? parentId) =>
        Nodes.Where(n => n.ParentId == parentId);

    public IReadOnlyList<SceneNode> Roots() => ChildrenOf(null).ToList();

    public bool TryRemove(Guid id)
    {
        var node = Find(id);
        if (node is null)
            return false;
        Nodes.Remove(node);
        foreach (var child in Nodes.Where(n => n.ParentId == id).ToList())
            child.ParentId = node.ParentId;
        if (SelectionId == id)
            SelectionId = null;
        if (ActiveCameraId == id)
            ActiveCameraId = null;
        return true;
    }

    public static SceneDocument CreateEmpty(string name = "Untitled") => CreatePrimitiveStage(name);

    public static SceneDocument CreatePrimitiveStage(string name = "Primitive Stage")
    {
        var doc = BaseShell(name);
        var root = doc.Roots().OfType<GroupNode>().First();
        doc.Nodes.Add(new MeshNode
        {
            Name = "Box",
            ParentId = root.Id,
            Primitive = MeshPrimitiveKind.Box,
            Size = [1, 1, 1],
            Transform = new SceneTransform { Position = [-1.5f, 0.5f, 0] },
        });
        doc.Nodes.Add(new MeshNode
        {
            Name = "Sphere",
            ParentId = root.Id,
            Primitive = MeshPrimitiveKind.Sphere,
            Size = [1, 1, 1],
            Segments = 20,
            Transform = new SceneTransform { Position = [0, 0.6f, 0] },
        });
        doc.Nodes.Add(new MeshNode
        {
            Name = "Cylinder",
            ParentId = root.Id,
            Primitive = MeshPrimitiveKind.Cylinder,
            Size = [0.8f, 1.2f, 0.8f],
            Transform = new SceneTransform { Position = [1.5f, 0.6f, 0] },
        });
        doc.Nodes.Add(new MeshNode
        {
            Name = "Cone",
            ParentId = root.Id,
            Primitive = MeshPrimitiveKind.Cone,
            Size = [0.9f, 1.2f, 0.9f],
            Transform = new SceneTransform { Position = [0, 0.6f, 1.8f] },
        });
        doc.Nodes.Add(new MeshNode
        {
            Name = "Torus",
            ParentId = root.Id,
            Primitive = MeshPrimitiveKind.Torus,
            Size = [1.2f, 1f, 0.8f],
            Segments = 24,
            Transform = new SceneTransform { Position = [0, 0.4f, -1.8f] },
        });
        return doc;
    }

    public static SceneDocument CreateClonerRow(string name = "Array Row")
    {
        var doc = BaseShell(name);
        var root = doc.Roots().OfType<GroupNode>().First();
        var box = new MeshNode
        {
            Name = "Source Box",
            ParentId = root.Id,
            Primitive = MeshPrimitiveKind.Box,
            Size = [0.7f, 0.7f, 0.7f],
            Transform = new SceneTransform { Position = [0, 0.35f, 0] },
        };
        doc.Nodes.Add(box);
        doc.Nodes.Add(new GeneratorNode
        {
            Name = "Array",
            ParentId = root.Id,
            Generator = GeneratorKind.Cloner,
            SourceId = box.Id,
            Count = 5,
            Offset = [1.2f, 0, 0],
        });
        return doc;
    }

    public static SceneDocument CreateBooleCut(string name = "Boolean Cut")
    {
        var doc = BaseShell(name);
        var root = doc.Roots().OfType<GroupNode>().First();
        var target = new MeshNode
        {
            Name = "Target",
            ParentId = root.Id,
            Primitive = MeshPrimitiveKind.Box,
            Size = [2, 1.2f, 2],
            Transform = new SceneTransform { Position = [0, 0.6f, 0] },
        };
        var cutter = new MeshNode
        {
            Name = "Cutter",
            ParentId = root.Id,
            Primitive = MeshPrimitiveKind.Cylinder,
            Size = [1.2f, 2f, 1.2f],
            Transform = new SceneTransform { Position = [0, 0.6f, 0] },
        };
        doc.Nodes.Add(target);
        doc.Nodes.Add(cutter);
        doc.Nodes.Add(new GeneratorNode
        {
            Name = "Boolean Difference",
            ParentId = root.Id,
            Generator = GeneratorKind.Boole,
            TargetId = target.Id,
            CutterId = cutter.Id,
            BooleanKind = BooleanKind.Difference,
        });
        return doc;
    }

    public static SceneDocument CreateLookSetup(string name = "Lights")
    {
        var doc = BaseShell(name);
        doc.Nodes.RemoveAll(n => n is LightNode);
        var root = doc.Roots().OfType<GroupNode>().First();
        doc.Nodes.Add(new MeshNode
        {
            Name = "Subject",
            ParentId = root.Id,
            Primitive = MeshPrimitiveKind.Capsule,
            Size = [0.8f, 1.6f, 0.8f],
            Transform = new SceneTransform { Position = [0, 0.9f, 0] },
        });
        doc.Nodes.Add(new LightNode
        {
            Name = "Key Spot",
            ParentId = root.Id,
            LightKind = LightKind.Spot,
            Intensity = 3.5f,
            ConeAngleDeg = 35f,
            Transform = new SceneTransform { Position = [2, 3, 2], RotationDeg = [35, -30, 0] },
        });
        doc.Nodes.Add(new LightNode
        {
            Name = "Sun",
            ParentId = root.Id,
            LightKind = LightKind.Infinite,
            Intensity = 0.4f,
            Transform = new SceneTransform { RotationDeg = [-45, 20, 0] },
        });
        doc.Nodes.Add(new LightNode
        {
            Name = "Fill Area",
            ParentId = root.Id,
            LightKind = LightKind.Area,
            Intensity = 0.7f,
            AreaSize = [2, 1],
            Transform = new SceneTransform { Position = [-2.5f, 1.5f, 1] },
        });
        return doc;
    }

    public static SceneDocument CreateEditBox(string name = "Edit Box")
    {
        var doc = BaseShell(name);
        var root = doc.Roots().OfType<GroupNode>().First();
        var box = new MeshNode
        {
            Name = "Editable Box",
            ParentId = root.Id,
            Primitive = MeshPrimitiveKind.Box,
            Size = [1.5f, 1.5f, 1.5f],
            Transform = new SceneTransform { Position = [0, 0.75f, 0] },
        };
        var baked = PrimitiveMesher.Tessellate(box);
        MeshEditBake.WriteBaked(box, baked);
        doc.Nodes.Add(box);
        doc.SelectionId = box.Id;
        doc.Edit.EditMeshId = box.Id;
        doc.Edit.Mode = SceneEditMode.Polygon;
        doc.Edit.SelectedFaces.Add(0);
        return doc;
    }

    public static SceneDocument CreatePrimitiveGallery(string name = "Primitive Gallery")
    {
        var doc = BaseShell(name);
        var root = doc.Roots().OfType<GroupNode>().First();
        var kinds = Enum.GetValues<MeshPrimitiveKind>();
        var i = 0;
        foreach (var kind in kinds)
        {
            var col = i % 5;
            var row = i / 5;
            doc.Nodes.Add(new MeshNode
            {
                Name = kind.ToString(),
                ParentId = root.Id,
                Primitive = kind,
                Size = [0.9f, 0.9f, 0.9f],
                Segments = kind == MeshPrimitiveKind.Landscape ? 12 : 16,
                Transform = new SceneTransform
                {
                    Position = [col * 2.2f - 4.4f, 0.5f, row * 2.2f - 2.2f],
                },
            });
            i++;
        }

        return doc;
    }

    // Back-compat aliases
    public static SceneDocument CreateSpotRimSample() => CreateLookSetup("Lights");
    public static SceneDocument CreateMultiLightStudio() => CreateLookSetup("Lights");

    private static SceneDocument BaseShell(string name)
    {
        var doc = new SceneDocument
        {
            Name = name,
            CreatedAt = DateTimeOffset.UtcNow,
            ModifiedAt = DateTimeOffset.UtcNow,
        };
        var root = new GroupNode { Name = "Scene" };
        var cam = new CameraNode
        {
            Name = "Camera",
            ParentId = root.Id,
            Transform = new SceneTransform { Position = [5, 3.5f, 7] },
            Target = [0, 0.5f, 0],
        };
        var key = new LightNode
        {
            Name = "Key",
            ParentId = root.Id,
            LightKind = LightKind.Omni,
            Intensity = 2f,
            Transform = new SceneTransform { Position = [2, 4, 2] },
        };
        var floor = new MeshNode
        {
            Name = "Floor",
            ParentId = root.Id,
            Primitive = MeshPrimitiveKind.Plane,
            Size = [10, 0.05f, 10],
        };
        doc.Nodes.AddRange([root, cam, key, floor]);
        doc.ActiveCameraId = cam.Id;
        return doc;
    }
}

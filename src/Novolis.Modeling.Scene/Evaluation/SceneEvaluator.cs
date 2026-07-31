using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Novolis.Modeling.Scene;

/// <summary>JSON load/save for <c>.nov3djson</c>.</summary>
public static class SceneSerializer
{
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public static string Serialize(SceneDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        document.ModifiedAt = DateTimeOffset.UtcNow;
        return JsonSerializer.Serialize(document, JsonOptions);
    }

    public static SceneDocument Deserialize(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        var doc = JsonSerializer.Deserialize<SceneDocument>(json, JsonOptions)
                  ?? throw new InvalidOperationException("Failed to deserialize scene document.");
        if (!string.Equals(doc.Format, "novolis.scene", StringComparison.Ordinal))
            throw new InvalidOperationException($"Unexpected format '{doc.Format}'.");
        return doc;
    }

    public static void Save(SceneDocument document, string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        File.WriteAllText(path, Serialize(document));
    }

    public static SceneDocument Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return Deserialize(File.ReadAllText(path));
    }
}

/// <summary>World-space evaluated node for lights/cameras/materials.</summary>
public sealed class EvaluatedNode
{
    public required SceneNode Source { get; init; }
    public required Matrix4x4 WorldMatrix { get; init; }
    public required Vector3 WorldPosition { get; init; }
}

/// <summary>Staged evaluation cache.</summary>
public sealed class LookCache
{
    public IReadOnlyList<EvaluatedNode> Lights { get; init; } = [];
    public IReadOnlyList<EvaluatedNode> Cameras { get; init; } = [];
    public IReadOnlyList<EvaluatedNode> Meshes { get; init; } = [];
    public IReadOnlyList<EvaluatedMesh> EvaluatedMeshes { get; init; } = [];
    public IReadOnlyList<EvaluatedNode> Materials { get; init; } = [];
    public int MeshGeneration { get; init; }
    public int LookGeneration { get; init; }
}

/// <summary>Staged evaluator with narrow invalidation.</summary>
public sealed class SceneEvaluator
{
    private SceneDocument? _document;
    private LookCache _cache = new();
    private int _meshGeneration;
    private int _lookGeneration;
    private int _builtMeshGeneration = -1;
    private int _builtLookGeneration = -1;

    public LookCache Cache => EnsureBuilt();

    /// <summary>Advances when mesh topology / transforms that affect mesh eval change.</summary>
    public int MeshGeneration => _meshGeneration;

    /// <summary>Advances when lights/cameras/materials change.</summary>
    public int LookGeneration => _lookGeneration;

    public void Bind(SceneDocument document)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        InvalidateAll();
    }

    public void InvalidateMesh() => _meshGeneration++;

    public void InvalidateLook() => _lookGeneration++;

    public void InvalidateAll()
    {
        _meshGeneration++;
        _lookGeneration++;
    }

    public void NotifyNodeChanged(SceneNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        if (node is LightNode or CameraNode or MaterialNode)
            InvalidateLook();
        else
            InvalidateAll();
    }

    private LookCache EnsureBuilt()
    {
        if (_document is null)
            return _cache;

        if (_builtMeshGeneration == _meshGeneration && _builtLookGeneration == _lookGeneration)
            return _cache;

        var worlds = BuildWorldMatrices(_document);
        var meshNodes = new List<EvaluatedNode>();
        var lights = new List<EvaluatedNode>();
        var cameras = new List<EvaluatedNode>();
        var materials = new List<EvaluatedNode>();

        foreach (var node in _document.Nodes)
        {
            if (!node.Visible || !worlds.TryGetValue(node.Id, out var m))
                continue;

            var evaluated = new EvaluatedNode
            {
                Source = node,
                WorldMatrix = m,
                WorldPosition = Vector3.Transform(Vector3.Zero, m),
            };

            switch (node)
            {
                case MeshNode:
                    meshNodes.Add(evaluated);
                    break;
                case LightNode:
                    lights.Add(evaluated);
                    break;
                case CameraNode:
                    cameras.Add(evaluated);
                    break;
                case MaterialNode:
                    materials.Add(evaluated);
                    break;
            }
        }

        var evaluatedMeshes = MeshStackEvaluator.EvaluateDocument(_document, worlds);

        _cache = new LookCache
        {
            Meshes = meshNodes,
            EvaluatedMeshes = evaluatedMeshes,
            Lights = lights,
            Cameras = cameras,
            Materials = materials,
            MeshGeneration = _meshGeneration,
            LookGeneration = _lookGeneration,
        };
        _builtMeshGeneration = _meshGeneration;
        _builtLookGeneration = _lookGeneration;
        return _cache;
    }

    internal static Dictionary<Guid, Matrix4x4> BuildWorldMatrices(SceneDocument doc)
    {
        var local = doc.Nodes.ToDictionary(n => n.Id, n => n.Transform.ToMatrix());
        var world = new Dictionary<Guid, Matrix4x4>();
        var visiting = new HashSet<Guid>();

        Matrix4x4 Resolve(Guid id)
        {
            if (world.TryGetValue(id, out var cached))
                return cached;
            if (!visiting.Add(id))
                return Matrix4x4.Identity;

            var node = doc.Find(id);
            if (node is null)
            {
                visiting.Remove(id);
                return Matrix4x4.Identity;
            }

            var parent = node.ParentId is { } pid ? Resolve(pid) : Matrix4x4.Identity;
            var m = local[id] * parent;
            world[id] = m;
            visiting.Remove(id);
            return m;
        }

        foreach (var n in doc.Nodes)
            Resolve(n.Id);
        return world;
    }
}

using System.Text.Json;
using Novolis.Cad.Primitives;

namespace Novolis.Cad.Blueprint;

/// <summary>JSON I/O for <c>.cadblueprint.json</c> using shared <see cref="CadJson.Options"/>.</summary>
public static class CadBlueprintJson
{
    public const string FormatId = "novolis.cad.blueprint";

    public static string Serialize(CadBlueprint blueprint)
    {
        ArgumentNullException.ThrowIfNull(blueprint);
        return JsonSerializer.Serialize(blueprint, CadJson.Options);
    }

    public static CadBlueprint Deserialize(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new InvalidOperationException("CadBlueprint JSON is empty.");

        var bp = JsonSerializer.Deserialize<CadBlueprint>(json, CadJson.Options)
                 ?? throw new InvalidOperationException("CadBlueprint JSON deserialized to null.");

        if (!string.Equals(bp.Format, FormatId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Expected format '{FormatId}' but found '{bp.Format}'.");
        }

        return bp;
    }

    public static CadBlueprint Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!File.Exists(path))
            throw new FileNotFoundException($"CadBlueprint file not found: {path}", path);
        return Deserialize(File.ReadAllText(path));
    }

    public static void Save(CadBlueprint blueprint, string path)
    {
        ArgumentNullException.ThrowIfNull(blueprint);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        File.WriteAllText(path, Serialize(blueprint));
    }
}

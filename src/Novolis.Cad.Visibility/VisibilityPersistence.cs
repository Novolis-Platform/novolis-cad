using System.Text.Json;
using System.Text.Json.Serialization;

namespace Novolis.Cad.Visibility;

/// <summary>Persist hierarchy visibility overrides separately from geometry.</summary>
public static class VisibilityPersistence
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    public static string DefaultFileName => "visibility.json";

    public static void Save(HierarchyVisibilityService service, string path)
    {
        var snap = service.CaptureSnapshot();
        var dto = new VisibilityFileDto
        {
            Label = snap.Label,
            Overrides = snap.Overrides
                .Where(kv => kv.Value != LocalVisibility.Inherit)
                .ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase),
        };
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        File.WriteAllText(path, JsonSerializer.Serialize(dto, JsonOptions));
    }

    public static bool TryLoad(HierarchyVisibilityService service, string path, out int overrideCount)
    {
        overrideCount = 0;
        if (!File.Exists(path))
            return false;

        var dto = JsonSerializer.Deserialize<VisibilityFileDto>(File.ReadAllText(path), JsonOptions);
        if (dto?.Overrides is null)
            return false;

        service.ApplySnapshot(
            new VisibilitySnapshot
            {
                Label = dto.Label,
                Overrides = new Dictionary<string, LocalVisibility>(dto.Overrides, StringComparer.OrdinalIgnoreCase),
            },
            recordUndo: false,
            asPreset: dto.Label);
        overrideCount = dto.Overrides.Count;
        return true;
    }

    private sealed class VisibilityFileDto
    {
        public string? Label { get; set; }
        public Dictionary<string, LocalVisibility> Overrides { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }
}

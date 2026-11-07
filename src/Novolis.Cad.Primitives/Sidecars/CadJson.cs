using System.Text.Json;
using System.Text.Json.Serialization;

namespace Novolis.Cad.Primitives;

/// <summary>JSON options for .cadjson / sidecar interchange.</summary>
public static class CadJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true,
    };
}

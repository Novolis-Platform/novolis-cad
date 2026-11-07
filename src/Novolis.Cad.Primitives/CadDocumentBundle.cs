using System.Text.Json;

namespace Novolis.Cad.Primitives;

/// <summary>Read/write .cadjson + layers/shapes sidecars as a named bundle.</summary>
public static class CadDocumentBundle
{
    public static void Write(
        string directory,
        CadLayersDocument layers,
        CadShapesDocument shapes,
        CadDocument cad,
        string fileStem = "document")
    {
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, $"{fileStem}.cadlayers.json"), JsonSerializer.Serialize(layers, CadJson.Options));
        File.WriteAllText(Path.Combine(directory, $"{fileStem}.cadshapejson"), JsonSerializer.Serialize(shapes, CadJson.Options));
        File.WriteAllText(Path.Combine(directory, $"{fileStem}.cadjson"), JsonSerializer.Serialize(cad, CadJson.Options));
    }

    public static (CadLayersDocument Layers, CadShapesDocument Shapes, CadDocument Cad) Read(
        string directory,
        string fileStem = "document")
    {
        var layers = JsonSerializer.Deserialize<CadLayersDocument>(
            File.ReadAllText(Path.Combine(directory, $"{fileStem}.cadlayers.json")), CadJson.Options)
            ?? throw new InvalidOperationException($"Failed to read {fileStem}.cadlayers.json");
        var shapes = JsonSerializer.Deserialize<CadShapesDocument>(
            File.ReadAllText(Path.Combine(directory, $"{fileStem}.cadshapejson")), CadJson.Options)
            ?? throw new InvalidOperationException($"Failed to read {fileStem}.cadshapejson");
        var cad = JsonSerializer.Deserialize<CadDocument>(
            File.ReadAllText(Path.Combine(directory, $"{fileStem}.cadjson")), CadJson.Options)
            ?? throw new InvalidOperationException($"Failed to read {fileStem}.cadjson");
        return (layers, shapes, cad);
    }
}

# Novolis.Cad.SceneBridge

Avalonia-free bridge from `.cadjson` (`CadDocument`) to `.nov3djson` (`SceneDocument`). Tessellates solids, walls, and space floor plates; maps materials and copies camera/light entities.

## Install

```bash
dotnet add package Novolis.Cad.SceneBridge
```

**Prerequisites:** [.NET 10 SDK](https://dotnet.microsoft.com/download) (`net10.0`). References `Novolis.Cad.Primitives`, `Novolis.Modeling.Scene`, `Novolis.Math.Geometry`.

## Quick start

```csharp
using System.Text.Json;
using Novolis.Cad.Primitives;
using Novolis.Cad.SceneBridge;
using Novolis.Modeling.Scene;

var cad = JsonSerializer.Deserialize<CadDocument>(File.ReadAllText("room.cadjson"))!;

var scene = CadSceneBridge.ToSceneDocument(cad, new CadSceneBridgeOptions
{
    EnsureStudioLights = true,
    IncludeSpaceCeilings = false,
});

SceneSerializer.Save(scene, "room.nov3djson");

// Or one-shot:
CadSceneBridge.SaveNov3dJson(cad, "room.nov3djson", options);
```

## API

| API | Purpose |
|-----|---------|
| `CadSceneBridge.ToSceneDocument(cad, options?)` | Tessellate entities → `SceneDocument` |
| `CadSceneBridge.SaveNov3dJson(cad, path, options?)` | `ToSceneDocument` + `SceneSerializer.Save` |
| `CadSceneBridgeOptions.EnsureStudioLights` | Add Key/Fill/Rim when scene has no lights |
| `CadSceneBridgeOptions.IncludeSpaceCeilings` | Ceiling plates when tessellating `space` entities |
| `CadEntityTessellator.TryTessellate(CadEntity)` | Route entity to wall/space/solid tessellator |
| `CadSolidTessellator.TryTessellate` / `Box` / `Sphere` / `Cylinder` | Solid mesh generation |
| `CadWallTessellator.TryTessellate(CadEntity)` | Wall slab tessellation (A–B or polyline) |
| `CadSpaceTessellator.TryTessellate(space, includeCeiling)` | Space floor plate tessellation |

**Conversion behavior:** skips `camera`, `light`, `material` entities for meshing (handled separately); maps `entity.Material` or wall side `ShapeId` → `MaterialNode` + `MeshNode.MaterialId`.

## Related / dogfood

| Package / app | Notes |
|---------------|-------|
| [`Novolis.Cad.Primitives`](../Novolis.Cad.Primitives/README.md) | Input `CadDocument` DTOs |
| [`Novolis.Modeling.Scene`](../Novolis.Modeling.Scene/README.md) | Output `SceneDocument` / `.nov3djson` |
| [`Novolis.Avalonia.Cad`](../../novolis-avalonia/src/Novolis.Avalonia.Cad/README.md) | `exportscene` / `bridgescene` session commands |

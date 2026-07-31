# novolis-cad

Avalonia-free CAD interchange packages for Novolis.

## Packages

| Package | Role |
|---------|------|
| [`Novolis.Cad.Primitives`](src/Novolis.Cad.Primitives/README.md) | `.cadjson` / `.cadphys` DTOs, workspace enums, vec helpers |
| [`Novolis.Modeling.Scene`](src/Novolis.Modeling.Scene/README.md) | Mesh-first scene graph (`.nov3djson`), evaluation, mesh editing |
| [`Novolis.Cad.SceneBridge`](src/Novolis.Cad.SceneBridge/README.md) | `CadDocument` → `SceneDocument` tessellation bridge |
| [`Novolis.Modeling.Import`](src/Novolis.Modeling.Import/README.md) | Assimp-backed mesh import (FBX, OBJ, glTF, …) → `EditableMesh` |

Schemas: [novolis-governance](https://github.com/Novolis-Platform/novolis-governance) (`schemas/cad`). UI editor: [Novolis.Avalonia.Cad](https://github.com/Novolis-Platform/novolis-avalonia/tree/main/src/Novolis.Avalonia.Cad).

## Install

```bash
dotnet add package Novolis.Cad.Primitives
dotnet add package Novolis.Modeling.Scene
dotnet add package Novolis.Cad.SceneBridge
dotnet add package Novolis.Modeling.Import
```

**Prerequisites:** [.NET 10 SDK](https://dotnet.microsoft.com/download) (`net10.0`); `Novolis.*` packages from GitHub Packages at `2026.1.*`.

## Quick start

```csharp
using System.Text.Json;
using Novolis.Cad.Primitives;
using Novolis.Cad.SceneBridge;
using Novolis.Modeling.Scene;

var cad = JsonSerializer.Deserialize<CadDocument>(File.ReadAllText("room.cadjson"))!;
var scene = CadSceneBridge.ToSceneDocument(cad, new CadSceneBridgeOptions { EnsureStudioLights = true });
SceneSerializer.Save(scene, "room.nov3djson");
```

## Build

```powershell
dotnet build Novolis.Cad.slnx
dotnet test Novolis.Cad.slnx
```

Cross-repo local iteration: open `Novolis.Platform.slnx` (ProjectReference mode). Do not use local NuGet folder feeds.

## Dogfood

| App | Notes |
|-----|-------|
| [Novolis.Avalonia.Cad](../novolis-avalonia/src/Novolis.Avalonia.Cad) | Draft Studio / CAD Studio 3D editor |
| [SceneLab](../novolis-dogfooding/apps/avalonia/SceneLab) | `.nov3djson` preview and import |
| [CorellianFreighterBuilder](../novolis-dogfooding/apps/avalonia/SceneLab/tools/CorellianFreighterBuilder) | CLI `--import` mesh pipeline |

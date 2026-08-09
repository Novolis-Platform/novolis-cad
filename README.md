<!-- novolis-marketing:start -->
<p align="center">
  <a href="https://github.com/Novolis-Platform">
    <img src="https://raw.githubusercontent.com/Novolis-Platform/.github/main/brand/logo-brand-transparent.svg" width="360" alt="Novolis"/>
  </a>
</p>

<p align="center">
  <img src="https://raw.githubusercontent.com/Novolis-Platform/.github/main/brand/banners/novolis-cad.svg" width="100%" alt="novolis-cad"/>
</p>

<p align="center">
  <strong>CAD interchange without UI</strong><br/>
  Avalonia-free CAD primitives and interchange (.cadjson / .cadphys).
</p>

<p align="center">
  <a href="https://novolis-platform.github.io/.github/novolis-cad/"><img src="https://img.shields.io/badge/docs-portfolio-0a7ea3" alt="docs"/></a>
  <a href="https://github.com/Novolis-Platform/novolis-cad/actions"><img src="https://img.shields.io/github/actions/workflow/status/Novolis-Platform/novolis-cad/merge.yml?branch=main&label=merge&logo=github" alt="merge"/></a>
  <a href="https://github.com/orgs/Novolis-Platform/packages?repo_name=novolis-cad"><img src="https://img.shields.io/badge/packages-GitHub%20Packages-0a7ea3?logo=nuget" alt="packages"/></a>
  <a href="https://github.com/Novolis-Platform"><img src="https://img.shields.io/badge/org-Novolis--Platform-111827" alt="org"/></a>
</p>

<p align="center">
  <a href="https://novolis-platform.github.io/.github/novolis-cad/">Docs</a>
  ·
  <a href="https://nuget.pkg.github.com/Novolis-Platform/index.json"><code>https://nuget.pkg.github.com/Novolis-Platform/index.json</code></a>
  ·
  <a href="https://github.com/Novolis-Platform/.github/blob/main/profile/README.md">Org landing</a>
  ·
  <a href="https://github.com/Novolis-Platform/novolis-governance">Governance</a>
</p>

---
<!-- novolis-marketing:end -->
<!-- novolis-package-index:start -->
> **GitHub Packages shows this repository README on every package page** (upstream limitation).
> Open the **package README** for install and quick start — embedded in each .nupkg and linked below.

## Published packages

| Package | Install | Package README |
|---------|---------|----------------|
| `Novolis.Cad.Primitives` | `dotnet add package Novolis.Cad.Primitives` | [README](https://github.com/Novolis-Platform/novolis-cad/blob/main/src/Novolis.Cad.Primitives/README.md) |
| `Novolis.Cad.Blueprint` | `dotnet add package Novolis.Cad.Blueprint` | [README](https://github.com/Novolis-Platform/novolis-cad/blob/main/src/Novolis.Cad.Blueprint/README.md) |
| `Novolis.Cad.SceneBridge` | `dotnet add package Novolis.Cad.SceneBridge` | [README](https://github.com/Novolis-Platform/novolis-cad/blob/main/src/Novolis.Cad.SceneBridge/README.md) |
| `Novolis.Modeling.Import` | `dotnet add package Novolis.Modeling.Import` | [README](https://github.com/Novolis-Platform/novolis-cad/blob/main/src/Novolis.Modeling.Import/README.md) |
| `Novolis.Modeling.Scene` | `dotnet add package Novolis.Modeling.Scene` | [README](https://github.com/Novolis-Platform/novolis-cad/blob/main/src/Novolis.Modeling.Scene/README.md) |

For NuGet.org and Visual Studio, the **embedded** README.md inside each package is authoritative.

<!-- novolis-package-index:end -->
# novolis-cad

Avalonia-free CAD interchange packages for Novolis.

## Packages

| Package | Role |
|---------|------|
| [`Novolis.Cad.Primitives`](src/Novolis.Cad.Primitives/README.md) | `.cadjson` / `.cadphys` DTOs, workspace enums, vec helpers |
| [`Novolis.Cad.Blueprint`](src/Novolis.Cad.Blueprint/README.md) | `CadBlueprint` companion — walls, interiors, exteriors, openings + smart sheets |
| [`Novolis.Modeling.Scene`](src/Novolis.Modeling.Scene/README.md) | Mesh-first scene graph (`.nov3djson`), evaluation, mesh editing |
| [`Novolis.Cad.SceneBridge`](src/Novolis.Cad.SceneBridge/README.md) | `CadDocument` → `SceneDocument` tessellation bridge |
| [`Novolis.Modeling.Import`](src/Novolis.Modeling.Import/README.md) | Assimp-backed mesh import (FBX, OBJ, glTF, …) → `EditableMesh` |

Schemas: [novolis-governance](https://github.com/Novolis-Platform/novolis-governance) (`schemas/cad`). UI editor: [Novolis.Avalonia.Cad](https://github.com/Novolis-Platform/novolis-avalonia/tree/main/src/Novolis.Avalonia.Cad).

## Install

```bash
dotnet add package Novolis.Cad.Primitives
dotnet add package Novolis.Cad.Blueprint
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


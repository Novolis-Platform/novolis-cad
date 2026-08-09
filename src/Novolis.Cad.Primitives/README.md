<!-- novolis-pkg-brand:start -->
<p align="center">
  <a href="https://github.com/Novolis-Platform/novolis-cad">
    <img src="https://raw.githubusercontent.com/Novolis-Platform/.github/main/brand/logo-icon.svg" width="72" alt="Novolis"/>
  </a>
</p>
<!-- novolis-pkg-brand:end -->

# Novolis.Cad.Primitives

Avalonia-free CAD interchange types for Novolis `.cadjson` / `.cadphys` documents.

- Document / entity / layer DTOs (`CadDocument`, `CadEntity`, …)
- Workspace and selection enums (`CadWorkspace`, `CadSelectionMode`, …)
- Vec / deck / bounds helpers (`CadVec`, `CadShipGeometry`)
- Opening wall-split helper (`OpeningDerivation`)
- Phys mesh / collider DTOs (`CadPhysDocument`)

Contextual companion **`CadBlueprint`** (walls / interiors / exteriors / openings + smart sheets) lives in [`Novolis.Cad.Blueprint`](../Novolis.Cad.Blueprint/README.md).

Schemas and docs live in [novolis-governance](https://github.com/Novolis-Platform/novolis-governance) (`schemas/cad`, `docs/cadjson.md`, `docs/smart-blueprint.md`). UI stays in `Novolis.Avalonia.Cad`.

## Install

```bash
dotnet add package Novolis.Cad.Primitives
```

**Prerequisites:** [.NET 10 SDK](https://dotnet.microsoft.com/download) (`net10.0`). References `Novolis.Math.Geometry` (NURBS tessellation helpers).

## Quick start

```csharp
using System.Text.Json;
using Novolis.Cad.Primitives;

var doc = new CadDocument { Name = "Demo" };
doc.Entities.Add(new CadEntity
{
    Kind = "box",
    Center = [0f, 0.5f, 0f],
    HalfExtents = [1f, 0.5f, 1f],
});

var json = JsonSerializer.Serialize(doc);
var workspace = CadWorkspaceMapping.Parse("modeling");
```

## API

| API | Purpose |
|-----|---------|
| `CadDocument` | Root `.cadjson`: `Format`, `Entities`, `Layers`, `Camera`, `UnitScaleMeters` |
| `CadEntity` | Polymorphic entity by `Kind`; geometry fields (`A`/`B`, `Center`, `Points`, `MeshVertices`/`MeshIndices`) |
| `CadLayer`, `CadCamera`, `CadGenerator`, `CadTransform` | Document metadata |
| `CadPhysDocument` | `.cadphys`: `Meshes`, `Colliders`, `BaseDocument` |
| `CadWorkspace` | Cad, Modeling, Preview |
| `CadWorkspaceMapping` | `Parse`, `ToStorage`, `ToDisplay`, `FromViewMode`, `ToViewMode` |
| `CadVec` | `Xz`, `Xyz`, `Plan`, deck helpers, `EnumerateWorldPoints`, `TranslateEntity` |
| `CadShipGeometry.TryGetBox` | Extract box center/half-extents from entity |
| `OpeningDerivation.Apply` | Split walls at opening footprints |

## Related / dogfood

| Package / app | Notes |
|---------------|-------|
| [`Novolis.Cad.SceneBridge`](../Novolis.Cad.SceneBridge/README.md) | Tessellate `CadDocument` → `.nov3djson` |
| [`Novolis.Avalonia.Cad`](../../novolis-avalonia/src/Novolis.Avalonia.Cad/README.md) | CAD editor UI |
| [novolis-governance](https://github.com/Novolis-Platform/novolis-governance) | `schemas/cad`, `docs/cadjson.md` |


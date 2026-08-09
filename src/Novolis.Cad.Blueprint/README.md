<!-- novolis-pkg-brand:start -->
<p align="center">
  <a href="https://github.com/Novolis-Platform/novolis-cad">
    <img src="https://raw.githubusercontent.com/Novolis-Platform/.github/main/brand/logo-icon.svg" width="72" alt="Novolis"/>
  </a>
</p>
<!-- novolis-pkg-brand:end -->

# Novolis.Cad.Blueprint

Avalonia-free **`CadBlueprint`** — contextual companion to **`CadDocument`**.

| | CadDocument | CadBlueprint |
|---|---|---|
| Role | Full CAD SoT | Contextual / simplified projection |
| Contains | Sketch, solids, modifiers, meshes, walls, spaces, … | Shells, walls, interiors (spaces), openings |
| File | `.cadjson` | `.cadblueprint.json` |
| Format | `novolis.cad` | `novolis.cad.blueprint` |

Intended contexts (open set): spaceships, space stations, seagoing ships, houses, skyscrapers.

Openings cover **doors**, **hatches**, and **holes**. Smart HTML5 sheets (toggleable layer folders) attach as `CadBlueprint.Sheets[]` — presentation, not geometry SoT.

## Install

```bash
dotnet add package Novolis.Cad.Blueprint
```

Depends on `Novolis.Cad.Primitives`.

## Quick start

```csharp
using Novolis.Cad.Blueprint;
using Novolis.Cad.Primitives;

CadDocument cad = /* load .cadjson */;
CadBlueprint bp = CadBlueprintProjector.FromCadDocument(cad, context: CadBlueprintContexts.House);
// bp.Walls / Spaces / Openings / Shells / Sheets
```

## API

| API | Purpose |
|-----|---------|
| `CadBlueprint` | Root companion document |
| `CadBlueprintWall` / `Space` / `Opening` / `Shell` / `Level` | Contextual elements |
| `CadBlueprintSheet` | Smart HTML sheet + layers/folders/presets |
| `CadBlueprintProjector.FromCadDocument` | Lift walls/spaces/openings from CAD |
| `CadBlueprintDom` | HTML5 `data-nbp-*` contract constants |
| `CadBlueprintContexts` | Well-known context strings |

Schema / protocol: [novolis-governance](https://github.com/Novolis-Platform/novolis-governance) `docs/smart-blueprint.md`, `schemas/cad/novolis.cad.blueprint.schema.json`.

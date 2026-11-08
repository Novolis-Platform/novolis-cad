<!-- novolis-pkg-brand:start -->
[![Novolis](https://raw.githubusercontent.com/Novolis-Platform/.github/main/brand/logo-icon.png)](https://novolis-platform.github.io/.github/novolis-cad/)

[Novolis](https://github.com/Novolis-Platform) · [Docs](https://novolis-platform.github.io/.github/novolis-cad/) · [Source](https://github.com/Novolis-Platform/novolis-cad)
<!-- novolis-pkg-brand:end -->

# Novolis.Cad.Blueprint

Avalonia-free **`CadBlueprint`** companion export package for **`CadDocument`**.

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

Depends on `Novolis.Cad.Primitives` only (no Ship / Avalonia).

## Quick start — export package

```csharp
using Novolis.Cad.Blueprint;
using Novolis.Cad.Primitives;

CadDocument cad = /* load .cadjson */;
var result = CadBlueprintExporter.Export(cad, new CadBlueprintExportOptions
{
    OutputDirectory = "./out",
    FileStem = "demo",
    ContextOverride = CadBlueprintContexts.House,
    CadDocumentHref = "./demo.cadjson",
});
if (!result.Ok)
    throw new InvalidOperationException(result.FormatDiagnostics());
// writes demo.cadblueprint.json + smart/*.html
```

Lower-level APIs:

```csharp
CadBlueprint bp = CadBlueprintProjector.FromCadDocument(cad, CadBlueprintContexts.House);
var diags = CadBlueprintValidator.Validate(bp);
CadBlueprintJson.Save(bp, "demo.cadblueprint.json");
string html = CadBlueprintHtmlExporter.EmitSheet(bp, bp.Sheets[0]);
```

## API

| API | Purpose |
|-----|---------|
| `CadBlueprint` | Root companion document |
| `CadBlueprintWall` / `Space` / `Opening` / `Shell` / `Level` | Contextual elements |
| `CadBlueprintSheet` | Smart HTML sheet + layers/folders/presets |
| `CadBlueprintProjector` | Lift walls/spaces/openings from CAD (+ lift diagnostics) |
| `CadBlueprintJson` | Load / Save `.cadblueprint.json` |
| `CadBlueprintValidator` | Schema checks → `CadBlueprintDiagnostic` |
| `CadBlueprintExporter` | Project → validate → write JSON + smart HTML |
| `CadBlueprintHtmlExporter` | `data-nbp-*` smart sheet HTML writer |
| `CadBlueprintDom` | HTML5 `data-nbp-*` contract constants |
| `CadBlueprintContexts` | Well-known context strings |

Schema / protocol: [novolis-governance](https://github.com/Novolis-Platform/novolis-governance) `docs/smart-blueprint.md`, `schemas/cad/novolis.cad.blueprint.schema.json`.

Orthographic / mesh-section detail drawings stay in **`Novolis.Cad.Drawing`** — hosts compose ISO chrome separately.

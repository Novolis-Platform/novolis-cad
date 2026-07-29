# Novolis.Cad.Primitives

Avalonia-free CAD interchange types for Novolis `.cadjson` / `.cadphys` documents.

- Document / entity / layer DTOs (`CadDocument`, `CadEntity`, …)
- Workspace and selection enums (`CadWorkspace`, `CadSelectionMode`, …)
- Vec / deck / bounds helpers (`CadVec`, `CadShipGeometry`)
- Phys mesh / collider DTOs (`CadPhysDocument`)

Schemas and docs live in [novolis-governance](https://github.com/Novolis-Platform/novolis-governance) (`schemas/cad`, `docs/cadjson.md`). UI stays in `Novolis.Avalonia.Cad`.

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

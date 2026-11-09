# Novolis.Cad.Dxf

DXF → `CadDocument` importer powered by [ACadSharp](https://github.com/DomCR/ACadSharp).

Layer-name heuristics lift architectural floor plans into walls, spaces, and openings
for `Novolis.Cad.Blueprint` sheet export (HTML / SVG / PDF).

```csharp
var result = DxfCadImporter.ImportFile(@"plan.dxf");
var bp = CadBlueprintProjector.FromCadDocument(result.Document, CadBlueprintContexts.House);
```

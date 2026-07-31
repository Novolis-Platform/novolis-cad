# Novolis.Modeling.Scene

Mesh-first scene graph for CAD 3D editing and rendering pipelines. Typed nodes, staged evaluation (generators/modifiers → triangles), runtime mesh-edit state, and `.nov3djson` serialization. No Avalonia UI and no LLM transports.

## Install

```bash
dotnet add package Novolis.Modeling.Scene
```

**Prerequisites:** [.NET 10 SDK](https://dotnet.microsoft.com/download) (`net10.0`). References `Novolis.Math.Geometry`.

## Quick start

```csharp
using Novolis.Modeling.Scene;

var doc = SceneDocument.CreatePrimitiveStage("Demo");

SceneSerializer.Save(doc, @"out.nov3djson");
var loaded = SceneSerializer.Load(@"out.nov3djson");

var evaluator = new SceneEvaluator();
evaluator.Bind(loaded);
IReadOnlyList<EvaluatedMesh> meshes = evaluator.Cache.EvaluatedMeshes;

var box = loaded.Nodes.OfType<MeshNode>().First();
EditableMesh editable = PrimitiveMesher.Tessellate(box);
MeshEditBake.MakeEditable(loaded, evaluator, box.Id);
```

## Scene graph

| Node type | JSON `kind` | Role |
|-----------|-------------|------|
| `GroupNode` | `group` | Scene root / grouping |
| `MeshNode` | `mesh` | Primitives or baked `Vertices`/`Indices` |
| `GeneratorNode` | `generator` | Cloner, Symmetry, Boole |
| `ModifierNode` | `modifier` | Weld, Subdivision, Optimize, Bridge, Extrude, Bevel, … |
| `MaterialNode` | `material` | Color / roughness / metallic |
| `LightNode` | `light` | Omni, Spot, Infinite, Area |
| `CameraNode` | `camera` | FOV, near/far, target |
| `NullNode` | `null` | Transform anchor |

## API

| API | Purpose |
|-----|---------|
| `SceneDocument` | Root document: `Format` = `"novolis.scene"`, `Nodes`, `ActiveCameraId`, `SelectionId` |
| `SceneDocument.CreatePrimitiveStage` / `CreateLookSetup` / `CreateEditBox` | Factory scenes |
| `SceneDocument.Find` / `ChildrenOf` / `Roots` / `TryRemove` | Graph navigation |
| `SceneSerializer.Save` / `Load` / `Serialize` / `Deserialize` | `.nov3djson` I/O |
| `SceneEvaluator` | `Bind`, `Cache`, `InvalidateMesh/Look/All`, `NotifyNodeChanged` |
| `LookCache` | `Lights`, `Cameras`, `Meshes`, `EvaluatedMeshes`, `Materials` |
| `EvaluatedMesh` | `SourceId`, `Vertices`, `Indices`, `World`, `ToEditableMesh()` |
| `PrimitiveMesher.Tessellate(MeshNode)` | Tessellate procedural mesh nodes |
| `MeshEditBake.MakeEditable` / `WriteBaked` | Bake procedural → editable vertex soup |
| `MeshEditState` | Edit mode, display mode, selection sets |
| `MeshPicker.Pick` / `ScreenRay` | Ray pick in object/point/edge/polygon modes |
| `MeshComponentOps` | `ExtrudeFaces`, `BevelEdges`, `BridgeSelectedEdges`, … |
| `RenderingLightExport.Export(LookCache)` | Export lights for render hosts |

## Related / dogfood

| Package / app | Notes |
|---------------|-------|
| [`Novolis.Cad.SceneBridge`](../Novolis.Cad.SceneBridge/README.md) | Produces `SceneDocument` from `.cadjson` |
| [`Novolis.Modeling.Import`](../Novolis.Modeling.Import/README.md) | Assimp import into editable meshes |
| [`Novolis.Avalonia.3D`](../../novolis-avalonia/src/Novolis.Avalonia.3D/README.md) | Scene editor UI |
| [SceneLab](../../novolis-dogfooding/apps/avalonia/SceneLab) | Interactive `.nov3djson` host |

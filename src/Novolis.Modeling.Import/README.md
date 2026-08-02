# Novolis.Modeling.Import

Assimp-backed mesh import for CAD / SceneLab dogfood.

## Formats

Anything Assimp can read with the default post-process flags — notably **FBX**, OBJ, glTF/GLB, DAE, 3DS, BLEND (when Assimp was built with Blender support).

Native Assimp binaries ship with **AssimpNet**; restore must resolve the platform RID.

## API

```csharp
using Novolis.Modeling.Import;

// Raw merge of all scene meshes (Frank.GameEngine.Assets SceneMeshImporter shape)
var mesh = AssimpMeshImporter.ImportFile(@"ship.fbx");

// With normalize / center / longest-axis → +Z
var editable = AssimpMeshImporter.ImportEditable(
    @"ship.fbx",
    new MeshImportOptions
    {
        TargetLengthMeters = 34.37f,
        CenterAtOrigin = true,
        LongestAxisToPositiveZ = true,
        PreTransformVertices = true,
    });
```

Stream overload: `ImportFromStream(stream, ".fbx", options?)`.

## API

| API | Purpose |
|-----|---------|
| `AssimpMeshImporter.CommonExtensions` | `.fbx`, `.obj`, `.gltf`, `.glb`, `.dae`, `.3ds`, `.blend`, `.stl`, `.ply` |
| `AssimpMeshImporter.ImportFile(path, options?)` | Merge all scene meshes → `TriangleMesh` |
| `AssimpMeshImporter.ImportFromStream(stream, formatHintExtension, options?)` | Stream import → `TriangleMesh` |
| `AssimpMeshImporter.ImportEditable(path, options?)` | Import → `EditableMesh` |
| `AssimpMeshImporter.ImportEditableFromStream(stream, ext, options?)` | Stream import → `EditableMesh` |
| `AssimpMeshImporter.IsSupportedExtension(pathOrExtension)` | Extension check |
| `AssimpSkinnedMeshImporter.TryImport(path, out result, options?)` | FBX/skin weights → `AssimpNamedSkinImport` (no pre-transform) |
| `MeshImportOptions` | `TargetLengthMeters`, `CenterAtOrigin`, `LongestAxisToPositiveZ`, `PreTransformVertices`, `GenerateNormals`, `OptimizeMeshes` |

## Related / dogfood

| Package / app | Notes |
|---------------|-------|
| [`Novolis.Modeling.Scene`](../Novolis.Modeling.Scene/README.md) | Scene graph for imported meshes |
| [`Novolis.Avalonia.3D`](../../novolis-avalonia/src/Novolis.Avalonia.3D/README.md) | `importmesh` + SceneLab Import |
| [CorellianFreighterBuilder](../../novolis-dogfooding/apps/avalonia/SceneLab/tools/CorellianFreighterBuilder) | CLI `--import` |

## Policy

PackageReference only (nuget.org AssimpNet + GPR `Novolis.Math.Geometry`). No local feeds.

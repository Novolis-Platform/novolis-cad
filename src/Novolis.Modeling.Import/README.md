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

## Consumers

- `Novolis.Avalonia.3D` session action `importmesh` + SceneLab **Import…**
- CLI tools (e.g. CorellianFreighterBuilder `--import`)

## Policy

PackageReference only (nuget.org AssimpNet + GPR `Novolis.Math.Geometry`). No local feeds.

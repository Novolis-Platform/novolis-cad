# Design

`Novolis.Cad.Primitives` is the interchange layer for CAD documents:

- No Avalonia, Raylib, or UI chrome
- String `Kind` entity bag (compatible with governance `novolis.cad.schema.json`)
- Light dependency on `Novolis.Math.Geometry` for NURBS tessellation in `CadVec`

`Novolis.Cad.Evaluation` owns staged `CadDocument` evaluation and phys export (Avalonia-free).

`Novolis.Cad.SceneBridge` projects `.cadjson` into `Novolis.ThreeD.Scene` (`SceneDocument` / `.nov3djson`). Mesh scene authoring lives in **novolis-avalonia** (`Novolis.ThreeD.Scene`, `Novolis.ThreeD.Import.Assimp`), not in this repo.

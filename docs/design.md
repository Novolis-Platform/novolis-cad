# Design

`Novolis.Cad.Primitives` is the interchange layer for CAD documents:

- No Avalonia, Raylib, or UI chrome
- String `Kind` entity bag (compatible with governance `novolis.cad.schema.json`)
- Light dependency on `Novolis.Math.Geometry` for NURBS tessellation in `CadVec`

Hard-surface evaluation (`CadModelEvaluator`) remains in Avalonia.Cad until a future `Novolis.Cad.Evaluation` extract. Mesh scene authoring (`.nov3djson`) stays in `Novolis.Modeling.Scene`.

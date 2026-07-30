# Novolis.Modeling.Scene

Mesh-first scene graph for CAD 3D editing / rendering pipelines.

- Typed nodes: Group, Mesh, Generator, Modifier, Material, Light, Camera, Null
- Primitives tessellated to `EditableMesh` (`PrimitiveMesher`)
- Generators: Array, Symmetry, Boolean via `Novolis.Math.Geometry.MeshBoolean`
- Modifiers: Weld, Optimize, Subdivision, Extrude, Bevel, Bridge
- `.nov3djson` load/save (`format: novolis.scene`)

No Avalonia UI and no LLM transports — see `Novolis.Avalonia.3D` and `Novolis.Agent.Surface`.

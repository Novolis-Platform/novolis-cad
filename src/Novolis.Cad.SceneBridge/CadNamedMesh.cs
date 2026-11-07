using Novolis.Cad.Primitives;
using Novolis.Math.Geometry;

namespace Novolis.Cad.SceneBridge;

/// <summary>Named triangle mesh collected from a CAD entity.</summary>
public readonly record struct CadNamedMesh(string Name, EditableMesh Mesh, CadEntity Entity);

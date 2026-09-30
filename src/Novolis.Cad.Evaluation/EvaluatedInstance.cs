using System.Numerics;
using Novolis.Cad.Primitives;
using Novolis.Math.Geometry;
using Novolis.Cad.SceneBridge.Tessellation;

namespace Novolis.Cad.Evaluation;

public sealed record EvaluatedInstance(Guid SourceId, Matrix4x4 Transform, EditableMesh? Mesh);

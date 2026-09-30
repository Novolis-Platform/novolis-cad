using System.Numerics;
using Novolis.Cad.Primitives;
using Novolis.Math.Geometry;
using Novolis.Cad.SceneBridge.Tessellation;

namespace Novolis.Cad.Evaluation;

public enum CadEvalStage
{
    Cad,
    Mesh,
    Modeling,
    Scene,
    Preview,
}

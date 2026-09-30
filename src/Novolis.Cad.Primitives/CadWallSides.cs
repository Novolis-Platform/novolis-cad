using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Novolis.Cad.Primitives;

public sealed class CadWallSides
{
    public CadWallSide? A { get; set; }

    public CadWallSide? B { get; set; }
}

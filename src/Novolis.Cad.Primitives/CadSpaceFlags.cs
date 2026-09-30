using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Novolis.Cad.Primitives;

public sealed class CadSpaceFlags
{
    public bool Enclosed { get; set; }

    public bool Hollow { get; set; }
}

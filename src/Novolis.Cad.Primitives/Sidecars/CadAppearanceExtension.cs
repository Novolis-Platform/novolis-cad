using System.Text.Json;
using System.Text.Json.Serialization;

namespace Novolis.Cad.Primitives;

public sealed class CadAppearanceExtension
{
    public CadFill? Fill { get; set; }
    public CadStroke? Stroke { get; set; }
    public int? ColorIndex { get; set; }
}

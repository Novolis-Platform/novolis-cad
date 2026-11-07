using System.Text.Json;
using System.Text.Json.Serialization;

namespace Novolis.Cad.Primitives;

public sealed class CadShapeExtensions
{
    public CadAppearanceExtension? Appearance { get; set; }
    public CadMaterialExtension? Material { get; set; }
}

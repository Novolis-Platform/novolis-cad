using System.Text.Json;
using System.Text.Json.Serialization;
using Novolis.Cad.Primitives;

namespace Novolis.Cad.Blueprint;

/// <summary>Well-known <see cref="CadBlueprint.Context"/> values — open set; custom strings allowed.</summary>
public static class CadBlueprintContexts
{
    public const string Generic = "generic";
    public const string Spaceship = "spaceship";
    public const string SpaceStation = "space-station";
    public const string SeagoingShip = "seagoing-ship";
    public const string House = "house";
    public const string Skyscraper = "skyscraper";
}

namespace Novolis.Cad.SceneBridge;

/// <summary>Options for <see cref="CadSceneBridge"/>.</summary>
public sealed class CadSceneBridgeOptions
{
    /// <summary>When true and the scene has no lights after conversion, add Key/Fill/Rim.</summary>
    public bool EnsureStudioLights { get; set; }

    /// <summary>Optional path to a <c>.cadshapejson</c> catalog for side shape colors.</summary>
    public string? ShapeCatalogPath { get; set; }

    /// <summary>Include ceiling plates when tessellating spaces.</summary>
    public bool IncludeSpaceCeilings { get; set; }
}

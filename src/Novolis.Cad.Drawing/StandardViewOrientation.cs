namespace Novolis.Cad.Drawing;

/// <summary>
/// Named orthographic orientations. Callers supply the document coordinate frame;
/// these names describe view intent relative to a longitudinal axis (+Z = "bow-ward").
/// </summary>
public enum StandardViewOrientation
{
    /// <summary>Looking down (−Y). U = −Z, V = −X.</summary>
    PlanSternLeft,

    /// <summary>Looking along +X. U = −Z, V = Y.</summary>
    ProfileSternLeft,

    /// <summary>Looking along −Z. U = −X, V = Y.</summary>
    TransverseLookingAft,

    /// <summary>Looking along +Z. U = +X, V = Y.</summary>
    TransverseLookingForward,
}

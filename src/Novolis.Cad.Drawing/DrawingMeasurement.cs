namespace Novolis.Cad.Drawing;

public sealed record DrawingMeasurement(
    string Id,
    string Label,
    float ValueMeters,
    bool IsTrue3D,
    string? SourceNote = null);

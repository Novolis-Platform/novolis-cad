namespace Novolis.Cad.Drawing;

public sealed record PartRegisterLine(
    string PartId,
    string Description,
    string OwningAssembly,
    string? Material,
    int Quantity,
    string? ExtentNote,
    string? FabDrawingRef,
    float? MassKg);

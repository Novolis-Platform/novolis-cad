namespace Novolis.Cad.Drawing;

public sealed record DrawingReferenceSet(
    DrawingReferenceFlags Flags = DrawingReferenceFlags.DefaultContext,
    IReadOnlyList<float>? ExtraStationsZStem = null);

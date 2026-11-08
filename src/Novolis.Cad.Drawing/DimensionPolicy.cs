namespace Novolis.Cad.Drawing;

public sealed record DimensionPolicy(
    DimensionPolicyKind Kind = DimensionPolicyKind.OverallExtents,
    IReadOnlyList<string>? ExplicitIntentIds = null);

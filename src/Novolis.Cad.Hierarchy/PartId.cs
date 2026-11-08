namespace Novolis.Cad.Hierarchy;

/// <summary>Stable physical part identity.</summary>
public readonly record struct PartId(string Value)
{
    public static PartId Of(string value) =>
        new(string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("Part id required.", nameof(value))
            : value.Trim());

    public override string ToString() => Value;
}

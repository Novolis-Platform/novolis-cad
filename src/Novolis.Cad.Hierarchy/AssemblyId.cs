namespace Novolis.Cad.Hierarchy;

/// <summary>Stable assembly identity (string value, case-insensitive equality).</summary>
public readonly record struct AssemblyId(string Value)
{
    public static AssemblyId Of(string value) =>
        new(string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("Assembly id required.", nameof(value))
            : value.Trim());

    public override string ToString() => Value;
}

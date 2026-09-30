namespace Novolis.Cad.Primitives;

public sealed class CadColliderBody
{
    public float Mass { get; set; } = 1f;

    public float[] InertiaDiagonal { get; set; } = [1f, 1f, 1f];

    public bool Kinematic { get; set; }
}

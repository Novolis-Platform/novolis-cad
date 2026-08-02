using Novolis.Cad.Primitives;

namespace Novolis.Cad.Unit;

public sealed class OpeningDerivationTests
{
    [Test]
    public async Task Apply_SplitsWallAroundOpeningFootprint()
    {
        var wallId = Guid.NewGuid();
        var entities = new List<CadEntity>
        {
            new()
            {
                Id = wallId,
                Kind = "wall",
                Name = "Corridor",
                Deck = 0,
                Thickness = 0.2f,
                Height = 2.4f,
                A = [0, 0, 0],
                B = [10, 0, 0],
            },
            new()
            {
                Kind = "opening",
                Deck = 0,
                Footprint =
                [
                    [4.5f, 0, -0.5f],
                    [5.5f, 0, -0.5f],
                    [5.5f, 0, 0.5f],
                    [4.5f, 0, 0.5f],
                ],
            },
        };

        OpeningDerivation.Apply(entities);

        await Assert.That(entities.Any(e => e.Id == wallId)).IsFalse();
        var walls = entities.Where(e => e.Kind == "wall").ToList();
        await Assert.That(walls.Count).IsEqualTo(2);
        await Assert.That(walls.All(w => w.HostWallId is null)).IsTrue();
        await Assert.That(entities.Single(e => e.Kind == "opening").HostWallId).IsNotNull();
    }

    [Test]
    public async Task Apply_NoOpenings_IsNoOp()
    {
        var entities = new List<CadEntity>
        {
            new() { Kind = "wall", A = [0, 0, 0], B = [5, 0, 0] },
        };
        var before = entities.Count;
        OpeningDerivation.Apply(entities);
        await Assert.That(entities.Count).IsEqualTo(before);
    }

    [Test]
    public async Task Apply_ShortWall_IsNotSplit()
    {
        var entities = new List<CadEntity>
        {
            new() { Kind = "wall", A = [0, 0, 0], B = [0.2f, 0, 0] },
            new()
            {
                Kind = "opening",
                Footprint = [[0, 0, 0], [0.1f, 0, 0], [0.1f, 0, 0.1f]],
            },
        };
        OpeningDerivation.Apply(entities);
        await Assert.That(entities.Count(e => e.Kind == "wall")).IsEqualTo(1);
    }
}

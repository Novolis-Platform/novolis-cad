using System.Numerics;
using Novolis.Cad.Primitives;

namespace Novolis.Cad.Unit;

public sealed class CadVecAndEntityTests
{
    [Test]
    public async Task CadVec_ConversionsAndTranslate()
    {
        await Assert.That(CadVec.Xz(1, 2)).IsEquivalentTo(new float[] { 1, 0, 2 });
        await Assert.That(CadVec.Xyz(1, 2, 3)).IsEquivalentTo(new float[] { 1, 2, 3 });
        await Assert.That(CadVec.Plan(1, 2, 3)).IsEquivalentTo(new float[] { 1, 3, 2 });
        await Assert.That(CadVec.From(new Vector3(1, 2, 3))).IsEquivalentTo(new float[] { 1, 2, 3 });
        await Assert.That(CadVec.To([1, 2, 3])).IsEqualTo(new Vector3(1, 2, 3));
        await Assert.That(CadVec.To(null, new Vector3(9, 9, 9))).IsEqualTo(new Vector3(9, 9, 9));
        await Assert.That(CadVec.OnPlane(1, 2, 3)).IsEqualTo(new Vector3(1, 3, 2));

        var a = CadVec.Xyz(0, 0, 0);
        CadVec.Translate(a, 1, 2, 3);
        await Assert.That(a).IsEquivalentTo(new float[] { 1, 2, 3 });

        var pts = new List<float[]> { CadVec.Xyz(0, 0, 0), CadVec.Xyz(1, 1, 1) };
        CadVec.TranslateAll(pts, 0, 1, 0);
        await Assert.That(pts[0][1]).IsEqualTo(1);
        await Assert.That(pts[1][1]).IsEqualTo(2);
    }

    [Test]
    public async Task CadVec_LooksLikeShipDocument_UsesWallSpaceThreshold()
    {
        var empty = new CadDocument();
        await Assert.That(CadVec.LooksLikeShipDocument(empty)).IsFalse();

        var ship = new CadDocument();
        for (var i = 0; i < 8; i++)
            ship.Entities.Add(new CadEntity { Kind = i % 2 == 0 ? "wall" : "space" });
        await Assert.That(CadVec.LooksLikeShipDocument(ship)).IsTrue();

        var few = new CadDocument();
        few.Entities.Add(new CadEntity { Kind = "wall" });
        few.Entities.Add(new CadEntity { Kind = "box" });
        await Assert.That(CadVec.LooksLikeShipDocument(few)).IsFalse();
    }

    [Test]
    public async Task CadVec_ElevationOf_CoversEntityKinds()
    {
        await Assert.That(CadVec.ElevationOf(new CadEntity { Kind = "wall", Deck = 1 })).IsEqualTo(CadVec.DeckHeightMeters);
        await Assert.That(CadVec.ElevationOf(new CadEntity { Kind = "line", A = [0, 2.5f, 0] })).IsEqualTo(2.5f);
        await Assert.That(CadVec.ElevationOf(new CadEntity { Kind = "circle", Center = [0, 1.2f, 0], Radius = 1 })).IsEqualTo(1.2f);
        await Assert.That(CadVec.ElevationOf(new CadEntity
        {
            Kind = "spline",
            FitPoints = [CadVec.Xyz(0, 4f, 0)],
        })).IsEqualTo(4f);

        var shipBox = new CadEntity
        {
            Kind = "box",
            Deck = 2,
            Points = [CadVec.Xyz(0, 0, 0), CadVec.Xyz(1, 1, 1)],
        };
        await Assert.That(CadVec.ElevationOf(shipBox)).IsEqualTo(2 * CadVec.DeckHeightMeters);

        var analyticBox = new CadEntity
        {
            Kind = "box",
            Center = [0, 0.75f, 0],
            HalfExtents = [1, 0.5f, 1],
        };
        await Assert.That(CadVec.ElevationOf(analyticBox)).IsEqualTo(0.75f);
    }

    [Test]
    public async Task CadVec_MatchesLevel_DeckedAndGeometric()
    {
        var wall = new CadEntity { Kind = "wall", Deck = 1 };
        await Assert.That(CadVec.MatchesLevel(wall, CadVec.DeckHeightMeters, 0.01f)).IsTrue();
        await Assert.That(CadVec.MatchesLevel(wall, 0, 0.01f)).IsFalse();

        var line = new CadEntity { Kind = "line", A = [0, 1f, 0], B = [1, 1f, 0] };
        await Assert.That(CadVec.MatchesLevel(line, 1f, 0.05f)).IsTrue();
        await Assert.That(CadVec.MatchesLevel(line, 2f, 0.05f)).IsFalse();

        var opening = new CadEntity { Kind = "opening", Deck = 1 };
        await Assert.That(CadVec.MatchesLevel(opening, CadVec.DeckHeightMeters, 0.01f)).IsTrue();

        var space = new CadEntity { Kind = "space", Deck = 0 };
        await Assert.That(CadVec.MatchesLevel(space, 0f, 0.01f)).IsTrue();

        var shipBox = new CadEntity
        {
            Kind = "box",
            Deck = 1,
            Points = [CadVec.Xyz(0, 0, 0), CadVec.Xyz(1, 1, 1)],
        };
        await Assert.That(CadVec.MatchesLevel(shipBox, CadVec.DeckHeightMeters, 0.01f)).IsTrue();
    }

    [Test]
    public async Task CadVec_TranslateAll_null_is_noop()
    {
        List<float[]>? list = null;
        CadVec.TranslateAll(list, 1, 2, 3);
        await Assert.That(list).IsNull();
    }

    [Test]
    public async Task CadVec_ElevationOf_remaining_kinds()
    {
        await Assert.That(CadVec.ElevationOf(new CadEntity { Kind = "rect", A = [0, 3f, 0] })).IsEqualTo(3f);
        await Assert.That(CadVec.ElevationOf(new CadEntity { Kind = "opening", Deck = 1 })).IsEqualTo(CadVec.DeckHeightMeters);
        await Assert.That(CadVec.ElevationOf(new CadEntity { Kind = "space", Deck = 2 })).IsEqualTo(2 * CadVec.DeckHeightMeters);
        await Assert.That(CadVec.ElevationOf(new CadEntity
        {
            Kind = "spline",
            ControlPoints = [CadVec.Xyz(0, 5f, 0)],
        })).IsEqualTo(5f);
        await Assert.That(CadVec.ElevationOf(new CadEntity { Kind = "wedge", Center = [0, 1.1f, 0] })).IsEqualTo(1.1f);
        await Assert.That(CadVec.ElevationOf(new CadEntity { Kind = "cylinder", Center = [0, 2.2f, 0] })).IsEqualTo(2.2f);
        await Assert.That(CadVec.ElevationOf(new CadEntity { Kind = "cone", Center = [0, 3.3f, 0] })).IsEqualTo(3.3f);
        await Assert.That(CadVec.ElevationOf(new CadEntity { Kind = "sphere", Center = [0, 4.4f, 0] })).IsEqualTo(4.4f);
        await Assert.That(CadVec.ElevationOf(new CadEntity { Kind = "unknown" })).IsEqualTo(0f);
    }

    [Test]
    public async Task CadVec_EnsureDefaultLayerAndWithLayer()
    {
        var doc = new CadDocument();
        var layerId = CadVec.EnsureDefaultLayer(doc);
        await Assert.That(doc.Layers.Count).IsEqualTo(1);
        await Assert.That(doc.Layers[0].Id).IsEqualTo(layerId);

        var entity = new CadEntity { Kind = "line" }.WithLayer(doc);
        await Assert.That(entity.LayerId).IsEqualTo(layerId);
    }

    [Test]
    public async Task CadVec_EnumerateWorldPoints_CoversMajorKinds()
    {
        var linePts = CadVec.EnumerateWorldPoints(new CadEntity
        {
            Kind = "line",
            A = [0, 0, 0],
            B = [2, 0, 0],
        }).ToList();
        await Assert.That(linePts.Count).IsEqualTo(2);

        var wallPts = CadVec.EnumerateWorldPoints(new CadEntity
        {
            Kind = "wall",
            A = [0, 0, 0],
            B = [3, 0, 0],
            Height = 2.4f,
        }).ToList();
        await Assert.That(wallPts.Count).IsEqualTo(2);
        await Assert.That(wallPts[1].Y).IsEqualTo(2.4f);

        var spacePts = CadVec.EnumerateWorldPoints(new CadEntity
        {
            Kind = "space",
            Height = 2f,
            Points = [CadVec.Xz(0, 0), CadVec.Xz(2, 0), CadVec.Xz(2, 2)],
        }).ToList();
        await Assert.That(spacePts.Count).IsEqualTo(6);

        var openingPts = CadVec.EnumerateWorldPoints(new CadEntity
        {
            Kind = "opening",
            Footprint = [CadVec.Xz(0, 0), CadVec.Xz(1, 0), CadVec.Xz(1, 1)],
        }).ToList();
        await Assert.That(openingPts.Count).IsEqualTo(3);
    }

    [Test]
    public async Task CadVec_EnumerateWorldPoints_remaining_kinds()
    {
        var circlePts = CadVec.EnumerateWorldPoints(new CadEntity
        {
            Kind = "circle",
            Center = [0, 0, 0],
            Radius = 2f,
        }).ToList();
        await Assert.That(circlePts.Count).IsEqualTo(4);

        var spherePts = CadVec.EnumerateWorldPoints(new CadEntity
        {
            Kind = "sphere",
            Center = [0, 0, 0],
            Radius = 1f,
        }).ToList();
        await Assert.That(spherePts.Count).IsEqualTo(6);

        var rectPts = CadVec.EnumerateWorldPoints(new CadEntity
        {
            Kind = "rect",
            Center = [0, 0, 0],
            HalfExtents = [1, 0, 1],
        }).ToList();
        await Assert.That(rectPts.Count).IsEqualTo(2);

        var splinePts = CadVec.EnumerateWorldPoints(new CadEntity
        {
            Kind = "spline",
            ControlPoints = [CadVec.Xyz(0, 0, 0), CadVec.Xyz(1, 0, 0), CadVec.Xyz(2, 0, 0)],
            Knots = [0, 0, 0, 1, 1, 1],
            Degree = 2,
        }).ToList();
        await Assert.That(splinePts.Count).IsGreaterThan(0);

        var fitPts = CadVec.EnumerateWorldPoints(new CadEntity
        {
            Kind = "spline",
            FitPoints = [CadVec.Xyz(0, 0, 0), CadVec.Xyz(1, 0, 1)],
        }).ToList();
        await Assert.That(fitPts.Count).IsEqualTo(2);

        var boxPts = CadVec.EnumerateWorldPoints(new CadEntity
        {
            Kind = "box",
            Center = [0, 0.5f, 0],
            HalfExtents = [1, 0.5f, 1],
        }).ToList();
        await Assert.That(boxPts.Count).IsEqualTo(2);

        var wallPts = CadVec.EnumerateWorldPoints(new CadEntity
        {
            Kind = "wall",
            Points = [CadVec.Xyz(0, 0, 0), CadVec.Xyz(2, 0, 0)],
        }).ToList();
        await Assert.That(wallPts.Count).IsEqualTo(2);

        var openingAb = CadVec.EnumerateWorldPoints(new CadEntity
        {
            Kind = "opening",
            A = [0, 0, 0],
            B = [1, 0, 0],
        }).ToList();
        await Assert.That(openingAb.Count).IsEqualTo(2);

        var cylinderPts = CadVec.EnumerateWorldPoints(new CadEntity
        {
            Kind = "cylinder",
            Center = [0, 0, 0],
            Radius = 1f,
            Height = 2f,
        }).ToList();
        await Assert.That(cylinderPts.Count).IsEqualTo(2);

        var polyPts = CadVec.EnumerateWorldPoints(new CadEntity
        {
            Kind = "polyline",
            Points = [CadVec.Xyz(0, 0, 0), CadVec.Xyz(1, 0, 0)],
        }).ToList();
        await Assert.That(polyPts.Count).IsEqualTo(2);
    }

    [Test]
    public async Task CadVec_TranslateEntity_MovesAllPointFields()
    {
        var entity = new CadEntity
        {
            Kind = "polyline",
            A = [0, 0, 0],
            B = [1, 0, 0],
            Center = [0, 0, 0],
            Points = [CadVec.Xyz(0, 0, 0)],
            ControlPoints = [CadVec.Xyz(0, 0, 0)],
            FitPoints = [CadVec.Xyz(0, 0, 0)],
        };
        CadVec.TranslateEntity(entity, 1, 2, 3);
        await Assert.That(entity.A![0]).IsEqualTo(1);
        await Assert.That(entity.Points![0][1]).IsEqualTo(2);
        await Assert.That(entity.ControlPoints![0][2]).IsEqualTo(3);
    }

    [Test]
    public async Task CadEntity_SummaryAndIsSolid()
    {
        var wall = new CadEntity { Kind = "wall", Name = "Bulkhead", Thickness = 0.2f, Height = 2.4f, Deck = 0 };
        await Assert.That(wall.Summary).Contains("Bulkhead");
        await Assert.That(wall.Summary).Contains("wall");

        var box = new CadEntity
        {
            Kind = "box",
            Center = [0, 0.5f, 0],
            HalfExtents = [1, 0.5f, 1],
        };
        await Assert.That(box.IsSolid).IsTrue();
        await Assert.That(box.Summary).Contains("box");

        var line = new CadEntity { Kind = "line", A = [0, 0, 0], B = [1, 0, 1] };
        await Assert.That(line.IsSolid).IsFalse();
        await Assert.That(line.Summary).Contains("line");
    }

    [Test]
    public async Task CadEntity_Summary_covers_remaining_kinds()
    {
        await Assert.That(new CadEntity { Kind = "circle", Name = "C", Radius = 2f }.Summary).Contains("circle r=2");
        await Assert.That(new CadEntity { Kind = "rect", Name = "R" }.Summary).Contains("rect");
        await Assert.That(new CadEntity
        {
            Kind = "spline",
            Name = "S",
            Degree = 3,
            ControlPoints = [CadVec.Xyz(0, 0, 0)],
        }.Summary).Contains("spline deg=3");
        await Assert.That(new CadEntity { Kind = "cylinder", Radius = 1f, Height = 2f }.Summary).Contains("cylinder");
        await Assert.That(new CadEntity { Kind = "sphere", Radius = 0.5f }.Summary).Contains("sphere");
        await Assert.That(new CadEntity { Kind = "opening", OpeningType = "window" }.Summary).Contains("window");
        await Assert.That(new CadEntity { Kind = "space", Height = 2.4f, Deck = 1 }.Summary).Contains("space");
        await Assert.That(new CadEntity { Kind = "cone", Name = "K" }.Summary).Contains("cone");
    }

    [Test]
    public async Task CadShipGeometry_TryGetBox_AnalyticAndShipEncodings()
    {
        var analytic = new CadEntity
        {
            Kind = "box",
            Center = [1, 2, 3],
            HalfExtents = [0.5f, 0.5f, 0.5f],
        };
        await Assert.That(CadShipGeometry.TryGetBox(analytic, out var c, out var he)).IsTrue();
        await Assert.That(c).IsEqualTo(new Vector3(1, 2, 3));
        await Assert.That(he).IsEqualTo(new Vector3(0.5f, 0.5f, 0.5f));

        var ship = new CadEntity
        {
            Kind = "box",
            Points = [CadVec.Xyz(0, 0, 0), CadVec.Xyz(0, 0, 0)],
            Thickness = 1f,
            Height = 2f,
        };
        await Assert.That(CadShipGeometry.TryGetBox(ship, out c, out he)).IsTrue();
        await Assert.That(he.X).IsGreaterThan(0);

        var missing = new CadEntity { Kind = "box" };
        await Assert.That(CadShipGeometry.TryGetBox(missing, out _, out _)).IsFalse();
    }

    [Test]
    public async Task CadWorkspaceMapping_ParsesAllAliases()
    {
        await Assert.That(CadWorkspaceMapping.Parse("modeler")).IsEqualTo(CadWorkspace.Modeling);
        await Assert.That(CadWorkspaceMapping.Parse("render")).IsEqualTo(CadWorkspace.Preview);
        await Assert.That(CadWorkspaceMapping.Parse("sketch")).IsEqualTo(CadWorkspace.Cad);
        await Assert.That(CadWorkspaceMapping.Parse(null)).IsEqualTo(CadWorkspace.Cad);
        await Assert.That(CadWorkspaceMapping.ToStorage(CadWorkspace.Modeling)).IsEqualTo("modeling");
        await Assert.That(CadWorkspaceMapping.FromViewMode(CadViewMode.Model)).IsEqualTo(CadWorkspace.Preview);
        await Assert.That(CadWorkspaceMapping.ToViewMode(CadWorkspace.Cad)).IsEqualTo(CadViewMode.Draft);
    }
}

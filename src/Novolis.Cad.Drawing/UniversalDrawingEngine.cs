using System.Text.Json;
using Novolis.Cad.Hierarchy;
using Novolis.Cad.Primitives;

namespace Novolis.Cad.Drawing;

/// <summary>
/// Hierarchy-driven drawing engine: subject → geometry → project/section → measurements.
/// HTML/SVG composition is left to hosts (Calypso package chrome, CadStudio, …).
/// </summary>
public static class UniversalDrawingEngine
{
    public sealed record RenderResult(
        SubjectGeometryResolver.ResolveResult Resolved,
        DrawingManifest Manifest,
        ViewProjector.ProjectionResult? PrimaryProjection);

    public static RenderResult Render(
        DrawingRequest request,
        CadDocument document,
        ICadHierarchySource hierarchy)
    {
        var resolved = SubjectGeometryResolver.Resolve(request.SubjectId, hierarchy, document);
        var measurements = DrawingMeasurements.ComputeOverall(resolved.Geometry, request.Dimensions);
        var register = PartRegisterBuilder.Build(request.SubjectId, hierarchy);

        var unsupported = resolved.Diagnostics
            .Where(d => d.Contains("Unsupported", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var ok = resolved.HasDrawableGeometry
                 || request.Views.All(v => v.Kind == DrawingViewKind.Assembly);

        ViewProjector.ProjectionResult? primary = null;
        if (resolved.HasDrawableGeometry && request.Views.Count > 0)
        {
            var view = request.Views[0];
            if (view.Kind == DrawingViewKind.Section && view.SectionPlane is { } plane)
            {
                var edges = MeshSectioner.Section(resolved.Geometry, plane);
                var segs = edges.Select(e =>
                {
                    var a = ViewProjector.Map(e.A, view.Orientation);
                    var b = ViewProjector.Map(e.B, view.Orientation);
                    return new ViewProjector.ProjectedSegment(a, b, e.SourceEntityId, e.SourceName, true);
                }).ToList();
                if (segs.Count > 0)
                {
                    primary = new ViewProjector.ProjectionResult(
                        segs, [], DrawingBounds3.Empty,
                        segs.Min(s => System.Math.Min(s.A.U, s.B.U)),
                        segs.Max(s => System.Math.Max(s.A.U, s.B.U)),
                        segs.Min(s => System.Math.Min(s.A.V, s.B.V)),
                        segs.Max(s => System.Math.Max(s.A.V, s.B.V)));
                }
            }
            else
            {
                primary = ViewProjector.ProjectOrthographic(resolved.Geometry, view.Orientation);
            }
        }

        var manifest = new DrawingManifest
        {
            SubjectId = request.SubjectId,
            DrawingNumber = request.DrawingNumber,
            ViewIds = request.Views.Select(v => v.Id).ToList(),
            GeometrySourceIds = resolved.Geometry.Select(g => g.SourceEntityId).Distinct().ToList(),
            Measurements = measurements,
            Diagnostics = resolved.Diagnostics,
            PartRegister = register,
            Ok = ok && unsupported.Count == 0,
        };

        return new RenderResult(resolved, manifest, primary);
    }

    public static string RenderManifestJson(DrawingManifest manifest) =>
        JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
}

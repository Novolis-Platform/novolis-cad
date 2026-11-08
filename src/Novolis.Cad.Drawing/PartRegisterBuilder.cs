using Novolis.Cad.Hierarchy;

namespace Novolis.Cad.Drawing;

public static class PartRegisterBuilder
{
    public static IReadOnlyList<PartRegisterLine> Build(string subjectId, ICadHierarchySource hierarchy)
    {
        var parts = SubjectGeometryResolver.CollectPhysicalParts(subjectId, hierarchy);
        var lines = new List<PartRegisterLine>();
        foreach (var group in parts.GroupBy(p => p.Id.Value, StringComparer.OrdinalIgnoreCase))
        {
            var p = group.First();
            lines.Add(new PartRegisterLine(
                p.Id.Value,
                p.DisplayName,
                p.Assembly.Value,
                Material: p.Metadata?.GetValueOrDefault("material"),
                Quantity: group.Count(),
                ExtentNote: p.Metadata?.GetValueOrDefault("extent"),
                FabDrawingRef: p.Metadata?.GetValueOrDefault("fabPartId"),
                MassKg: null));
        }

        return lines;
    }
}

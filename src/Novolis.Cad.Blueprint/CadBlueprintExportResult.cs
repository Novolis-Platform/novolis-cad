namespace Novolis.Cad.Blueprint;

/// <summary>Structured outcome of validate / export — never silent-fail.</summary>
public sealed class CadBlueprintExportResult
{
    public bool Ok { get; init; }

    public CadBlueprint? Blueprint { get; init; }

    public IReadOnlyList<CadBlueprintDiagnostic> Diagnostics { get; init; } = [];

    public IReadOnlyList<string> WrittenPaths { get; init; } = [];

    public IEnumerable<CadBlueprintDiagnostic> Errors =>
        Diagnostics.Where(d => d.Severity == CadBlueprintDiagnosticSeverity.Error);

    public IEnumerable<CadBlueprintDiagnostic> Warnings =>
        Diagnostics.Where(d => d.Severity == CadBlueprintDiagnosticSeverity.Warning);

    public string FormatDiagnostics() =>
        string.Join(Environment.NewLine, Diagnostics.Select(d => $"[{d.Severity}] {d.Code}: {d.Message}"));
}

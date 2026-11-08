namespace Novolis.Cad.Blueprint;

public sealed record CadBlueprintDiagnostic(
    CadBlueprintDiagnosticSeverity Severity,
    string Code,
    string Message,
    string? Path = null);

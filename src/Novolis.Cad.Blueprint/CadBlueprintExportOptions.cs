using Novolis.Cad.Primitives;

namespace Novolis.Cad.Blueprint;

public sealed class CadBlueprintExportOptions
{
    /// <summary>Directory for <c>.cadblueprint.json</c> and smart HTML sheets.</summary>
    public required string OutputDirectory { get; init; }

    /// <summary>Stem for the companion JSON file (default: document / blueprint name sanitized).</summary>
    public string? FileStem { get; init; }

    /// <summary>Subfolder under <see cref="OutputDirectory"/> for HTML sheets (default: <c>smart</c>).</summary>
    public string SmartHtmlSubfolder { get; init; } = "smart";

    public string? ContextOverride { get; init; }

    public string? CadDocumentHref { get; init; }

    /// <summary>When true, warnings also fail the export (<see cref="CadBlueprintExportResult.Ok"/> = false).</summary>
    public bool FailOnWarning { get; init; }

    /// <summary>When true (default), refuse to write files if validation has errors.</summary>
    public bool FailOnError { get; init; } = true;

    /// <summary>When null, emit HTML for every sheet; otherwise only these sheet ids.</summary>
    public IReadOnlyList<string>? SheetIds { get; init; }

    /// <summary>Write the companion JSON (default true).</summary>
    public bool WriteJson { get; init; } = true;

    /// <summary>Write smart HTML sheets (default true).</summary>
    public bool WriteHtml { get; init; } = true;
}

namespace Novolis.Cad.Blueprint;

public sealed class CadBlueprintExportOptions
{
    /// <summary>Directory for companion JSON, HTML, SVG, and PDF sheets.</summary>
    public required string OutputDirectory { get; init; }

    /// <summary>Stem for the companion JSON file (default: document / blueprint name sanitized).</summary>
    public string? FileStem { get; init; }

    /// <summary>Subfolder under <see cref="OutputDirectory"/> for HTML sheets (default: <c>smart</c>).</summary>
    public string SmartHtmlSubfolder { get; init; } = "smart";

    /// <summary>Subfolder for standalone SVG sheets (default: <c>svg</c>).</summary>
    public string SvgSubfolder { get; init; } = "svg";

    /// <summary>Subfolder for PDF sheets (default: <c>pdf</c>).</summary>
    public string PdfSubfolder { get; init; } = "pdf";

    public string? ContextOverride { get; init; }

    public string? CadDocumentHref { get; init; }

    /// <summary>When true, warnings also fail the export (<see cref="CadBlueprintExportResult.Ok"/> = false).</summary>
    public bool FailOnWarning { get; init; }

    /// <summary>When true (default), refuse to write files if validation has errors.</summary>
    public bool FailOnError { get; init; } = true;

    /// <summary>When null, emit for every sheet; otherwise only these sheet ids.</summary>
    public IReadOnlyList<string>? SheetIds { get; init; }

    /// <summary>Write the companion JSON (default true).</summary>
    public bool WriteJson { get; init; } = true;

    /// <summary>Write smart HTML sheets via Novolis.Markup.Html (default true).</summary>
    public bool WriteHtml { get; init; } = true;

    /// <summary>Write standalone SVG sheets via Novolis.Markup.Svg (default true).</summary>
    public bool WriteSvg { get; init; } = true;

    /// <summary>Write PDF sheets via Svg.Skia + SkiaSharp (default true).</summary>
    public bool WritePdf { get; init; } = true;
}

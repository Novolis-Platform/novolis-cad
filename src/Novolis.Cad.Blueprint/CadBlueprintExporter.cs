using System.Text;
using Novolis.Cad.Primitives;

namespace Novolis.Cad.Blueprint;

/// <summary>
/// Orchestrates project → validate → write <c>.cadblueprint.json</c> + smart HTML sheets.
/// </summary>
public static class CadBlueprintExporter
{
    public static CadBlueprintExportResult Export(CadDocument document, CadBlueprintExportOptions options)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(options);

        var (blueprint, liftDiagnostics) = CadBlueprintProjector.FromCadDocumentWithDiagnostics(
            document,
            options.ContextOverride,
            options.CadDocumentHref);

        return Export(blueprint, options, liftDiagnostics);
    }

    public static CadBlueprintExportResult Export(
        CadBlueprint blueprint,
        CadBlueprintExportOptions options,
        IReadOnlyList<CadBlueprintDiagnostic>? priorDiagnostics = null)
    {
        ArgumentNullException.ThrowIfNull(blueprint);
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(options.OutputDirectory))
        {
            return FailOnly(
                new CadBlueprintDiagnostic(
                    CadBlueprintDiagnosticSeverity.Error,
                    "BP100",
                    "OutputDirectory is required."));
        }

        if (!string.IsNullOrWhiteSpace(options.ContextOverride))
            blueprint.Context = options.ContextOverride!;

        if (!string.IsNullOrWhiteSpace(options.CadDocumentHref))
            blueprint.CadDocumentHref = options.CadDocumentHref;

        var diagnostics = new List<CadBlueprintDiagnostic>();
        if (priorDiagnostics is { Count: > 0 })
            diagnostics.AddRange(priorDiagnostics);
        diagnostics.AddRange(CadBlueprintValidator.Validate(blueprint));

        var hasErrors = CadBlueprintValidator.HasErrors(diagnostics);
        var hasWarnings = diagnostics.Any(d => d.Severity == CadBlueprintDiagnosticSeverity.Warning);
        var fail = (options.FailOnError && hasErrors)
                   || (options.FailOnWarning && hasWarnings);

        if (fail)
        {
            return new CadBlueprintExportResult
            {
                Ok = false,
                Blueprint = blueprint,
                Diagnostics = diagnostics,
                WrittenPaths = [],
            };
        }

        Directory.CreateDirectory(options.OutputDirectory);
        var written = new List<string>();
        var stem = SanitizeStem(options.FileStem
                                ?? (!string.IsNullOrWhiteSpace(blueprint.Name) ? blueprint.Name : "blueprint"));

        try
        {
            if (options.WriteJson)
            {
                var jsonPath = Path.Combine(options.OutputDirectory, $"{stem}.cadblueprint.json");
                CadBlueprintJson.Save(blueprint, jsonPath);
                written.Add(jsonPath);
            }

            if (options.WriteHtml)
            {
                var htmlDir = Path.Combine(options.OutputDirectory, options.SmartHtmlSubfolder);
                Directory.CreateDirectory(htmlDir);
                var sheets = options.SheetIds is { Count: > 0 }
                    ? blueprint.Sheets.Where(s => options.SheetIds.Contains(s.Id, StringComparer.Ordinal)).ToList()
                    : blueprint.Sheets.ToList();

                if (sheets.Count == 0)
                {
                    diagnostics.Add(new CadBlueprintDiagnostic(
                        CadBlueprintDiagnosticSeverity.Warning,
                        "BP101",
                        "No sheets to export as HTML.",
                        "sheets"));
                }

                foreach (var sheet in sheets)
                {
                    var file = Path.Combine(htmlDir, $"{SanitizeStem(sheet.Id)}.html");
                    CadBlueprintHtmlExporter.WriteSheet(blueprint, sheet, file);
                    written.Add(file);
                }
            }
        }
        catch (Exception ex)
        {
            diagnostics.Add(new CadBlueprintDiagnostic(
                CadBlueprintDiagnosticSeverity.Error,
                "BP102",
                $"Export write failed: {ex.Message}"));
            return new CadBlueprintExportResult
            {
                Ok = false,
                Blueprint = blueprint,
                Diagnostics = diagnostics,
                WrittenPaths = written,
            };
        }

        if (options.FailOnWarning && diagnostics.Any(d => d.Severity == CadBlueprintDiagnosticSeverity.Warning))
        {
            return new CadBlueprintExportResult
            {
                Ok = false,
                Blueprint = blueprint,
                Diagnostics = diagnostics,
                WrittenPaths = written,
            };
        }

        return new CadBlueprintExportResult
        {
            Ok = !CadBlueprintValidator.HasErrors(diagnostics),
            Blueprint = blueprint,
            Diagnostics = diagnostics,
            WrittenPaths = written,
        };
    }

    private static CadBlueprintExportResult FailOnly(CadBlueprintDiagnostic diagnostic) =>
        new()
        {
            Ok = false,
            Diagnostics = [diagnostic],
            WrittenPaths = [],
        };

    private static string SanitizeStem(string name)
    {
        var sb = new StringBuilder(name.Length);
        foreach (var c in name.Trim())
            sb.Append(char.IsLetterOrDigit(c) || c is '-' or '_' or '.' ? c : '-');
        var s = sb.ToString().Trim('-');
        return string.IsNullOrEmpty(s) ? "blueprint" : s;
    }
}

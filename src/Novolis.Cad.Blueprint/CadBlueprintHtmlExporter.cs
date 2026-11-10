using System.Text.Json;
using Novolis.Cad.Primitives;
using Novolis.Markup.Html;
using Novolis.Markup.Html.Css;

namespace Novolis.Cad.Blueprint;

/// <summary>
/// Emits smart HTML5 sheets via typed <see cref="HtmlMarkup"/> wrapping
/// <see cref="CadBlueprintSheetSvg"/> (Novolis.Markup.Svg).
/// </summary>
public static class CadBlueprintHtmlExporter
{
    public static string EmitSheet(CadBlueprint blueprint, CadBlueprintSheet sheet)
    {
        ArgumentNullException.ThrowIfNull(blueprint);
        ArgumentNullException.ThrowIfNull(sheet);

        var svg = CadBlueprintSheetSvg.Compose(blueprint, sheet);
        var doc = HtmlMarkup.Document(html =>
        {
            html.Lang("en")
                .CharsetUtf8()
                .Title(sheet.Title);
            html.HtmlRoot
                .Data("nbp-format", CadBlueprintDom.FormatValue)
                .Data("nbp-schema", blueprint.SchemaVersion.ToString());

            html.StyleSheet(css => AppendStyles(css, sheet));

            html.WithBody(body =>
            {
                body.Element("main", main =>
                {
                    main.Id("nbp-stage");
                    main.SvgDocument(svg);
                });

                if (sheet.ShowUi)
                    AppendUi(body, blueprint, sheet);

                var manifest = new
                {
                    format = blueprint.Format,
                    schemaVersion = blueprint.SchemaVersion,
                    name = blueprint.Name,
                    context = blueprint.Context,
                    sheets = new[] { sheet },
                };
                body.Element("script", s =>
                {
                    s.Id(CadBlueprintDom.ManifestElementId)
                        .Type("application/json")
                        .Raw(JsonSerializer.Serialize(manifest, CadJson.Options));
                });
            });
        });

        return doc.ToString();
    }

    public static void WriteSheet(CadBlueprint blueprint, CadBlueprintSheet sheet, string path)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        File.WriteAllText(path, EmitSheet(blueprint, sheet));
    }

    private static void AppendStyles(CssStylesheet css, CadBlueprintSheet sheet)
    {
        css.Raw("""
            :root { --nbp-sheet:#fff; --nbp-ink:#000; --nbp-accent:#333; }
            @page { size: 841mm 594mm; margin: 0; }
            html, body { margin:0; padding:0; background:#fff; color:#111; font:13px/1.35 system-ui,sans-serif; }
            #nbp-stage { margin:0; padding:0; }
            #nbp-sheet { display:block; width:100%; height:auto; background:var(--nbp-sheet); }
            #nbp-ui { max-width:841px; margin:10px auto 20px; padding:0 12px; }
            #nbp-ui details { border:1px solid #ddd; padding:8px 12px; background:#fafafa; }
            #nbp-ui summary { cursor:pointer; font-weight:600; }
            #nbp-ui fieldset { border:1px solid #ccc; margin:8px 0; padding:8px 10px; }
            #nbp-ui legend { padding:0 4px; color:var(--nbp-accent); font-size:11px; }
            #nbp-ui label { display:flex; gap:8px; align-items:center; margin:4px 0; cursor:pointer; }
            #nbp-ui label.locked { opacity:0.65; cursor:default; }
            """);

        css.Media("print", print =>
        {
            print.Raw("""
                #nbp-ui { display:none !important; }
                #nbp-sheet { width:841mm; height:594mm; }
                g[data-nbp-plot="false"] { display:none !important; }
                """);
        });

        foreach (var layer in sheet.Layers.Where(l => !l.Locked))
        {
            var tid = ToggleId(layer.Id);
            css.Raw(
                $"body:has(#{tid}:not(:checked)) g[{CadBlueprintDom.LayerAttr}=\"{layer.Id}\"] {{ display:none; }}");
        }
    }

    private static void AppendUi(HtmlElement body, CadBlueprint blueprint, CadBlueprintSheet sheet)
    {
        body.Element("aside", aside =>
        {
            aside.Id(CadBlueprintDom.UiElementId).Aria("label", "Blueprint layers");
            aside.Element("details", details =>
            {
                details.Element("summary", s => s.Text($"Layers · {sheet.Title} · {blueprint.Context}"));
                details.Element("form", form =>
                {
                    form.Id("nbp-layer-form");
                    var byFolder = sheet.Folders.Count > 0
                        ? sheet.Folders
                        : [new CadBlueprintFolder { Path = "layers", Label = "Layers" }];

                    foreach (var folder in byFolder)
                    {
                        var layers = sheet.Layers.Where(l =>
                            l.Path.StartsWith(folder.Path, StringComparison.Ordinal)
                            || string.Equals(l.Path.Split('/')[0], folder.Path, StringComparison.Ordinal)).ToList();
                        if (layers.Count == 0 && folder.Path is not "chrome")
                            continue;

                        form.Element("fieldset", fs =>
                        {
                            fs.Element("legend", l => l.Text(folder.Label));
                            foreach (var layer in layers.Count > 0 ? layers : sheet.Layers.Where(l => l.Kind is "chrome"))
                                AppendToggle(fs, layer);
                        });
                    }
                });
                details.Element("p", p =>
                {
                    p.Class("meta");
                    p.Text("Toggles use CSS :has(). Print hides UI and non-plot layers.");
                });
            });
        });
    }

    private static void AppendToggle(HtmlElement fieldset, CadBlueprintLayer layer)
    {
        fieldset.Element("label", label =>
        {
            if (layer.Locked)
            {
                label.Class("locked");
                label.Element("input", i => i.Attr("type", "checkbox").Bool("checked").Bool("disabled"));
            }
            else
            {
                var tid = ToggleId(layer.Id);
                label.Element("input", i =>
                {
                    i.Id(tid).Attr("type", "checkbox");
                    if (layer.DefaultVisible)
                        i.Bool("checked");
                });
            }

            label.Text($" {layer.Label}");
        });
    }

    private static string ToggleId(string layerId)
    {
        var chars = layerId.Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray();
        return "t-" + new string(chars);
    }
}

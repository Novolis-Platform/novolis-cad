using System.Text.Json;

namespace Novolis.Cad.Blueprint;

/// <summary>DOM contract constants for HTML5 smart-sheet emitters.</summary>
public static class CadBlueprintDom
{
    public const string FormatAttr = "data-nbp-format";
    public const string FormatValue = "novolis.cad.blueprint";
    public const string SchemaAttr = "data-nbp-schema";
    public const string ManifestElementId = "nbp-manifest";
    public const string SheetElementId = "nbp-sheet";
    public const string UiElementId = "nbp-ui";
    public const string LayerAttr = "data-nbp-layer";
    public const string PathAttr = "data-nbp-path";
    public const string KindAttr = "data-nbp-kind";
    public const string PlotAttr = "data-nbp-plot";
    public const string ViewAttr = "data-nbp-view";
    public const string HiddenClass = "nbp-hidden";
}

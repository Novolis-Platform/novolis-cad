namespace Novolis.Cad.Primitives;

public static class CadWorkspaceMapping
{
    public static CadWorkspace FromViewMode(CadViewMode mode) =>
        mode == CadViewMode.Model ? CadWorkspace.Preview : CadWorkspace.Cad;

    public static CadViewMode ToViewMode(CadWorkspace workspace) =>
        workspace == CadWorkspace.Cad ? CadViewMode.Draft : CadViewMode.Model;

    public static CadWorkspace Parse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return CadWorkspace.Cad;
        return raw.Trim().ToLowerInvariant() switch
        {
            "modeling" or "modeler" => CadWorkspace.Modeling,
            "preview" or "model" or "render" => CadWorkspace.Preview,
            "cad" or "draft" or "sketch" => CadWorkspace.Cad,
            _ => CadWorkspace.Cad,
        };
    }

    public static string ToStorage(CadWorkspace workspace) =>
        workspace switch
        {
            CadWorkspace.Modeling => "modeling",
            CadWorkspace.Preview => "preview",
            _ => "cad",
        };

    public static string ToDisplay(CadWorkspace workspace) =>
        workspace switch
        {
            CadWorkspace.Modeling => "Modeling",
            CadWorkspace.Preview => "Preview",
            _ => "CAD",
        };
}

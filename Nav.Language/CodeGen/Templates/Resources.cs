#region Using Directives

using System.IO;

#endregion

namespace Pharmatechnik.Nav.Language.CodeGen.Templates;

static class Resources {

    // ReSharper disable InconsistentNaming
    public static readonly string IBeginWfsTemplate  = LoadText("IBeginWFS.stg");
    public static readonly string IWfsTemplate       = LoadText("IWFS.stg");
    public static readonly string WfsBaseTemplate    = LoadText("WFSBase.stg");
    public static readonly string WFSOneShotTemplate = LoadText("WFSOneShot.stg");
    public static readonly string CommonTemplate     = LoadText("Common.stg");
    public static readonly string CodeGenFacts       = LoadText("CodeGenFacts.stg");

    public static readonly string TOTemplate = LoadText("TO.stg");
    // ReSharper restore InconsistentNaming

    static string LoadText(string resourceName) {

        var fullResourceName = $"{typeof(Resources).Namespace}.{resourceName}";

        using Stream stream = typeof(Resources).Assembly.GetManifestResourceStream(fullResourceName);
        // ReSharper disable once AssignNullToNotNullAttribute Lass krachen...
        using StreamReader reader = new StreamReader(stream);
        string             result = reader.ReadToEnd();
        return result;

    }

}
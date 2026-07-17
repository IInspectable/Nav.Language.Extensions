using System.IO;

namespace Nav.Language.CodeAnalysis.Tests;

// Lädt die eingebetteten Ressourcen. FrameworkStubs.cs ist aus Nav.Language.Tests verlinkt (siehe
// .csproj) — der Manifest-Name folgt dem Link-Pfad: {RootNamespace}.Resources.FrameworkStubs.cs.
static class Resources {

    public static readonly string FrameworkStubsCode = LoadText("FrameworkStubs.cs");

    static string LoadText(string resourceName) {

        var fullResourceName = $"{typeof(Resources).Namespace}.Resources.{resourceName}";

        using Stream       stream = typeof(Resources).Assembly.GetManifestResourceStream(fullResourceName);
        using StreamReader reader = new StreamReader(stream);

        return reader.ReadToEnd();
    }
}

#region Using Directives

using System;
using System.IO;
using System.Linq;
using Pharmatechnik.Nav.Language.CodeGen;
using Pharmatechnik.Nav.Utilities.IO;

#endregion

namespace Pharmatechnik.Nav.Language;

/// <summary>
/// Standard-Implementierung von <see cref="IPathProvider"/>. Leitet die Zielverzeichnisse aus den
/// Wurzelverzeichnissen der <see cref="GenerationOptions"/> ab und setzt die Artefakt-Dateinamen aus
/// Task-Name und den Namens-Bausteinen zusammen. Trennt die <b>invariante</b> Interface-Ablage
/// (Schnittstellen-Vertrag, aus <c>CodeGenInvariants</c>) von der <b>versionierbaren</b>
/// Implementierungs-Ablage (Namens-Suffixe aus den <c>ICodeGenFacts</c> der Sprach-Generation).
/// </summary>
public class PathProvider: IPathProvider {

    /// <summary>Der Name des Unterordners, in dem generierte Dateien abgelegt werden (<c>generated</c>).</summary>
    public const string GeneratedFolderName = "generated";

    /// <summary>Der Namensbaustein, der generierte Dateien vor der Endung kennzeichnet (<c>…generated.cs</c>).</summary>
    public const string GeneratedFileNameSuffix = "generated";

    /// <summary>Die Dateiendung der erzeugten C#-Dateien (<c>cs</c>).</summary>
    public const string CSharpFileExtension = "cs";

    readonly ICodeGenFacts _facts;

    // Invariante Ablage des IBegin{Task}WFS-Interfaces (Grundsatz 3): der WFL-Namespace der
    // Interface-Ablage ist generationsunabhängig und bewusst getrennt vom gleichlautenden, aber
    // versionierbaren Implementierungs-Namespace der {Task}WFS-Typen (WflGeneratedDirectory).
    readonly string _beginInterfaceGeneratedDirectory;

    /// <summary>
    /// Erzeugt einen Pfad-Provider für eine Task und berechnet die Zielverzeichnisse.
    /// </summary>
    /// <param name="syntaxFileName">Der Pfad der <c>.nav</c>-Quelldatei; darf nicht leer sein.</param>
    /// <param name="taskName">Der Name der Task; darf nicht leer sein.</param>
    /// <param name="generateTo">Optionaler Ziel-Unterordner (aus <c>generate to</c>).</param>
    /// <param name="options">Die Generierungsoptionen; <c>null</c> nutzt <see cref="GenerationOptions.Default"/>.</param>
    /// <param name="facts">Die versionsabhängigen Codegen-Fakten; <c>null</c> nutzt die Default-Generation.</param>
    /// <exception cref="ArgumentException"><paramref name="syntaxFileName"/> oder <paramref name="taskName"/> ist leer.</exception>
    public PathProvider(string syntaxFileName, string taskName, string? generateTo = null, GenerationOptions? options = null, ICodeGenFacts? facts = null) {

        if (String.IsNullOrEmpty(syntaxFileName)) {
            throw new ArgumentException("Missing syntax filename", nameof(syntaxFileName));
        }

        if (String.IsNullOrEmpty(taskName)) {
            throw new ArgumentException("Missing taskName", nameof(taskName));
        }

        options ??= GenerationOptions.Default;
        // Ohne explizit übergebene Generation (etwa bei direkter Konstruktion in Tests) gilt die
        // Default-Generation; die versionsrichtige Instanz reicht die PathProviderFactory herein.
        _facts  =   facts ?? NavCodeGenFacts.For(NavLanguageVersion.Default);

        var translatedToWflDir = PathHelper.TryTranslateToDirectory(syntaxFileName, options.ProjectRootDirectory, options.WflRootDirectory);
        var wflDirectory       = Path.GetDirectoryName(translatedToWflDir);

        var translatedToIwflDir = PathHelper.TryTranslateToDirectory(syntaxFileName, options.ProjectRootDirectory, options.IwflRootDirectory);
        var iwflDirectory       = Path.GetDirectoryName(translatedToIwflDir);

        TaskName       = taskName;
        SyntaxFileName = syntaxFileName;

        // Interface-Ablage — invariant (Schnittstellen-Vertrag): I{Task}WFS unter IWFL, IBegin{Task}WFS unter WFL.
        IwflGeneratedDirectory            = CombinePath(iwflDirectory, CodeGenInvariants.IwflNamespaceSuffix, generateTo, GeneratedFolderName);
        _beginInterfaceGeneratedDirectory = CombinePath(wflDirectory,  CodeGenInvariants.WflNamespaceSuffix,  generateTo, GeneratedFolderName);

        // Implementierungs-Ablage — versionierbar (Namespace-Suffix aus den Facts der Generation).
        WflGeneratedDirectory = CombinePath(wflDirectory, _facts.WflNamespaceSuffix, generateTo, GeneratedFolderName);
        WflDirectory          = CombinePath(wflDirectory, _facts.WflNamespaceSuffix, generateTo);
    }

    /// <summary>Der Name der Task, aus dem die Artefakt-Namen gebildet werden.</summary>
    public string TaskName               { get; }
    /// <summary>Das Verzeichnis der benutzergepflegten Implementierungs-Dateien (WFL).</summary>
    public string WflDirectory           { get; }
    /// <summary>Das Verzeichnis der generierten Implementierungs-Dateien (WFL, <c>generated</c>-Ordner).</summary>
    public string WflGeneratedDirectory  { get; }
    /// <summary>Das Verzeichnis der generierten Schnittstellen-Dateien (IWFL, <c>generated</c>-Ordner).</summary>
    public string IwflGeneratedDirectory { get; }

    /// <inheritdoc/>
    public virtual string SyntaxFileName    { get; }
    // Implementierungs-Dateinamen: versionierbar (Klassen-/Basisklassen-Suffix aus den Facts).
    /// <inheritdoc/>
    public virtual string WfsBaseFileName   => CombinePath(WflGeneratedDirectory,             $"{TaskName}{_facts.WfsBaseClassSuffix}.{GeneratedFileNameSuffix}.{CSharpFileExtension}");
    /// <inheritdoc/>
    public virtual string WfsFileName       => CombinePath(WflDirectory,                      $"{TaskName}{_facts.WfsClassSuffix}.{CSharpFileExtension}");
    // Interface-Dateinamen: invariant (Interface-Präfix/-Suffix aus den CodeGenInvariants).
    /// <inheritdoc/>
    public virtual string IWfsFileName      => CombinePath(IwflGeneratedDirectory,            $"{CodeGenInvariants.InterfacePrefix}{TaskName}{CodeGenInvariants.InterfaceSuffix}.{GeneratedFileNameSuffix}.{CSharpFileExtension}");
    /// <inheritdoc/>
    public virtual string IBeginWfsFileName => CombinePath(_beginInterfaceGeneratedDirectory, $"{CodeGenInvariants.BeginInterfacePrefix}{TaskName}{CodeGenInvariants.InterfaceSuffix}.{GeneratedFileNameSuffix}.{CSharpFileExtension}");

    /// <inheritdoc/>
    public string GetToFileName(string toClassName) {
        return CombinePath(IwflGeneratedDirectory, $"{toClassName}.{GeneratedFileNameSuffix}.{CSharpFileExtension}");
    }

    /// <inheritdoc/>
    public string GetRelativePath(string fromPath, string toPath) {
        return PathHelper.GetRelativePath(fromPath, toPath);
    }

    static string CombinePath(string? first, params string?[] parts) {
        // Path.GetDirectoryName liefert bei Wurzelpfaden null; das darf hier nicht durchschlagen.
        return parts.Where(part => !String.IsNullOrEmpty(part)).Aggregate(first ?? String.Empty, Path.Combine);
    }

}
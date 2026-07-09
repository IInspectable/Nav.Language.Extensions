#region Using Directives

using System;
using System.IO;
using System.Linq;
using Pharmatechnik.Nav.Language.CodeGen;
using Pharmatechnik.Nav.Utilities.IO;

#endregion

namespace Pharmatechnik.Nav.Language;

public class PathProvider: IPathProvider {

    /// <summary>
    /// generated
    /// </summary>
    public const string GeneratedFolderName = "generated";

    /// <summary>
    /// generated
    /// </summary>
    public const string GeneratedFileNameSuffix = "generated";

    /// <summary>
    /// cs
    /// </summary>
    public const string CSharpFileExtension = "cs";

    readonly ICodeGenFacts _facts;

    // Invariante Ablage des IBegin{Task}WFS-Interfaces (Grundsatz 3): der WFL-Namespace der
    // Interface-Ablage ist generationsunabhängig und bewusst getrennt vom gleichlautenden, aber
    // versionierbaren Implementierungs-Namespace der {Task}WFS-Typen (WflGeneratedDirectory).
    readonly string _beginInterfaceGeneratedDirectory;

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

    public string TaskName               { get; }
    public string WflDirectory           { get; }
    public string WflGeneratedDirectory  { get; }
    public string IwflGeneratedDirectory { get; }

    public virtual string SyntaxFileName    { get; }
    // Implementierungs-Dateinamen: versionierbar (Klassen-/Basisklassen-Suffix aus den Facts).
    public virtual string WfsBaseFileName   => CombinePath(WflGeneratedDirectory,             $"{TaskName}{_facts.WfsBaseClassSuffix}.{GeneratedFileNameSuffix}.{CSharpFileExtension}");
    public virtual string WfsFileName       => CombinePath(WflDirectory,                      $"{TaskName}{_facts.WfsClassSuffix}.{CSharpFileExtension}");
    // Interface-Dateinamen: invariant (Interface-Präfix/-Suffix aus den CodeGenInvariants).
    public virtual string IWfsFileName      => CombinePath(IwflGeneratedDirectory,            $"{CodeGenInvariants.InterfacePrefix}{TaskName}{CodeGenInvariants.InterfaceSuffix}.{GeneratedFileNameSuffix}.{CSharpFileExtension}");
    public virtual string IBeginWfsFileName => CombinePath(_beginInterfaceGeneratedDirectory, $"{CodeGenInvariants.BeginInterfacePrefix}{TaskName}{CodeGenInvariants.InterfaceSuffix}.{GeneratedFileNameSuffix}.{CSharpFileExtension}");

    public string GetToFileName(string toClassName) {
        return CombinePath(IwflGeneratedDirectory, $"{toClassName}.{GeneratedFileNameSuffix}.{CSharpFileExtension}");
    }

    public string GetRelativePath(string fromPath, string toPath) {
        return PathHelper.GetRelativePath(fromPath, toPath);
    }

    static string CombinePath(string? first, params string?[] parts) {
        // Path.GetDirectoryName liefert bei Wurzelpfaden null; das darf hier nicht durchschlagen.
        return parts.Where(part => !String.IsNullOrEmpty(part)).Aggregate(first ?? String.Empty, Path.Combine);
    }

}
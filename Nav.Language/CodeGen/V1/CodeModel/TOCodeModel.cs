#region Using Directives

using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.Immutable;

using Pharmatechnik.Nav.Language.Text;

#endregion

namespace Pharmatechnik.Nav.Language.CodeGen;

/// <summary>
/// Das FileGeneration-Modell des Transfer-Objekt-Typs <c>{View}TO</c> — je referenziertem View-Knoten eine
/// eigene Datei. Trägt den Ausschnitt, den der <see cref="TOEmitter"/> als Platzhalter <c>public partial class {View}TO : TO</c>
/// im IWFL-Namespace des Tasks (<see cref="IwflNamespace"/>) erzeugt. Der eigentliche Inhalt gehört dem externen
/// GUI-Generator; nav.exe legt nur das Gerüst an (<see cref="OverwritePolicy.Never"/>).
/// </summary>
// ReSharper disable once InconsistentNaming
sealed class TOCodeModel : FileGenerationCodeModel {

    TOCodeModel(string? relativeSyntaxFileName,
                TaskCodeInfo taskCodeInfo,
                ImmutableList<string> usingNamespaces,
                string? className,
                string? filePath)
        : base(taskCodeInfo, relativeSyntaxFileName, filePath) {

        UsingNamespaces = usingNamespaces ?? throw new ArgumentNullException(nameof(usingNamespaces));
        ClassName       = className       ?? String.Empty;
    }

    /// <summary>Der Name der erzeugten TO-Klasse (<c>{View}TO</c>, Suffix <see cref="CodeGenFacts.ToClassNameSuffix"/>).</summary>
    public string ClassName { get; }

    /// <summary>Die <c>using</c>-Direktiven der TO-Datei (der IWFL-Namespace der Navigation-Engine).</summary>
    public ImmutableList<string> UsingNamespaces { get; }

    /// <summary>Der IWFL-Namespace des Tasks — der Namespace, in den der Platzhalter <c>{View}TO</c> gesetzt wird.</summary>
    public string IwflNamespace => Task.IwflNamespace;

    /// <summary>
    /// Erzeugt je View-Knoten mit mindestens einer Referenz ein TO-Modell: bestimmt Klassennamen und Zielpfad über
    /// den <see cref="IPathProvider"/> und den relativen Pfad zur <c>.nav</c>-Quelldatei.
    /// </summary>
    public static IEnumerable<TOCodeModel> FromTaskDefinition(ITaskDefinitionSymbol taskDefinition, IPathProvider pathProvider, GenerationOptions options) {

        if (taskDefinition == null) {
            throw new ArgumentNullException(nameof(taskDefinition));
        }
        if (pathProvider == null) {
            throw new ArgumentNullException(nameof(pathProvider));
        }

        var taskCodeInfo = TaskCodeInfo.FromTaskDefinition(taskDefinition);

        foreach(var guiNode in taskDefinition.NodeDeclarations.OfType<IGuiNodeSymbol>().Where(n => n.References.Any())) {

            var viewName = guiNode.Name;

            var toClassName = $"{viewName.ToPascalcase()}{CodeGenFacts.ToClassNameSuffix}";
            var filePath    = pathProvider.GetToFileName(guiNode.Name + CodeGenFacts.ToClassNameSuffix);

            var relativeSyntaxFileName = pathProvider.GetRelativePath(filePath, pathProvider.SyntaxFileName);

            yield return new TOCodeModel(
                relativeSyntaxFileName: relativeSyntaxFileName,
                taskCodeInfo          : taskCodeInfo,
                usingNamespaces       : GetUsingNamespaces().ToImmutableList(),
                className             : toClassName,
                filePath              : filePath);
        }
    }

    /// <summary>Die <c>using</c>-Direktiven der TO-Datei — der IWFL-Namespace der Navigation-Engine, sortiert.</summary>
    static IEnumerable<string> GetUsingNamespaces() {

        var namespaces = new List<string> {
            CodeGenFacts.NavigationEngineIwflNamespace
        };

        return namespaces.ToSortedNamespaces();
    }

}

#region Using Directives

using System.IO;

using Microsoft.VisualStudio.Imaging.Interop;

using Pharmatechnik.Nav.Language.Extension.Images;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.CallHierarchy;

/// <summary>
/// Baut aus Engine-Task-Symbolen die VS-Knoten (<see cref="NavCallHierarchyMemberItem"/>) — Pendant zum
/// LSP-<c>CallHierarchyBuilder</c>: <see cref="FromDefinition"/> für Task-Definitionen (Wurzel/Aufrufer),
/// <see cref="FromDeclaration"/> für die Ziel-Deklaration eines ausgehenden Aufrufs (ggf. cross-file).
/// </summary>
static class NavCallHierarchyItemFactory {

    /// <summary>Knoten für eine Task-Definition (Wurzel bei Prepare, Aufrufer bei eingehenden Aufrufen).</summary>
    public static NavCallHierarchyMemberItem FromDefinition(ITaskDefinitionSymbol task) {
        return Build(task.Name, task.Location, ImageMonikers.TaskDefinition);
    }

    /// <summary>
    /// Knoten für eine (ggf. cross-file inkludierte) Task-Deklaration — das Ziel eines ausgehenden Aufrufs.
    /// <see cref="Location.FilePath"/> zeigt bereits auf die Zieldatei, sodass Navigation und Neuauflösung
    /// cross-file korrekt landen.
    /// </summary>
    public static NavCallHierarchyMemberItem FromDeclaration(ITaskDeclarationSymbol declaration) {
        return Build(declaration.Name, declaration.Location, ImageMonikers.TaskDeclaration);
    }

    static NavCallHierarchyMemberItem Build(string name, Location nameLocation, ImageMoniker moniker) {

        if (string.IsNullOrEmpty(nameLocation?.FilePath)) {
            return null;
        }

        // Datei (ohne Endung) als "enthaltender Typ" — analog Roslyn (Namespace.Typ.Member) ergibt das
        // im Toolfenster eine lesbare Qualifizierung "Datei.Task".
        var containingTypeName = Path.GetFileNameWithoutExtension(nameLocation.FilePath);

        return new NavCallHierarchyMemberItem(
            memberName        : name,
            containingTypeName: containingTypeName,
            navigationTarget  : nameLocation,
            filePath          : nameLocation.FilePath,
            offset            : nameLocation.Start,
            glyphMoniker      : moniker);
    }

}

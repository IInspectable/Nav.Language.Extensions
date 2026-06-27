#region Using Directives

using System.Collections.Generic;
using System.IO;

using Microsoft.VisualStudio.Imaging.Interop;
using Microsoft.VisualStudio.Language.CallHierarchy;

using Pharmatechnik.Nav.Language.Extension.Images;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.CallHierarchy;

/// <summary>
/// Baut aus Engine-Task-Symbolen die VS-Knoten (<see cref="NavCallHierarchyMemberItem"/>) — Pendant zum
/// LSP-<c>CallHierarchyBuilder</c>: <see cref="FromDefinition"/> für Task-Definitionen (Wurzel/Aufrufer),
/// <see cref="FromDeclaration"/> für die Ziel-Deklaration eines ausgehenden Aufrufs (ggf. cross-file).
/// <para>
/// <c>details</c> sind die Aufrufstellen, die im Detailbereich des Knotens erscheinen (bei
/// ausgehenden Aufrufen die TaskNodes in der aufrufenden Task; bei der Wurzel keine).
/// </para>
/// </summary>
static class NavCallHierarchyItemFactory {

    /// <summary>Knoten für eine Task-Definition (Wurzel bei Prepare, Aufrufer bei eingehenden Aufrufen).</summary>
    public static NavCallHierarchyMemberItem FromDefinition(ITaskDefinitionSymbol task,
                                                            IReadOnlyList<ICallHierarchyItemDetails> details = null) {
        return Build(task.Name, task.Location, ImageMonikers.TaskDefinition, details);
    }

    /// <summary>
    /// Knoten für eine (ggf. cross-file inkludierte) Task-Deklaration — das Ziel eines ausgehenden Aufrufs.
    /// <see cref="Location.FilePath"/> zeigt bereits auf die Zieldatei, sodass Navigation und Neuauflösung
    /// cross-file korrekt landen. In der Aufrufhierarchie ist auch das Ziel konzeptionell "die Task" →
    /// einheitliches Task-Glyph (nicht das Interface-Glyph der Deklaration).
    /// </summary>
    public static NavCallHierarchyMemberItem FromDeclaration(ITaskDeclarationSymbol declaration,
                                                             IReadOnlyList<ICallHierarchyItemDetails> details = null) {
        return Build(declaration.Name, declaration.Location, ImageMonikers.TaskDefinition, details);
    }

    static NavCallHierarchyMemberItem Build(string name, Location nameLocation, ImageMoniker moniker,
                                            IReadOnlyList<ICallHierarchyItemDetails> details) {

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
            glyphMoniker      : moniker,
            details           : details);
    }

}
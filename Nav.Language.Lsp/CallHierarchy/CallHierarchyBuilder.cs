#region Using Directives

using System;

using Protocol = Microsoft.VisualStudio.LanguageServer.Protocol;

#endregion

namespace Pharmatechnik.Nav.Language.Lsp.CallHierarchy;

/// <summary>
/// Baut die LSP-<see cref="CallHierarchyItem"/>s für die Aufrufhierarchie aus den Engine-Task-Symbolen
/// (analog <see cref="DocumentSymbolBuilder"/>). Eine Task wird als <see cref="Protocol.SymbolKind.Class"/>
/// dargestellt — konsistent zum Document-Outline. <see cref="CallHierarchyItem.Range"/> ist der volle
/// Definitionsblock (falls Syntax vorhanden), <see cref="CallHierarchyItem.SelectionRange"/> der Bezeichner.
/// In <see cref="CallHierarchyItem.Data"/> wandern Dokument-URI + Bezeichner-Offset, damit die incoming-/
/// outgoing-Handler das Item per <c>NavReferenceService.FindSymbol</c> wiederfinden.
/// </summary>
static class CallHierarchyBuilder {

    /// <summary>Item für eine Task-Definition (Aufrufer bei incoming, Wurzel bei prepare).</summary>
    public static CallHierarchyItem? FromDefinition(ITaskDefinitionSymbol task) {
        return Build(task.Name, task.Location, task.Syntax.GetLocation());
    }

    /// <summary>
    /// Item für eine (ggf. cross-file inkludierte) Task-Deklaration — das Ziel eines ausgehenden Aufrufs.
    /// Bei inkludierten Deklarationen ist <see cref="ITaskDeclarationSymbol.Syntax"/> null; dann dient die
    /// Bezeichner-<see cref="Location"/> auch als Block-Bereich. <see cref="Location.FilePath"/> zeigt
    /// bereits auf die Zieldatei, sodass Navigation und Wiederfindung cross-file korrekt landen.
    /// </summary>
    public static CallHierarchyItem? FromDeclaration(ITaskDeclarationSymbol declaration) {
        return Build(declaration.Name, declaration.Location, declaration.Syntax?.GetLocation() ?? declaration.Location);
    }

    static CallHierarchyItem? Build(string name, Location nameLocation, Location blockLocation) {

        if (string.IsNullOrEmpty(nameLocation.FilePath)) {
            return null;
        }

        return new CallHierarchyItem {
            Name           = name,
            Kind           = Protocol.SymbolKind.Class,
            Uri            = new Uri(nameLocation.FilePath),
            Range          = LspMapper.ToRange(blockLocation),
            SelectionRange = LspMapper.ToRange(nameLocation),
            Data           = new CallHierarchyItemData { Uri = nameLocation.FilePath, Offset = nameLocation.Start }
        };
    }
}

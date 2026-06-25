#region Using Directives

using System.Linq;

using JetBrains.Annotations;

using Pharmatechnik.Nav.Language.Text;
using Pharmatechnik.Nav.Language.CodeFixes;
using Pharmatechnik.Nav.Language.CodeFixes.Refactoring;

#endregion

namespace Pharmatechnik.Nav.Language.Rename;

/// <summary>
/// VS-freier Service für das Umbenennen von Nav-Symbolen — Grundlage für LSP <c>textDocument/rename</c>.
/// Liefert zu einer Caret-Position den passenden <see cref="RenameCodeFix"/> aus der vorhandenen
/// Engine-Refactoring-Infrastruktur (<see cref="RenameCodeFixProvider"/>), die auch die VS-Extension nutzt
/// (<c>RenameCommandHandler</c>) — „eine Engine".
/// </summary>
/// <remarks>
/// Wie der VS-Rename ist die Operation <b>dateilokal</b>: alle <see cref="TextChange"/> beziehen sich auf
/// die übergebene <see cref="CodeGenerationUnit"/>. Referenzen einer Task-Definition in <i>anderen</i>
/// Dateien (cross-file via <c>taskref</c>) werden nicht mit umbenannt — eine Task-Referenz wird über ihren
/// lokalen Alias (<c>TaskNodeRenameCodeFix</c>) umbenannt, die Deklaration selbst nur dort, wo sie definiert ist.
/// </remarks>
public static class NavRenameService {

    /// <summary>
    /// Liefert den Rename-CodeFix für das spezifischste Symbol unter <paramref name="position"/>
    /// (0-basierter Offset) — oder <c>null</c>, wenn dort kein umbenennbares Symbol liegt. Caret-Auflösung
    /// über <c>SymbolPosition.SymbolsAt</c> (striktes Enthaltensein), konsistent zu GoTo/References/Hover.
    /// </summary>
    [CanBeNull]
    public static RenameCodeFix GetRenameFix([NotNull] CodeGenerationUnit unit, int position,
                                             [NotNull] TextEditorSettings settings) {

        var symbol = SymbolPosition.SymbolsAt(unit, position).FirstOrDefault();
        if (symbol == null) {
            return null;
        }

        var context = new CodeFixContext(symbol.Location.Extent, unit, settings);

        return RenameCodeFixProvider.SuggestCodeFixes(context).FirstOrDefault();
    }

}

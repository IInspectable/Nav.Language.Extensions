#nullable enable

#region Using Directives

using System.Collections.Generic;
using System.Linq;

#endregion

namespace Pharmatechnik.Nav.Language.GoTo;

/// <summary>
/// Liefert die Nav→Nav-Sprungziele für ein einzelnes Symbol. Dies ist der VS-freie Kern der
/// "Go To Definition"-Logik, die bislang nur VS-gekoppelt im <c>GoToSymbolBuilder</c> der Extension
/// vorlag. Bewusst NICHT abgebildet sind alle Sprünge in den generierten C#-Code (Task-/Begin-/
/// Trigger-/Exit-Codegen) — die hängen an Roslyn/dem VS-Projektmodell und bleiben extension-seitig.
/// </summary>
/// <remarks>
/// Spiegelt exakt die <c>SimpleLocationInfoProvider</c>-Zweige des VS-<c>GoToSymbolBuilder</c>:
/// Include → inkludierte Datei, Task-Node → Task-Deklaration (cross-file), Node-Referenz →
/// Node-Deklaration, Exit-Referenz → Exit-Definition. Alle übrigen Symbole liefern keine Ziele.
/// </remarks>
sealed class GoToTargetResolver: SymbolVisitor<IEnumerable<Location>> {

    static readonly IEnumerable<Location> None = Enumerable.Empty<Location>();

    protected override IEnumerable<Location> DefaultVisit(ISymbol symbol) => None;

    public override IEnumerable<Location> VisitIncludeSymbol(IIncludeSymbol includeSymbol) {
        // Sprung in die inkludierte .nav-Datei (Position 0/0).
        return One(includeSymbol.FileLocation);
    }

    public override IEnumerable<Location> VisitTaskNodeSymbol(ITaskNodeSymbol taskNodeSymbol) {
        // Sprung zur Task-Deklaration — bei inkludierten Tasks cross-file (Location trägt FilePath
        // der inkludierten Datei, auch wenn Syntax/CodeGenerationUnit null sind).
        return One(taskNodeSymbol.Declaration?.Location);
    }

    public override IEnumerable<Location> VisitNodeReferenceSymbol(INodeReferenceSymbol nodeReferenceSymbol) {
        // Sprung zur Node-Deklaration (innerhalb derselben Task-Definition).
        return One(nodeReferenceSymbol.Declaration?.Location);
    }

    public override IEnumerable<Location> VisitExitConnectionPointReferenceSymbol(IExitConnectionPointReferenceSymbol exitConnectionPointReferenceSymbol) {
        // Sprung zur Exit-Definition der referenzierten Task-Deklaration.
        return One(exitConnectionPointReferenceSymbol.Declaration?.Location);
    }

    static IEnumerable<Location> One(Location? location) {
        return location == null ? None : new[] { location };
    }

}

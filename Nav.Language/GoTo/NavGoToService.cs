#region Using Directives

using System.Collections.Generic;
using System.Linq;

using JetBrains.Annotations;

#endregion

namespace Pharmatechnik.Nav.Language.GoTo;

/// <summary>
/// VS-freier "Go To Definition"-Service auf Engine-Ebene. Ermittelt zu einer Caret-Position die
/// Nav→Nav-Sprungziele und ist damit gemeinsam von VS-Extension und LSP-Server nutzbar
/// ("eine Engine"). Sprünge in den generierten C#-Code bleiben bewusst aussen vor — siehe
/// <see cref="GoToTargetResolver"/>.
/// </summary>
public static class NavGoToService {

    /// <summary>
    /// Liefert die Nav→Nav-Sprungziele für die angegebene Zeichen-Position (0-basierter Offset)
    /// innerhalb der <paramref name="unit"/>. Mehrere überlappende Symbole an der Position werden
    /// zusammengeführt; Duplikate (gleiche Datei + Startposition) werden entfernt. Reihenfolge bleibt
    /// stabil (Reihenfolge der Symbole, dann der Ziele).
    /// </summary>
    [NotNull]
    public static IReadOnlyList<Location> GetGoToLocations([NotNull] CodeGenerationUnit unit, int position) {

        var symbols  = SymbolPosition.SymbolsAt(unit, position);
        var resolver = new GoToTargetResolver();

        var seen    = new HashSet<(string, int)>();
        var results = new List<Location>();

        foreach (var location in symbols.SelectMany(resolver.Visit)) {
            if (location != null && seen.Add((location.FilePath, location.Start))) {
                results.Add(location);
            }
        }

        return results;
    }

}

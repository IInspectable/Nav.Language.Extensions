#region Using Directives

using System;
using System.Collections.Generic;
using System.Linq;

#endregion

namespace Pharmatechnik.Nav.Language.GoTo;

/// <summary>
/// VS-freier "Go To Definition"-Service auf Engine-Ebene. Ermittelt zu einer Caret-Position die
/// Nav→Nav-Sprungziele und ist damit gemeinsam von VS-Extension und LSP-Server nutzbar
/// ("eine Engine"). Sprünge in den generierten C#-Code bleiben bewusst außen vor — siehe
/// <see cref="GoToTargetResolver"/>.
/// </summary>
public static class NavGoToService {

    /// <summary>
    /// Liefert die Nav→Nav-Sprungziele für die angegebene Zeichen-Position (0-basierter Offset)
    /// innerhalb der <paramref name="unit"/>. Mehrere überlappende Symbole an der Position werden
    /// zusammengeführt; Duplikate (gleiche Datei + Startposition) werden entfernt. Reihenfolge bleibt
    /// stabil (Reihenfolge der Symbole, dann der Ziele).
    /// </summary>
    public static IReadOnlyList<Location> GetGoToLocations(CodeGenerationUnit unit, int position) {

        var symbols = SymbolPosition.SymbolsAt(unit, position);

        var seen    = new HashSet<(string?, int)>();
        var results = new List<Location>();

        foreach (var location in symbols.SelectMany(GetGoToLocations)) {
            if (seen.Add((location.FilePath, location.Start))) {
                results.Add(location);
            }
        }

        return results;
    }

    /// <summary>
    /// Liefert die Nav→Nav-Sprungziele für ein einzelnes <paramref name="symbol"/> — ohne Positions-
    /// oder Dedup-Logik. Dies ist die geteilte Autorität für die Frage "wohin springt dieses Symbol",
    /// die von VS-Extension und LSP-Server gleichermaßen genutzt wird ("eine Engine"). Symbole ohne
    /// Nav→Nav-Ziel liefern eine leere Liste; Sprünge in den generierten C#-Code sind bewusst nicht
    /// enthalten — siehe <see cref="GoToTargetResolver"/>.
    /// </summary>
    public static IReadOnlyList<Location> GetGoToLocations(ISymbol? symbol) {
        if (symbol == null) {
            return Array.Empty<Location>();
        }

        return new GoToTargetResolver().Visit(symbol).ToList();
    }

}

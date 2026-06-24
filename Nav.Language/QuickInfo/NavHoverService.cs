#region Using Directives

using System.Collections.Immutable;

using JetBrains.Annotations;

using Pharmatechnik.Nav.Language.Text;

#endregion

namespace Pharmatechnik.Nav.Language.QuickInfo;

/// <summary>
/// VS-freier Hover-/QuickInfo-Service auf Engine-Ebene — Grundlage für LSP <c>textDocument/hover</c>.
/// Ermittelt zu einer Caret-Position das Symbol „unter dem Cursor" und dessen klassifizierte
/// Signatur-Bestandteile (<see cref="DisplayPartsBuilder"/>). Gemeinsam von VS-Extension (QuickInfo)
/// und LSP-Server nutzbar („eine Engine").
/// </summary>
public static class NavHoverService {

    /// <summary>
    /// Liefert die Hover-Information zur angegebenen Zeichen-Position (0-basierter Offset) innerhalb
    /// der <paramref name="unit"/> — oder <c>null</c>, wenn an der Position kein Symbol mit anzeigbarer
    /// Signatur liegt. Es wird vom spezifischsten Symbol unter dem Caret ausgegangen und das erste mit
    /// nicht-leeren Anzeige-Bestandteilen verwendet.
    /// </summary>
    [CanBeNull]
    public static NavHoverInfo GetHover([NotNull] CodeGenerationUnit unit, int position) {

        foreach (var symbol in SymbolPosition.SymbolsAt(unit, position)) {

            // Für das init-Keyword mit Alias zeigt auch die VS-QuickInfo bewusst nichts an (der Alias
            // selbst trägt die Information). Steht der Caret hingegen auf dem Alias, greift dieser Zweig
            // nicht — dann liefert DisplayPartsBuilder die Signatur des zugehörigen init-Knotens.
            if (symbol is IInitNodeSymbol { Alias: not null }) {
                continue;
            }

            var parts = symbol.ToDisplayParts();
            if (!parts.IsDefaultOrEmpty) {
                return new NavHoverInfo(parts, symbol.Location);
            }
        }

        return null;
    }

}

/// <summary>
/// Ergebnis von <see cref="NavHoverService.GetHover"/>: die klassifizierte Signatur des Symbols unter
/// dem Caret sowie dessen <see cref="Language.Location"/> (für den Hover-Bereich). Protokoll-frei.
/// </summary>
public sealed class NavHoverInfo {

    public NavHoverInfo(ImmutableArray<ClassifiedText> displayParts, [CanBeNull] Location location) {
        DisplayParts = displayParts;
        Location     = location;
    }

    /// <summary>Die klassifizierten Signatur-Bestandteile (z.B. <c>task</c> + <c> </c> + <c>Foo</c>).</summary>
    public ImmutableArray<ClassifiedText> DisplayParts { get; }

    /// <summary>Der Namens-Bereich des Symbols unter dem Caret; kann <c>null</c> sein.</summary>
    [CanBeNull]
    public Location Location { get; }

}

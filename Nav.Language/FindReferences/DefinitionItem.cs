#region Using Directives

using System.Collections.Immutable;

using Pharmatechnik.Nav.Language.Text;

#endregion

namespace Pharmatechnik.Nav.Language.FindReferences;

/// <summary>
/// Eine Definition im Ergebnis einer Referenzsuche — der gesuchte Gegenstand, unter dem die
/// gefundenen <see cref="ReferenceItem"/>s gruppiert werden (z.B. eine Task-Definition oder ein
/// Verbindungspunkt). Sie trägt das zugrunde liegende <see cref="Symbol"/>, den klassifizierten
/// Anzeigetext (<see cref="TextParts"/>) und Sortier-/Darstellungshinweise. Die Factory-Methoden
/// stehen im Gegenstück <c>DefinitionItem.Factory.cs</c>. Roslyn-Analogon
/// <c>Microsoft.CodeAnalysis.FindUsages.DefinitionItem</c>.
/// </summary>
public partial class DefinitionItem {

    DefinitionItem(ISymbol? symbol,
                   ImmutableArray<ClassifiedText> textParts,
                   bool expandedByDefault,
                   string? sortKey) {

        Symbol            = symbol;
        TextParts         = textParts;
        ExpandedByDefault = expandedByDefault;
        SortKey           = sortKey ?? "";
    }

    /// <summary>Das zugrunde liegende Symbol dieser Definition; <c>null</c> bei einer reinen Text-Definition (z.B. einer Meldung).</summary>
    public ISymbol? Symbol { get; }

    /// <summary>Die Location der Definition — die <see cref="ISymbol.Location"/> des <see cref="Symbol"/>, oder <c>null</c>.</summary>
    public Location? Location => Symbol?.Location;

    /// <summary>Der klassifizierte Anzeigetext der Definition.</summary>
    public ImmutableArray<ClassifiedText> TextParts         { get; }
    /// <summary>Ob der Definitionsknoten im Ergebnisbaum standardmäßig aufgeklappt dargestellt wird.</summary>
    public bool                           ExpandedByDefault { get; } // TODO In etwas allgemeineres umbenennen, oder ganz weglassen?
    /// <summary>Der Sortierschlüssel, der die Reihenfolge der Definitionen im Ergebnisbaum steuert (siehe <see cref="SortText"/>).</summary>
    public string                         SortKey           { get; }

    /// <summary>Der Anzeigetext der Definition als zusammengefügte Zeichenkette (<see cref="TextParts"/>).</summary>
    public string Text     => TextParts.JoinText();
    /// <summary>Der zum Sortieren verwendete Text — <see cref="SortKey"/> gefolgt von <see cref="Text"/>.</summary>
    public string SortText => SortKey + Text;

}
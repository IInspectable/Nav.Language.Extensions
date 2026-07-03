#nullable enable

#region Using Directives

using System.Collections.Immutable;

using Pharmatechnik.Nav.Language.Text;

#endregion

namespace Pharmatechnik.Nav.Language.FindReferences;

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

    public ISymbol? Symbol { get; }

    public Location? Location => Symbol?.Location;

    public ImmutableArray<ClassifiedText> TextParts         { get; }
    public bool                           ExpandedByDefault { get; } // TODO In etwas allgemeineres umbenennen, oder ganz weglassen?
    public string                         SortKey           { get; }

    public string Text     => TextParts.JoinText();
    public string SortText => SortKey + Text;

}
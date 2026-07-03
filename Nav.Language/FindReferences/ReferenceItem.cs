#nullable enable

#region Using Directives

using System;
using System.Collections.Immutable;

using Pharmatechnik.Nav.Language.Text;

#endregion

namespace Pharmatechnik.Nav.Language.FindReferences;

public class ReferenceItem {

    public ReferenceItem(DefinitionItem definition,
                         Location? location,
                         ImmutableArray<ClassifiedText> textParts,
                         TextExtent textHighlightExtent,
                         ImmutableArray<ClassifiedText> toolTipParts,
                         TextExtent toolTipHighlightExtent) {

        Definition             = definition ?? throw new ArgumentNullException(nameof(definition));
        Location               = location   ?? throw new ArgumentNullException(nameof(location));
        TextParts              = textParts;
        TextHighlightExtent    = textHighlightExtent;
        ToolTipParts           = toolTipParts;
        ToolTipHighlightExtent = toolTipHighlightExtent;

    }

    public static ReferenceItem NoReferencesFoundTo(DefinitionItem definition) {
        //return new ReferenceItem(
        //    definition            : definition,
        //    location              : definition.Location,
        //    textParts             : new[] {
        //        ClassifiedTexts.Text($"No references found to '{definition.Text}'.")
        //    }.ToImmutableArray(),
        //    textHighlightExtent   : TextExtent.Missing,
        //    toolTipParts          : ImmutableArray<ClassifiedText>.Empty,
        //    toolTipHighlightExtent: TextExtent.Missing
        //);
        return CreateSimpleMessage(definition, $"No references found to '{definition.Text}'.");
    }

    public static ReferenceItem CreateSimpleMessage(DefinitionItem definition, string message) {
        return new ReferenceItem(
            definition            : definition,
            location              : definition.Location,
            textParts             : new[] {
                ClassifiedTexts.Text(message)
            }.ToImmutableArray(),
            textHighlightExtent   : TextExtent.Missing,
            toolTipParts          : ImmutableArray<ClassifiedText>.Empty,
            toolTipHighlightExtent: TextExtent.Missing
        );
    }

    public ReferenceItem With(DefinitionItem? definition = null,
                              Location? location = null,
                              ImmutableArray<ClassifiedText>? textParts = null,
                              TextExtent? textHighlightExtent = null,
                              ImmutableArray<ClassifiedText>? toolTipParts = null,
                              TextExtent? toolTipHighlightExtent = null) {
        return new ReferenceItem(
            definition            : definition             ?? Definition,
            location              : location               ?? Location,
            textParts             : textParts              ?? TextParts,
            textHighlightExtent   : textHighlightExtent    ?? TextHighlightExtent,
            toolTipParts          : toolTipParts           ?? ToolTipParts,
            toolTipHighlightExtent: toolTipHighlightExtent ?? ToolTipHighlightExtent
        );
    }

    public DefinitionItem                 Definition             { get; }
    public Location                       Location               { get; }
    public ImmutableArray<ClassifiedText> TextParts              { get; }
    public TextExtent                     TextHighlightExtent    { get; }
    public ImmutableArray<ClassifiedText> ToolTipParts           { get; }
    public TextExtent                     ToolTipHighlightExtent { get; }

    public string Text    => TextParts.JoinText();
    public string ToolTip => ToolTipParts.JoinText();

}
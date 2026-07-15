#region Using Directives

using System;
using System.Collections.Immutable;

using Pharmatechnik.Nav.Language.Text;

#endregion

namespace Pharmatechnik.Nav.Language.FindReferences;

/// <summary>
/// Eine einzelne Fundstelle im Ergebnis einer Referenzsuche — verortet unter der
/// <see cref="DefinitionItem"/>, zu der sie gehört. Neben der <see cref="Location"/> trägt sie die
/// bereits klassifizierten Anzeigetexte für die Zeile (<see cref="TextParts"/>) und den Tooltip
/// (<see cref="ToolTipParts"/>) samt der jeweils hervorzuhebenden Bereiche. Roslyn-Analogon
/// <c>Microsoft.CodeAnalysis.FindUsages.SourceReferenceItem</c>.
/// </summary>
public class ReferenceItem {

    /// <summary>
    /// Erzeugt eine Fundstelle.
    /// </summary>
    /// <param name="definition">Die Definition, zu der diese Fundstelle gehört.</param>
    /// <param name="location">Die Verortung der Fundstelle; darf nicht <c>null</c> sein.</param>
    /// <param name="textParts">Der klassifizierte Anzeigetext der Ergebniszeile.</param>
    /// <param name="textHighlightExtent">Der innerhalb des Anzeigetexts hervorzuhebende Bereich.</param>
    /// <param name="toolTipParts">Der klassifizierte Text des Tooltips.</param>
    /// <param name="toolTipHighlightExtent">Der innerhalb des Tooltips hervorzuhebende Bereich.</param>
    /// <exception cref="ArgumentNullException"><paramref name="definition"/> oder <paramref name="location"/> ist <c>null</c>.</exception>
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

    /// <summary>
    /// Erzeugt eine Platzhalter-Fundstelle mit der Meldung, dass zu <paramref name="definition"/> keine
    /// Referenzen gefunden wurden.
    /// </summary>
    /// <param name="definition">Die Definition, zu der nichts gefunden wurde.</param>
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

    /// <summary>
    /// Erzeugt eine Fundstelle, die statt eines echten Treffers nur die <paramref name="message"/> an
    /// der Position der <paramref name="definition"/> anzeigt (ohne Hervorhebung, ohne Tooltip).
    /// </summary>
    /// <param name="definition">Die Definition, deren Position übernommen wird.</param>
    /// <param name="message">Der anzuzeigende Meldungstext.</param>
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

    /// <summary>
    /// Liefert eine Kopie dieser Fundstelle, in der nur die ausdrücklich übergebenen (nicht
    /// <c>null</c>) Bestandteile ersetzt sind; alle übrigen werden unverändert übernommen.
    /// </summary>
    /// <param name="definition">Neue Definition oder <c>null</c> für unverändert.</param>
    /// <param name="location">Neue Verortung oder <c>null</c> für unverändert.</param>
    /// <param name="textParts">Neuer Anzeigetext oder <c>null</c> für unverändert.</param>
    /// <param name="textHighlightExtent">Neuer Hervorhebungsbereich der Zeile oder <c>null</c> für unverändert.</param>
    /// <param name="toolTipParts">Neuer Tooltip-Text oder <c>null</c> für unverändert.</param>
    /// <param name="toolTipHighlightExtent">Neuer Hervorhebungsbereich des Tooltips oder <c>null</c> für unverändert.</param>
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

    /// <summary>Die Definition, zu der diese Fundstelle gehört.</summary>
    public DefinitionItem                 Definition             { get; }
    /// <summary>Die Verortung der Fundstelle (Datei und Textausschnitt).</summary>
    public Location                       Location               { get; }
    /// <summary>Der klassifizierte Anzeigetext der Ergebniszeile.</summary>
    public ImmutableArray<ClassifiedText> TextParts              { get; }
    /// <summary>Der innerhalb von <see cref="TextParts"/> hervorzuhebende Bereich.</summary>
    public TextExtent                     TextHighlightExtent    { get; }
    /// <summary>Der klassifizierte Text des Tooltips.</summary>
    public ImmutableArray<ClassifiedText> ToolTipParts           { get; }
    /// <summary>Der innerhalb von <see cref="ToolTipParts"/> hervorzuhebende Bereich.</summary>
    public TextExtent                     ToolTipHighlightExtent { get; }

    /// <summary>Der Anzeigetext der Ergebniszeile als zusammengefügte Zeichenkette (<see cref="TextParts"/>).</summary>
    public string Text    => TextParts.JoinText();
    /// <summary>Der Tooltip-Text als zusammengefügte Zeichenkette (<see cref="ToolTipParts"/>).</summary>
    public string ToolTip => ToolTipParts.JoinText();

}
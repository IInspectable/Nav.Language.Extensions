#region Using Directives

using System;
using System.Linq;
using System.Text;
using System.Collections.Generic;

#endregion

namespace Pharmatechnik.Nav.Language.Text;

/// <summary>
/// Wendet eine Menge von <see cref="TextChange"/> auf einen Ausgangstext an und liefert den geänderten
/// Text. Die Changes müssen paarweise disjunkt sein (keine Überlappung); ihre Reihenfolge ist beliebig,
/// da intern nach <see cref="TextExtent.Start"/> sortiert wird. Verbraucher ist u.a. der Formatter
/// (<see cref="Formatting.NavFormattingService.FormatDocument"/>).
/// </summary>
public class TextChangeWriter {

    /// <summary>
    /// Wendet alle <paramref name="textChanges"/> auf <paramref name="text"/> an und liefert das Ergebnis.
    /// Die Changes werden nach Startposition sortiert und dann von vorne nach hinten angewandt, wobei ein
    /// laufender Offset die durch frühere Changes verschobenen Positionen ausgleicht — der jeweilige
    /// <see cref="TextChange.Extent"/> bezieht sich also auf den <i>ursprünglichen</i> Text.
    /// </summary>
    /// <param name="text">Der Ausgangstext.</param>
    /// <param name="textChanges">Die anzuwendenden Änderungen; müssen paarweise disjunkt sein.</param>
    /// <returns>Der Text nach Anwendung aller Änderungen.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> oder <paramref name="textChanges"/> ist <c>null</c>.</exception>
    /// <exception cref="ArgumentException">Zwei Änderungen überlappen sich.</exception>
    public string ApplyTextChanges(string text, IEnumerable<TextChange> textChanges) {

        if (text == null) {
            throw new ArgumentNullException(nameof(text));
        }
        if (textChanges == null) {
            throw new ArgumentNullException(nameof(textChanges));
        }

        var orderedChanges = textChanges.OrderBy(tc => tc.Extent.Start).ToList();
        CheckForOverlappingChanges(orderedChanges);

        StringBuilder textBuilder = new StringBuilder(text);

        int offset = 0;
        foreach (var change in orderedChanges) {
                
            int start = offset + change.Extent.Start;

            textBuilder.Remove(start, change.Extent.Length);
            textBuilder.Insert(start, change.ReplacementText);

            offset += change.ReplacementText.Length - change.Extent.Length;
        }

        return textBuilder.ToString();
    }

    /// <summary>
    /// Stellt sicher, dass sich keine zwei — bereits nach Startposition sortierten — Änderungen
    /// überlappen; andernfalls wäre das Ergebnis nicht wohldefiniert.
    /// </summary>
    /// <param name="orderedChanges">Die nach <see cref="TextExtent.Start"/> aufsteigend sortierten Änderungen.</param>
    /// <exception cref="ArgumentException">Zwei Änderungen überlappen sich.</exception>
    void CheckForOverlappingChanges(IEnumerable<TextChange> orderedChanges) {

        int currentEnd = 0;
        foreach (var change in orderedChanges) {
            if (change.Extent.Start < currentEnd) {
                throw new ArgumentException("Overlapping changes are not supported");
            }

            currentEnd = change.Extent.End;
        }
    }
}
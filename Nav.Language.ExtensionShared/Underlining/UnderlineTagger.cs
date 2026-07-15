#region Using Directives

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;
using Pharmatechnik.Nav.Language.Extension.Common;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.Underlining; 

/// <summary>
/// Verwaltet die Menge der zu unterstreichenden Textstellen eines Puffers als logische
/// <see cref="UnderlineTag"/>s. Über <see cref="AddUnderlineSpan"/>/<see cref="RemoveUnderlineSpan"/>/
/// <see cref="RemoveAllUnderlineSpans"/> gepflegt und pro <see cref="ITextBuffer"/> als Singleton geführt
/// (<see cref="UnderlineTaggerProvider"/>); der
/// <see cref="Pharmatechnik.Nav.Language.Extension.Classification.UnderlineClassifier"/> setzt die Tags in
/// die sichtbare Unterstreichung um. Die Ausgabe der Tags in <see cref="GetTags"/> ist derzeit
/// deaktiviert.
/// </summary>
public class UnderlineTagger: ITagger<UnderlineTag> , IDisposable{

    readonly ITextBuffer        _textBuffer;
    readonly List<SnapshotSpan> _underlineSpans;

    /// <summary>Erzeugt einen Tagger für den angegebenen Puffer mit anfangs leerer Unterstreichungs-Menge.</summary>
    public UnderlineTagger(ITextBuffer textBuffer) {
        _underlineSpans = new List<SnapshotSpan>();
        _textBuffer     = textBuffer;
    }

    /// <summary>Nimmt eine Spanne in die Unterstreichungs-Menge auf (Duplikate werden ignoriert) und meldet die Änderung.</summary>
    public void AddUnderlineSpan(SnapshotSpan span) {
        if(_underlineSpans.Any(underlineSpan => underlineSpan == span)) {
            return;
        }

        _underlineSpans.Add(span);

        var args = new SnapshotSpanEventArgs(span);
        TagsChanged?.Invoke(this, args);
    }

    /// <summary>Entfernt eine Spanne aus der Unterstreichungs-Menge und meldet die Änderung, falls etwas entfernt wurde.</summary>
    public void RemoveUnderlineSpan(SnapshotSpan span) {
        if(_underlineSpans.RemoveAll(s=> s == span) > 0) {
            var args = new SnapshotSpanEventArgs(span);
            TagsChanged?.Invoke(this, args);
        }            
    }

    /// <summary>Leert die Unterstreichungs-Menge und meldet den gesamten Snapshot als geändert.</summary>
    public void RemoveAllUnderlineSpans() {
        if (_underlineSpans.Count == 0) {
            return;
        }

        _underlineSpans.Clear();

        var args = new SnapshotSpanEventArgs(_textBuffer.CurrentSnapshot.GetFullSpan());
        TagsChanged?.Invoke(this, args);
    }

    /// <summary>Liefert bzw. erzeugt den an den Puffer gebundenen <see cref="UnderlineTagger"/>-Singleton.</summary>
    public static UnderlineTagger GetOrCreateSingelton(ITextBuffer textBuffer) {

        return textBuffer.Properties.GetOrCreateSingletonProperty(
            () => new UnderlineTagger(textBuffer));
    }

    /// <summary>Liefert den Puffer-Singleton als <see cref="ITagger{T}"/> (für die MEF-Provider-Schnittstelle).</summary>
    public static ITagger<T> GetOrCreateSingelton<T>(ITextBuffer textBuffer) where T : ITag {
        return GetOrCreateSingelton(textBuffer) as ITagger<T>;
    }

    /// <summary>Wird ausgelöst, wenn sich die Unterstreichungs-Menge geändert hat.</summary>
    public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

    /// <summary>
    /// Liefert die <see cref="UnderlineTag"/>s der angefragten Bereiche. Die Ausgabe ist derzeit
    /// deaktiviert (liefert stets eine leere Folge).
    /// </summary>
    public IEnumerable<ITagSpan<UnderlineTag>> GetTags(NormalizedSnapshotSpanCollection spans) {
        yield break;
        //foreach (var span in spans) {
        //    foreach (var underlineSpan in _underlineSpans) {

        //        var tagSpan = underlineSpan.TranslateTo(span.Snapshot, SpanTrackingMode.EdgeExclusive);
        //        if (span.IntersectsWith(tagSpan)) {                        
        //            var tag     = new UnderlineTag();
        //            yield return new TagSpan<UnderlineTag>(tagSpan, tag);
        //        }
        //    }
        //}
    }

    /// <summary>Aktuell ohne Wirkung — der Tagger hält keine freizugebenden Ressourcen.</summary>
    public void Dispose() {
    }
}
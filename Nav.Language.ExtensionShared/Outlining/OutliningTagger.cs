#region Using Directives

using System;
using System.Collections.Generic;

using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;
using Pharmatechnik.Nav.Language.Extension.Common;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.Outlining; 

/// <summary>
/// Fabrik für <see cref="IOutliningRegionTag"/>-Instanzen, die den einzelnen Teil-Taggern
/// (<see cref="TaskDefinitionsOutlineTagger"/>, <see cref="NodeDeclarationBlockOutlineTagger"/> usw.)
/// gereicht wird. So bleibt die WPF-Aufklapp-Inhalt-Erzeugung (Hover-Vorschau) beim
/// <see cref="OutliningTagger"/> gebündelt und muss nicht in jedem Teil-Tagger wiederholt werden.
/// </summary>
interface IOutliningRegionTagCreator {
    /// <summary>
    /// Erzeugt einen Outlining-Tag mit der eingeklappten Darstellung <paramref name="collapsed"/> und
    /// einem Hover-Inhalt, der über den Text im <paramref name="span"/> gebildet wird.
    /// </summary>
    /// <param name="collapsed">Die im eingeklappten Zustand angezeigte Ersatzdarstellung (z.B. „...").</param>
    /// <param name="span">Der Bereich, dessen Text als Vorschau (Hint) beim Überfahren gezeigt wird.</param>
    IOutliningRegionTag CreateTag(object collapsed, SnapshotSpan span);
}

/// <summary>
/// Tagger, der die aufklappbaren Regionen (Outlining) einer <c>.nav</c>-Datei liefert. Er bündelt die
/// Ergebnisse mehrerer spezialisierter Teil-Tagger (Task-Definitionen, Knoten-Deklarationen,
/// Transitionsblöcke, mehrzeilige Kommentare, <c>using</c>-/<c>taskref</c>-Blöcke) zu einer gemeinsamen
/// Tag-Menge. Als <see cref="ParserServiceDependent"/> lauscht er auf den <see cref="ParserService"/> und
/// baut die Regionen bei jeder Änderung des Syntaxbaums neu auf. Erzeugt von
/// <see cref="OutliningTaggerProvider"/>.
/// </summary>
sealed class OutliningTagger: ParserServiceDependent, ITagger<IOutliningRegionTag>, IOutliningRegionTagCreator {

    readonly List<ITagSpan<IOutliningRegionTag>> _outLineTags;
    readonly CodeContentControlProvider          _codeContentControlProvider;

    /// <summary>
    /// Initialisiert den Tagger für den angegebenen <paramref name="textBuffer"/> und hinterlegt den
    /// <paramref name="codeContentControlProvider"/>, der den WPF-Vorschauinhalt der eingeklappten
    /// Regionen erzeugt.
    /// </summary>
    public OutliningTagger(ITextBuffer textBuffer, CodeContentControlProvider codeContentControlProvider): base(textBuffer) {

        _outLineTags                = new List<ITagSpan<IOutliningRegionTag>>();
        _codeContentControlProvider = codeContentControlProvider;
    }

    /// <summary>Wird ausgelöst, wenn sich die Menge der Outlining-Regionen geändert hat (nach einem Reparse).</summary>
    public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

    /// <inheritdoc/>
    public IOutliningRegionTag CreateTag(object collapsed, SnapshotSpan span) {
        return new OutliningRegionTag(false, false, collapsed, _codeContentControlProvider.CreateContentControlForOutlining(span));
    }

    /// <summary>
    /// Liefert die zwischengespeicherten Outlining-Tags. Sie werden nicht je Anfrage neu berechnet,
    /// sondern in <see cref="OnParseResultChanged"/> nach jedem Reparse einmal aufgebaut.
    /// </summary>
    public IEnumerable<ITagSpan<IOutliningRegionTag>> GetTags(NormalizedSnapshotSpanCollection spans) {
        return _outLineTags;
    }
        
    /// <summary>
    /// Baut nach einem abgeschlossenen Parse-Lauf die Regionen aus dem aktuellen Syntaxbaum neu auf und
    /// meldet die Änderung über <see cref="TagsChanged"/>.
    /// </summary>
    protected override void OnParseResultChanged(object sender, SnapshotSpanEventArgs e) {
        var syntaxTreeAndSnapshot = ParserService.SyntaxTreeAndSnapshot;
        if (syntaxTreeAndSnapshot == null) {
            return;
        }

        UpdateRegions(syntaxTreeAndSnapshot);
            
        TagsChanged?.Invoke(this, e);
    }

    /// <summary>
    /// Leert die Tag-Liste und füllt sie aus dem übergebenen Syntaxbaum, indem der Reihe nach alle
    /// spezialisierten Teil-Tagger befragt werden (jeweils mit diesem Tagger als
    /// <see cref="IOutliningRegionTagCreator"/>).
    /// </summary>
    void UpdateRegions(SyntaxTreeAndSnapshot syntaxTreeAndSnapshot) {
        _outLineTags.Clear();            
        //_outLineTags.AddRange(CodeNamespaceDeclarationOutlineTagger.GetTags(syntaxTreeAndSnapshot, this));
        _outLineTags.AddRange(CodeUsingDirectiveOutlineTagger.GetTags(syntaxTreeAndSnapshot, this));
        _outLineTags.AddRange(TaskReferenceOutlineTagger.GetTags(syntaxTreeAndSnapshot, this));
        _outLineTags.AddRange(TaskDefinitionsOutlineTagger.GetTags(syntaxTreeAndSnapshot, this));
        _outLineTags.AddRange(NodeDeclarationBlockOutlineTagger.GetTags(syntaxTreeAndSnapshot, this));
        _outLineTags.AddRange(TransitionDefinitionBlockOutlineTagger.GetTags(syntaxTreeAndSnapshot, this));
        _outLineTags.AddRange(MultilineCommentOutlineTagger.GetTags(syntaxTreeAndSnapshot, this));
    }        
}
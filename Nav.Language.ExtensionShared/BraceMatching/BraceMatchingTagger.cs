#region Using Directives

using System;
using System.Linq;
using System.Threading;
using System.Reactive.Linq;
using System.Collections.Generic;

using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Pharmatechnik.Nav.Language.Extension.Common;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.BraceMatching; 

/// <summary>
/// Hebt beim Bewegen des Caret die zusammengehörigen Klammern hervor: steht der Caret an einer
/// öffnenden oder schließenden Klammer (bzw. an einem Anführungszeichen eines String-Literals), werden
/// beide Enden des Paars mit einem <see cref="TextMarkerTag"/>
/// (<see cref="BraceMatchingTypeNames.BraceMatching"/>) markiert. Als <see cref="ParserServiceDependent"/>
/// arbeitet er auf dem aktuellen Syntaxbaum; Caret- und Layout-Änderungen werden — wie im C#-Editor —
/// gedrosselt (<see cref="ServiceProperties.BraceMatchingThrottleTime"/>). Instanziiert über
/// <see cref="BraceMatchingTaggerProvider"/>.
/// </summary>
sealed class BraceMatchingTagger : ParserServiceDependent, ITagger<TextMarkerTag> {

    static readonly List<BracePair<SyntaxTokenType>> BracePairs;

    readonly IDisposable _observable;

    /// <summary>Legt die unterstützten Klammerpaare fest: <c>[]</c>, <c>{}</c>, <c>()</c> und <c>&lt;&gt;</c>.</summary>
    static BraceMatchingTagger() {
        BracePairs = new List<BracePair<SyntaxTokenType>> {
            BracePair.Create(SyntaxTokenType.OpenBracket, SyntaxTokenType.CloseBracket),
            BracePair.Create(SyntaxTokenType.OpenBrace  , SyntaxTokenType.CloseBrace),
            BracePair.Create(SyntaxTokenType.OpenParen  , SyntaxTokenType.CloseParen),
            BracePair.Create(SyntaxTokenType.LessThan   , SyntaxTokenType.GreaterThan)
        };
    }

    /// <summary>
    /// Bindet den Tagger an die <see cref="ITextView"/>, abonniert Caret- und Layout-Änderungen und
    /// richtet die gedrosselte Neuberechnung der Hervorhebung ein.
    /// </summary>
    public BraceMatchingTagger(ITextView view, ITextBuffer textBuffer) : base(textBuffer) {

        View = view;
                      
        View.Caret.PositionChanged += OnCaretPositionChanged;
        View.LayoutChanged         += OnViewLayoutChanged;

        // Wir drosseln hier das Brace Matching etwas, um nicht zu viel 
        // Unruhe in die GUI zu bekommen. Der C# Editor verzögert ähnlich.
        _observable = Observable.FromEventPattern<EventArgs>(
                                     handler => Invalidated += handler,
                                     handler => Invalidated -= handler)
                                .Throttle(ServiceProperties.BraceMatchingThrottleTime)
                                .ObserveOn(SynchronizationContext.Current)
                                .Subscribe(_=> OnTagsChanged());            
    }
        
    /// <summary>Meldet die Ereignis-Abonnements ab und gibt die gedrosselte Beobachtung frei.</summary>
    public override void Dispose() {
        base.Dispose();

        _observable.Dispose();

        View.Caret.PositionChanged -= OnCaretPositionChanged;
        View.LayoutChanged         -= OnViewLayoutChanged;
    }
       
    /// <summary>Die Text-Ansicht, deren Caret die Klammer-Hervorhebung steuert.</summary>
    public ITextView View { get; }

    /// <summary>Stößt bei Layout-Änderungen der View eine (gedrosselte) Neuberechnung an.</summary>
    void OnViewLayoutChanged(object sender, TextViewLayoutChangedEventArgs e) {
        Invalidated?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Stößt bei geändertem Parse-Ergebnis eine (gedrosselte) Neuberechnung an.</summary>
    protected override void OnParseResultChanged(object sender, SnapshotSpanEventArgs e) {
        Invalidated?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Stößt bei Caret-Bewegung eine (gedrosselte) Neuberechnung an.</summary>
    void OnCaretPositionChanged(object sender, CaretPositionChangedEventArgs e) {
        Invalidated?.Invoke(this, EventArgs.Empty);
    }
        
    /// <summary>Internes Signal, dass die Hervorhebung neu berechnet werden muss (gedrosselt, siehe Konstruktor).</summary>
    event EventHandler<EventArgs> Invalidated;

    /// <summary>Wird ausgelöst, wenn sich die Klammer-Hervorhebung geändert hat (VS fordert dann neue Tags an).</summary>
    public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

    /// <summary>Meldet nach der Drosselung den gesamten Snapshot als geändert, sodass VS die Tags neu anfragt.</summary>
    void OnTagsChanged() {
        var syntaxTreeAndSnapshot = ParserService.SyntaxTreeAndSnapshot;
        if(syntaxTreeAndSnapshot ==null) {
            return;
        }

        var snapshot     = syntaxTreeAndSnapshot.Snapshot;
        var snapshotSpan = new SnapshotSpan(snapshot, new Span(0, snapshot.Length));

        TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(snapshotSpan));
    }
        
    /// <summary>
    /// Ermittelt zum aktuellen Caret die hervorzuhebenden Klammern: Steht der Caret an einer öffnenden
    /// Klammer, wird die passende schließende gesucht (und umgekehrt); an den Grenzen eines
    /// String-Literals werden dessen Anführungszeichen markiert. Liefert je Fund zwei
    /// <see cref="TextMarkerTag"/>-Spannen (beide Enden), sonst nichts.
    /// </summary>
    /// <param name="spans">Die vom Editor angefragten Bereiche (nur zur Snapshot-Ausrichtung genutzt).</param>
    /// <returns>Die Marker-Tags der beiden zusammengehörigen Klammern bzw. eine leere Folge.</returns>
    public IEnumerable<ITagSpan<TextMarkerTag>> GetTags(NormalizedSnapshotSpanCollection spans) {

        if (spans.Count == 0) {
            //there is no content in the buffer
            yield break;
        }

        var currentCharPoint = View.Caret.Position.Point.GetPoint(TextBuffer, View.Caret.Position.Affinity);

        //don't do anything if the current SnapshotPoint is not initialized or at the end of the buffer
        if (!currentCharPoint.HasValue || currentCharPoint.Value.Position >= currentCharPoint.Value.Snapshot.Length) {
            yield break;
        }

        // hold on to a snapshot of the current character
        SnapshotPoint currentChar = currentCharPoint.Value;
           
        //if the requested snapshot isn't the same as the one the brace is on, translate our spans to the expected snapshot
        if (spans[0].Snapshot != currentChar.Snapshot) {
            currentChar = currentChar.TranslateTo(spans[0].Snapshot, PointTrackingMode.Positive);
        }

        var syntaxTreeAndSnapshot = ParserService.SyntaxTreeAndSnapshot;
        if (syntaxTreeAndSnapshot == null || !syntaxTreeAndSnapshot.IsCurrent(currentChar.Snapshot)) {
            yield break;
        }

        // Bewusst der EXAKTE Lookup (FindAtPosition), nicht das owning FindToken: gesucht ist das Zeichen
        // direkt unter/vor dem Caret, nicht das umgebende Konstrukt. An einer Trivia-Position liefert
        // FindAtPosition kein Token (Missing) — sobald die Trivia nicht mehr im flachen Token-Strom liegt. Ein
        // Missing-Token ist weder Klammer noch StringLiteral, daher fallen alle Zweige unten durch (kein
        // Brace-Highlight) — gewünscht.
        var openToken  = syntaxTreeAndSnapshot.SyntaxTree.Tokens.FindAtPosition(currentChar.Position);
        var closeToken = syntaxTreeAndSnapshot.SyntaxTree.Tokens.FindAtPosition(currentChar.Position - 1);

        if (IsOpenBrace(openToken.Type)) {
            var node = openToken.Parent;
            if (node != null) {

                closeToken = node.ChildTokens().FirstOrDefault(GetCloseBraceType(openToken.Type));
                if (!closeToken.IsMissing) {
                    yield return new TagSpan<TextMarkerTag>(new SnapshotSpan(new SnapshotPoint(syntaxTreeAndSnapshot.Snapshot, openToken.Start) , 1), new TextMarkerTag(BraceMatchingTypeNames.BraceMatching));
                    yield return new TagSpan<TextMarkerTag>(new SnapshotSpan(new SnapshotPoint(syntaxTreeAndSnapshot.Snapshot, closeToken.Start), 1), new TextMarkerTag(BraceMatchingTypeNames.BraceMatching));
                }
            }
        } else if (IsCloseBrace(closeToken.Type)) {
            var node = closeToken.Parent;
            if (node != null) {

                openToken = node.ChildTokens().FirstOrDefault(GetOpenBraceType(closeToken.Type));
                if (!openToken.IsMissing) {
                    yield return new TagSpan<TextMarkerTag>(new SnapshotSpan(new SnapshotPoint(syntaxTreeAndSnapshot.Snapshot, openToken.Start) , 1), new TextMarkerTag(BraceMatchingTypeNames.BraceMatching));
                    yield return new TagSpan<TextMarkerTag>(new SnapshotSpan(new SnapshotPoint(syntaxTreeAndSnapshot.Snapshot, closeToken.Start), 1), new TextMarkerTag(BraceMatchingTypeNames.BraceMatching));
                }
            }
        } else if (openToken.Type == SyntaxTokenType.StringLiteral) {
            if (!openToken.IsMissing && currentChar.Position ==openToken.Start) {
                yield return new TagSpan<TextMarkerTag>(new SnapshotSpan(new SnapshotPoint(syntaxTreeAndSnapshot.Snapshot, openToken.Start)  , 1), new TextMarkerTag(BraceMatchingTypeNames.BraceMatching));
                yield return new TagSpan<TextMarkerTag>(new SnapshotSpan(new SnapshotPoint(syntaxTreeAndSnapshot.Snapshot, openToken.End - 1), 1), new TextMarkerTag(BraceMatchingTypeNames.BraceMatching));
            }
        } else if (closeToken.Type == SyntaxTokenType.StringLiteral) {
            if (!closeToken.IsMissing && currentChar.Position == closeToken.End) {
                yield return new TagSpan<TextMarkerTag>(new SnapshotSpan(new SnapshotPoint(syntaxTreeAndSnapshot.Snapshot, closeToken.Start)  , 1), new TextMarkerTag(BraceMatchingTypeNames.BraceMatching));
                yield return new TagSpan<TextMarkerTag>(new SnapshotSpan(new SnapshotPoint(syntaxTreeAndSnapshot.Snapshot, closeToken.End - 1), 1), new TextMarkerTag(BraceMatchingTypeNames.BraceMatching));
            }
        }
    }

    /// <summary>Prüft, ob der Token-Typ eine öffnende Klammer eines bekannten Paars ist.</summary>
    static bool IsOpenBrace(SyntaxTokenType tokenType) {
        return BracePairs.Any(t => t.OpenBrace == tokenType);
    }

    /// <summary>Liefert zur öffnenden Klammer den Typ der zugehörigen schließenden Klammer.</summary>
    static SyntaxTokenType GetCloseBraceType(SyntaxTokenType openBraceType) {
        return BracePairs.Where(bracePair => bracePair.OpenBrace == openBraceType)
                         .Select(bracePair => bracePair.CloseBrace)
                         .FirstOrDefault();
    }

    /// <summary>Prüft, ob der Token-Typ eine schließende Klammer eines bekannten Paars ist.</summary>
    static bool IsCloseBrace(SyntaxTokenType tokentype) {
        return BracePairs.Any(t => t.CloseBrace == tokentype);
    }

    /// <summary>Liefert zur schließenden Klammer den Typ der zugehörigen öffnenden Klammer.</summary>
    static SyntaxTokenType GetOpenBraceType(SyntaxTokenType closeBraceType) {
        return BracePairs.Where(bracePair => bracePair.CloseBrace == closeBraceType)
                         .Select(bracePair => bracePair.OpenBrace)
                         .FirstOrDefault();
    }        
}
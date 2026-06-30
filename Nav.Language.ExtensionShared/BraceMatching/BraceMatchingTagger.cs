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

sealed class BraceMatchingTagger : ParserServiceDependent, ITagger<TextMarkerTag> {

    static readonly List<BracePair<SyntaxTokenType>> BracePairs;

    readonly IDisposable _observable;

    static BraceMatchingTagger() {
        BracePairs = new List<BracePair<SyntaxTokenType>> {
            BracePair.Create(SyntaxTokenType.OpenBracket, SyntaxTokenType.CloseBracket),
            BracePair.Create(SyntaxTokenType.OpenBrace  , SyntaxTokenType.CloseBrace),
            BracePair.Create(SyntaxTokenType.OpenParen  , SyntaxTokenType.CloseParen),
            BracePair.Create(SyntaxTokenType.LessThan   , SyntaxTokenType.GreaterThan)
        };
    }

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
        
    public override void Dispose() {
        base.Dispose();

        _observable.Dispose();

        View.Caret.PositionChanged -= OnCaretPositionChanged;
        View.LayoutChanged         -= OnViewLayoutChanged;
    }
       
    public ITextView View { get; }

    void OnViewLayoutChanged(object sender, TextViewLayoutChangedEventArgs e) {
        Invalidated?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnParseResultChanged(object sender, SnapshotSpanEventArgs e) {
        Invalidated?.Invoke(this, EventArgs.Empty);
    }

    void OnCaretPositionChanged(object sender, CaretPositionChangedEventArgs e) {
        Invalidated?.Invoke(this, EventArgs.Empty);
    }
        
    event EventHandler<EventArgs> Invalidated;

    public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

    void OnTagsChanged() {
        var syntaxTreeAndSnapshot = ParserService.SyntaxTreeAndSnapshot;
        if(syntaxTreeAndSnapshot ==null) {
            return;
        }

        var snapshot     = syntaxTreeAndSnapshot.Snapshot;
        var snapshotSpan = new SnapshotSpan(snapshot, new Span(0, snapshot.Length));

        TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(snapshotSpan));
    }
        
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

    static bool IsOpenBrace(SyntaxTokenType tokenType) {
        return BracePairs.Any(t => t.OpenBrace == tokenType);
    }

    static SyntaxTokenType GetCloseBraceType(SyntaxTokenType openBraceType) {
        return BracePairs.Where(bracePair => bracePair.OpenBrace == openBraceType)
                         .Select(bracePair => bracePair.CloseBrace)
                         .FirstOrDefault();
    }

    static bool IsCloseBrace(SyntaxTokenType tokentype) {
        return BracePairs.Any(t => t.CloseBrace == tokentype);
    }

    static SyntaxTokenType GetOpenBraceType(SyntaxTokenType closeBraceType) {
        return BracePairs.Where(bracePair => bracePair.CloseBrace == closeBraceType)
                         .Select(bracePair => bracePair.OpenBrace)
                         .FirstOrDefault();
    }        
}
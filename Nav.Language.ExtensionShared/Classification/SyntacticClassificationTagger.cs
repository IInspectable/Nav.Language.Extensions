#region Using Directives

using System;
using System.Collections.Generic;
using System.Collections.Immutable;

using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Tagging;

using Pharmatechnik.Nav.Language.Text;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.Classification; 

/// <summary>
/// Syntaktischer (lexikalischer) Klassifizierungs-Tagger: färbt jedes Token des Nav-Syntaxbaums gemäß
/// seiner <see cref="TextClassification"/> ein — ohne Semantikmodell. Neben den signifikanten Token des
/// flachen Stroms werden Kommentare, Präprozessor-Direktiven (strukturierte Trivia) und vom Parser
/// übersprungene Läufe (als Syntaxfehler) berücksichtigt. Als <see cref="ParserServiceDependent"/> reagiert
/// er auf Parse-Ergebnis-Änderungen. Die semantische Einfärbung ergänzt der
/// <see cref="SemanticClassificationTagger"/>; instanziiert wird er über
/// <see cref="SyntacticClassificationTaggerProvider"/>.
/// </summary>
sealed class SyntacticClassificationTagger: ParserServiceDependent, ITagger<IClassificationTag> {

    readonly ImmutableDictionary<TextClassification, IClassificationType> _classificationMap;
    readonly IClassificationType                                          _commentClassification;

    /// <summary>
    /// Baut die Zuordnung der lexikalischen Kategorien auf Klassifizierungstypen
    /// (<see cref="ClassificationTypeDefinitions.GetSyntaxTokenClassificationMap"/>) auf und merkt sich den
    /// Kommentar-Typ gesondert (Kommentare stammen aus der angehängten Trivia).
    /// </summary>
    public SyntacticClassificationTagger(IClassificationTypeRegistryService registry, ITextBuffer textBuffer): base(textBuffer) {

        _classificationMap = ClassificationTypeDefinitions.GetSyntaxTokenClassificationMap(registry);
        _classificationMap.TryGetValue(TextClassification.Comment, out _commentClassification);
    }

    /// <summary>Meldet nach einer Parse-Ergebnis-Änderung den betroffenen Bereich zur Neu-Einfärbung.</summary>
    protected override void OnParseResultChanged(object sender, SnapshotSpanEventArgs e) {
        TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(e.Span));
    }

    /// <summary>Liefert die Klassifizierungs-Tags für die angefragten Bereiche aus dem aktuellen Syntaxbaum.</summary>
    /// <param name="spans">Die vom Editor angefragten Bereiche des aktuellen Snapshots.</param>
    /// <returns>Die Klassifizierungs-Tags der überlappenden Token.</returns>
    public IEnumerable<ITagSpan<IClassificationTag>> GetTags(NormalizedSnapshotSpanCollection spans) {

        var syntaxTreeAndSnapshot = ParserService.SyntaxTreeAndSnapshot;

        if (syntaxTreeAndSnapshot == null) {
            yield break;
        }

        var syntaxTree = syntaxTreeAndSnapshot.SyntaxTree;

        foreach (var span in spans) {

            var extent = TextExtent.FromBounds(span.Start.Position, span.End.Position);

            // Signifikante Token aus dem flachen Strom. Trivia (Whitespace, Zeilenenden, Kommentare) werden
            // hier übersprungen: Kommentare kommen aus der angehängten Trivia (Roslyn-Modell), Whitespace und
            // Zeilenenden tragen ohnehin keine Klassifizierung.
            foreach (var token in syntaxTree.Tokens[extent, includeOverlapping: true]) {

                if (SyntaxFacts.IsTrivia(token.Classification)) {
                    continue;
                }

                if (!_classificationMap.TryGetValue(token.Classification, out var ct) || ct == null) {
                    continue;
                }

                yield return CreateTagSpan(syntaxTreeAndSnapshot.Snapshot, span, token.Start, token.Length, ct);
            }

            // Kommentare aus der angehängten Trivia.
            if (_commentClassification != null) {
                foreach (var comment in syntaxTree.Comments()) {

                    if (comment.Extent.IsEmptyOrMissing || !comment.Extent.IntersectsWith(extent)) {
                        continue;
                    }

                    yield return CreateTagSpan(syntaxTreeAndSnapshot.Snapshot, span, comment.Start, comment.Length, _commentClassification);
                }
            }

            // Präprozessor-Direktiven liegen als strukturierte Trivia vor (nicht im flachen Strom); ihre Token
            // tragen ihre Klassifizierung lokal am Direktiv-Knoten.
            foreach (var directive in syntaxTree.Directives()) {
                foreach (var token in directive.ChildTokens()) {

                    if (token.Extent.IsEmptyOrMissing || !token.Extent.IntersectsWith(extent)) {
                        continue;
                    }

                    if (SyntaxFacts.IsTrivia(token.Classification)) {
                        continue;
                    }

                    if (!_classificationMap.TryGetValue(token.Classification, out var ct) || ct == null) {
                        continue;
                    }

                    yield return CreateTagSpan(syntaxTreeAndSnapshot.Snapshot, span, token.Start, token.Length, ct);
                }
            }

            // Vom Parser übersprungene Läufe liegen ebenfalls als strukturierte Trivia vor; ihre lokalen
            // Token tragen die Skiped-Klassifizierung — sie werden weiterhin als Syntaxfehler eingefärbt.
            foreach (var skipped in syntaxTree.SkippedTokens()) {
                foreach (var token in skipped.ChildTokens()) {

                    if (token.Extent.IsEmptyOrMissing || !token.Extent.IntersectsWith(extent)) {
                        continue;
                    }

                    if (!_classificationMap.TryGetValue(token.Classification, out var ct) || ct == null) {
                        continue;
                    }

                    yield return CreateTagSpan(syntaxTreeAndSnapshot.Snapshot, span, token.Start, token.Length, ct);
                }
            }
        }
    }

    /// <summary>Bildet ein Klassifizierungs-Tag für <paramref name="start"/>/<paramref name="length"/>, übersetzt auf den angefragten Snapshot.</summary>
    static ITagSpan<IClassificationTag> CreateTagSpan(ITextSnapshot snapshot, SnapshotSpan span, int start, int length, IClassificationType classificationType) {
        var tokenSpan = new SnapshotSpan(snapshot, new Span(start, length));
        var tagSpan   = tokenSpan.TranslateTo(span.Snapshot, SpanTrackingMode.EdgeExclusive);
        return new TagSpan<IClassificationTag>(tagSpan, new ClassificationTag(classificationType));
    }

    /// <summary>Wird ausgelöst, wenn sich die Klassifizierung für einen Bereich geändert hat (VS fordert dann neue Tags an).</summary>
    public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

}
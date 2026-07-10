using System;
using System.Collections.Generic;
using System.Diagnostics;

using Pharmatechnik.Nav.Language.Text;

namespace Pharmatechnik.Nav.Language.Formatting;

/// <summary>
/// VS-freier Formatter-Service auf Engine-Ebene — ein reiner <b>Gap-Rewriter</b>: er ändert nie den Text
/// signifikanter Token, sondern schreibt ausschließlich die Whitespace-Lücken <i>zwischen</i>
/// aufeinanderfolgenden signifikanten Token neu. Eingabe ist bewusst der <see cref="SyntaxTree"/>
/// (rein syntaktisches Feature — Token, Trivia und Syntax-Diagnostics hängen dort, kein Semantik-Build
/// nötig); Hosts mit einer <see cref="CodeGenerationUnit"/> reichen deren Syntaxbaum durch.
/// </summary>
/// <remarks>
/// <para><b>Ein-Change-pro-Lücke-Invariante:</b> Die FullSpans der Token kacheln den Text lückenlos und
/// überlappungsfrei; für zwei aufeinanderfolgende Token A, B ist die Lücke exakt
/// <c>[A.Extent.End, B.Extent.Start)</c>. Pro Lücke entsteht höchstens ein <see cref="TextChange"/> in
/// einem einzigen Durchlauf — die Change-Extents sind damit konstruktiv paarweise disjunkt und geordnet
/// (<see cref="TextChangeWriter"/> kann nie eine Überlappung sehen), und das Unterdrücken einer Lücke
/// ist das bloße <i>Weglassen</i> ihres Changes.</para>
/// <para><b>Final-Lücke:</b> Das abschließende <see cref="SyntaxTokenType.EndOfFile"/> trägt die komplette
/// Datei-End-Trivia als Leading-Trivia; die eine verbleibende Lücke <c>[letztes reales Token, EOF)</c>
/// wird ausschließlich von <see cref="RenderFinalGap"/> behandelt (Final-Newline/EOF-Trim) und läuft
/// bewusst <b>nicht</b> zusätzlich durch die Regel-Schleife — zwei Changes für eine Lücke brächen die
/// Invariante.</para>
/// </remarks>
public static class NavFormattingService {

    /// <summary>
    /// Formatiert das ganze Dokument und liefert die minimalen Text-Änderungen (nur Lücken, deren
    /// kanonische Form vom Ist-Text abweicht) — anwendbar z.B. über
    /// <see cref="TextChangeWriter.ApplyTextChanges"/>. Die Changes sind paarweise disjunkt und nach
    /// Position geordnet.
    /// </summary>
    public static IReadOnlyList<TextChange> FormatDocument(SyntaxTree syntaxTree, TextEditorSettings settings, NavFormattingOptions options) {

        if (syntaxTree == null) {
            throw new ArgumentNullException(nameof(syntaxTree));
        }

        if (settings == null) {
            throw new ArgumentNullException(nameof(settings));
        }

        if (options == null) {
            throw new ArgumentNullException(nameof(options));
        }

        var tokens = syntaxTree.Tokens;
        if (tokens.Count == 0) {
            return Array.Empty<TextChange>();
        }

        Debug.Assert(tokens[tokens.Count - 1].Type == SyntaxTokenType.EndOfFile,
                     "Der Lexer terminiert den Token-Strom stets mit dem nullbreiten EndOfFile.");

        var changes   = new List<TextChange>();
        var renderer  = new GapRenderer(syntaxTree.SourceText, settings, options);
        var alignment = AlignmentMapBuilder.Build(syntaxTree, options); // Ausrichtungs-Vorpass: Lücke -> aufgelöste Space-Zahl (block-weit, kanonische Breiten).

        // Datei-Anfang: die Leading-Trivia des ersten realen Tokens liegt vor der ersten Paar-Lücke und
        // wird gesondert normalisiert. Skiped-Läufe (insbesondere ein führendes BOM, das als Unknown ->
        // SkippedTokensTrivia gelext wird) bleiben verbatim — so entsteht nie ein Change an Offset 0,
        // der das BOM anfasste (BOM-Guard).
        var firstToken = tokens[0];
        if (firstToken.Type != SyntaxTokenType.EndOfFile && !HasSkippedTokens(firstToken.LeadingTrivia)) {

            var leadingExtent    = TextExtent.FromBounds(0, firstToken.Start);
            var canonicalLeading = renderer.RenderLeadingGap(firstToken, ComputeIndentDepth(firstToken));

            if (canonicalLeading != syntaxTree.SourceText.Substring(leadingExtent)) {
                changes.Add(TextChange.NewReplace(leadingExtent, canonicalLeading));
            }
        }

        // Alle Paare realer Token — das Paar (letztes reales Token, EOF) ist die Final-Lücke und bleibt
        // ausschließlich RenderFinalGap vorbehalten (siehe Klassen-Doku).
        for (var i = 0; i < tokens.Count - 2; i++) {

            var ctx    = CreateContext(syntaxTree, tokens[i], tokens[i + 1], alignment, options);
            var layout = GapRules.Select(in ctx);

            if (layout is GapLayout.Verbatim) {
                // Unterdrücken = Weglassen des Changes — über disjunkten Lücken nie ein Overlap.
                continue;
            }

            var extent    = ctx.Extent;
            var canonical = renderer.Render(in ctx, layout);

            if (canonical != syntaxTree.SourceText.Substring(extent)) {
                changes.Add(TextChange.NewReplace(extent, canonical));
            }
        }

        var finalChange = RenderFinalGap(syntaxTree, renderer, options);
        if (finalChange != null) {
            changes.Add(finalChange.Value);
        }

        return changes;
    }

    /// <summary>
    /// Behandelt die Final-Lücke zwischen dem letzten realen Token und dem Dateiende — der einzige Ort
    /// für Final-Newline und EOF-Trailing-Trim (Kommentar-/Direktivzeilen am Dateiende bleiben erhalten,
    /// hinter dem letzten Inhalt endet die Datei mit genau einer Newline). Bei
    /// <see cref="NavFormattingOptions.InsertFinalNewline"/> = <c>false</c> bleibt die Lücke verbatim;
    /// Skiped-Läufe bleiben immer verbatim.
    /// </summary>
    static TextChange? RenderFinalGap(SyntaxTree syntaxTree, GapRenderer renderer, NavFormattingOptions options) {

        if (!options.InsertFinalNewline) {
            return null;
        }

        var tokens    = syntaxTree.Tokens;
        var endOfFile = tokens[tokens.Count - 1];
        var lastToken = tokens.Count >= 2 ? tokens[tokens.Count - 2] : (SyntaxToken?) null;

        if (lastToken != null && HasSkippedTokens(lastToken.Value.TrailingTrivia) || HasSkippedTokens(endOfFile.LeadingTrivia)) {
            return null;
        }

        var extent    = TextExtent.FromBounds(lastToken?.End ?? 0, endOfFile.End);
        var canonical = renderer.RenderFinalGap(lastToken, endOfFile);

        return canonical != syntaxTree.SourceText.Substring(extent)
            ? TextChange.NewReplace(extent, canonical)
            : null;
    }

    static bool HasSkippedTokens(SyntaxTriviaList triviaList) {

        foreach (var trivia in triviaList) {
            if (trivia.Type == SyntaxTokenType.SkippedTokensTrivia) {
                return true;
            }
        }

        return false;
    }

    static GapContext CreateContext(SyntaxTree syntaxTree, SyntaxToken prev, SyntaxToken next, AlignmentMap alignment, NavFormattingOptions options) {
        return new GapContext(prev, next,
                              indentDepth: ComputeIndentDepth(next),
                              trivia: GapTrivia.Create(prev, next, syntaxTree.SourceText),
                              isSuppressed: false, // Fehler-Unterdrückung (ComputeSuppressedExtents) ist noch nicht angebunden.
                              alignment: alignment,
                              options: options);
    }

    /// <summary>
    /// Die Einzugstiefe eines Tokens — Nav ist flach (genau ein Block-Typ, keine Verschachtelung),
    /// deshalb wird nicht über Klammern gezählt (bricht bei unbalancierten Eingaben), sondern über
    /// Ahnenkette + Extent-Containment: das Token liegt genau dann im Body eines
    /// <c>task</c>/<c>taskref</c>-Blocks, wenn es hinter dessen realer öffnender Klammer beginnt und vor
    /// der schließenden (Öffnende/schließende Klammer liegen an der Grenze und haben selbst Tiefe 0 —
    /// Allman). Fehlt die schließende Klammer, gilt der Rest als Body — solche Bodies nimmt die
    /// Fehler-Unterdrückung ohnehin von der Formatierung aus.
    /// </summary>
    static int ComputeIndentDepth(SyntaxToken token) {

        if (token.Parent == null) {
            return 0;
        }

        var depth = 0;

        foreach (var node in token.Parent.AncestorsAndSelf()) {

            SyntaxToken openBrace;
            SyntaxToken closeBrace;

            switch (node) {
                case TaskDefinitionSyntax taskDefinition:
                    openBrace  = taskDefinition.OpenBrace;
                    closeBrace = taskDefinition.CloseBrace;
                    break;
                case TaskDeclarationSyntax taskDeclaration:
                    openBrace  = taskDeclaration.OpenBrace;
                    closeBrace = taskDeclaration.CloseBrace;
                    break;
                default:
                    continue;
            }

            if (openBrace.IsMissing) {
                continue;
            }

            if (token.Start >= openBrace.End && (closeBrace.IsMissing || token.Start < closeBrace.Start)) {
                depth++;
            }
        }

        return depth;
    }

}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

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

        var changes     = new List<TextChange>();
        var renderer    = new GapRenderer(syntaxTree.SourceText, settings, options);
        var alignment   = AlignmentMapBuilder.Build(syntaxTree, options); // Ausrichtungs-Vorpass: Lücke -> aufgelöste Space-Zahl (block-weit, kanonische Breiten).
        var suppression = FormatterSuppression.Compute(syntaxTree, options); // Fehler-Toleranz-Vorpass: verbatim vs. hand-gelegt.

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
        // ausschließlich RenderFinalGap vorbehalten (siehe Klassen-Doku). Bei fehlenden brauchbaren
        // Membern (reiner Müll/leer) übersprungen: nur die zwei konservativen Rand-Lücken (Global-Fallback).
        if (suppression.HasUsableMembers) {
            for (var i = 0; i < tokens.Count - 2; i++) {

                var extent = TextExtent.FromBounds(tokens[i].End, tokens[i + 1].Start);

                // Hand-gelegte Anweisung: Inneres verbatim, aber äußerer Einzug per Delta-Shift re-gesetzt.
                if (suppression.TryGetHandLaidShift(extent.Start, out var delta)) {
                    var ctx       = CreateContext(syntaxTree, tokens[i], tokens[i + 1], alignment, suppression, options);
                    var canonical = renderer.RenderRawShifted(in ctx, delta);
                    if (canonical != syntaxTree.SourceText.Substring(extent)) {
                        changes.Add(TextChange.NewReplace(extent, canonical));
                    }

                    continue;
                }

                var context = CreateContext(syntaxTree, tokens[i], tokens[i + 1], alignment, suppression, options);
                var layout  = GapRules.Select(in context);

                if (layout is GapLayout.Verbatim) {
                    // Unterdrücken = Weglassen des Changes — über disjunkten Lücken nie ein Overlap.
                    continue;
                }

                var canonicalGap = renderer.Render(in context, layout);

                if (canonicalGap != syntaxTree.SourceText.Substring(extent)) {
                    changes.Add(TextChange.NewReplace(extent, canonicalGap));
                }
            }
        }

        var finalChange = RenderFinalGap(syntaxTree, renderer, options);
        if (finalChange != null) {
            changes.Add(finalChange.Value);
        }

        return options.VerifyResult ? Guard(syntaxTree, changes) : changes;
    }

    /// <summary>
    /// Formatiert nur die <b>Auswahl</b> <paramref name="range"/> und liefert die Changes, die vollständig
    /// darin liegen. Tragendes Modell: <c>FormatRange</c> ist ein <b>gefiltertes</b> <see cref="FormatDocument"/> —
    /// intern wird immer das ganze Dokument formatiert (alle nicht-lokalen Pässe — Suppression, Ausrichtungs-
    /// Vorpass/<c>targetCol</c>, Einzug — laufen dabei über die <b>volle</b> Datei, nie range-beschränkt),
    /// emittiert werden nur die Changes, deren Extent im (erweiterten) Range liegt:
    /// <c>FormatRange(x, r) ≡ { c ∈ FormatDocument(x) : c.Extent ⊆ ExpandRange(r) }</c>.
    /// </summary>
    /// <remarks>
    /// <para>Daraus folgen gratis die <b>Subset-/Monotonie-Garantien</b>: <c>FormatRange(x, ganzeDatei) ==
    /// FormatDocument(x)</c>, und ein späterer Voll-Format verschiebt nie, was ein Range-Format schon
    /// platziert hat (Range-Format ist eine Teilanwendung desselben Ergebnisses). Weil die Change-Extents pro
    /// Lücke disjunkt sind, ist das Filtern overlap-frei; der Final-Gap unterliegt <b>demselben</b>
    /// <c>⊆</c>-Filter (kein Extra-Schritt — eine Auswahl ohne das Dateiende fügt dort keine Newline ein).</para>
    /// <para>Der Range wird zuvor erweitert (<see cref="ExpandRange"/>): erst auf ganze Zeilen einrasten,
    /// dann auf ganze Anweisungs-/Member-Knoten ausweiten, die er teilweise schneidet — inklusive der
    /// vorangehenden Lücke, die den Einzug des Knotens setzt. Zerschneidet die Auswahl eine
    /// Ausrichtungsgruppe, bleiben Out-of-Range-Nachbarn ggf. ragged (erwartete Editor-Konvention „nur die
    /// Auswahl anfassen", löst sich beim nächsten Voll-Format) — die <c>targetCol</c> ist dank kanonischer
    /// Breitenmessung dennoch identisch zum Voll-Modus.</para>
    /// </remarks>
    public static IReadOnlyList<TextChange> FormatRange(SyntaxTree syntaxTree, TextExtent range, TextEditorSettings settings, NavFormattingOptions options) {

        if (syntaxTree == null) {
            throw new ArgumentNullException(nameof(syntaxTree));
        }

        if (settings == null) {
            throw new ArgumentNullException(nameof(settings));
        }

        if (options == null) {
            throw new ArgumentNullException(nameof(options));
        }

        // Immer das ganze Dokument formatieren (inkl. Laufzeit-Wächter); danach auf den erweiterten Range
        // filtern. So gilt die Subset-Garantie konstruktiv — es gibt keinen range-beschränkten Sonderpfad.
        var documentChanges = FormatDocument(syntaxTree, settings, options);
        if (documentChanges.Count == 0) {
            return documentChanges;
        }

        var expanded = ExpandRange(syntaxTree, range);

        var result = new List<TextChange>();
        foreach (var change in documentChanges) {
            if (expanded.Contains(change.Extent)) {
                result.Add(change);
            }
        }

        return result;
    }

    /// <summary>
    /// Erweitert die rohe Auswahl zum tatsächlich formatierten Bereich: (1) auf ganze Zeilen einrasten,
    /// (2) auf ganze Anweisungs-/Member-Knoten ausweiten, die der zeilen-eingerastete Range teilweise
    /// schneidet — bis zum Knoten-Ende und nach vorn bis zum Ende des vorangehenden signifikanten Tokens.
    /// Diese eine vordere Lücke (Extent <c>[prev.End, first.Start]</c>) ist der einzige Change, der den
    /// Einzug des Knotens setzt (ein Change pro Lücke); ohne sie bliebe der Einzug der selektierten
    /// Anweisung unkorrigiert und eine mehrzeilige Anweisung (hand-gelegt, mehrzeiliges <c>[params]</c>)
    /// würde nur halb formatiert.
    /// </summary>
    static TextExtent ExpandRange(SyntaxTree syntaxTree, TextExtent range) {

        var sourceText = syntaxTree.SourceText;
        var length     = sourceText.Length;

        // Rohe Auswahl in das Dokument klemmen (ein Host könnte eine veraltete/zu große Auswahl liefern).
        var start = Math.Max(0, Math.Min(range.Start, length));
        var end   = Math.Max(start, Math.Min(range.End, length));

        // (1) Auf ganze Zeilen einrasten.
        start = sourceText.GetTextLineAtPosition(start).Start;

        var endLine = sourceText.GetTextLineAtPosition(end);
        // Endet die Auswahl am Zeilenanfang (Spalte 0) einer weiteren Zeile, gehört diese Zeile nicht mehr
        // dazu — der letzte ausgewählte Inhalt endet am Ende der Vorzeile (== endLine.Start).
        if (end <= start || end != endLine.Start) {
            end = endLine.End;
        }

        // (2) Auf ganze Anweisungs-/Member-Knoten ausweiten, die der (fixe) zeilen-eingerastete Range echt
        //     schneidet (Adjazenz zählt nicht). Der Test läuft gegen den FullExtent (inkl. Leading-Trivia).
        var lineRange = TextExtent.FromBounds(start, end);
        foreach (var node in FormattableNodes(syntaxTree)) {

            var full = node.FullExtent;
            if (full.Start >= lineRange.End || full.End <= lineRange.Start) {
                continue;
            }

            start = Math.Min(start, LeadingGapStart(syntaxTree, node));
            end   = Math.Max(end, node.End);
        }

        return TextExtent.FromBounds(start, end);
    }

    /// <summary>
    /// Der Startpunkt der Lücke <b>vor</b> dem ersten Token eines Knotens — das Ende des unmittelbar
    /// vorangehenden signifikanten Tokens (oder 0 am Datei-Anfang). Genau diese Lücke trägt den Change, der
    /// den Einzug des Knotens setzt; der Range muss bis hierher zurückreichen, damit dieser Change beim
    /// <c>⊆</c>-Filter erhalten bleibt.
    /// </summary>
    static int LeadingGapStart(SyntaxTree syntaxTree, SyntaxNode node) {

        var firstToken = syntaxTree.Tokens.FindAtPosition(node.Start);
        var fullStart  = firstToken.IsMissing ? node.Start : firstToken.FullExtent.Start;

        if (fullStart <= 0) {
            return 0;
        }

        var previous = syntaxTree.Tokens.FindOwningToken(fullStart - 1);
        return previous.IsMissing ? 0 : previous.End;
    }

    /// <summary>
    /// Die für die Range-Ausweitung maßgeblichen Knoten: die Anweisungen (Transitionen, Exit-Transitionen,
    /// Node-Deklarationen) — dieselbe statement-/member-granulare Einheit wie
    /// <see cref="FormatterSuppression"/> — plus das <c>[params]</c> des Task-Kopfes
    /// (<see cref="CodeParamsDeclarationSyntax"/>, das nicht in einer Node-Deklaration steckt), damit ein
    /// mehrzeiliges Task-Kopf-<c>[params]</c> nicht halb formatiert wird, ohne den ganzen Task einzubeziehen.
    /// </summary>
    static IEnumerable<SyntaxNode> FormattableNodes(SyntaxTree syntaxTree) {

        var root = syntaxTree.Root;

        foreach (var node in root.DescendantNodes<TransitionDefinitionSyntax>()) {
            yield return node;
        }

        foreach (var node in root.DescendantNodes<ExitTransitionDefinitionSyntax>()) {
            yield return node;
        }

        foreach (var node in root.DescendantNodes<NodeDeclarationSyntax>()) {
            yield return node;
        }

        foreach (var node in root.DescendantNodes<CodeParamsDeclarationSyntax>()) {
            yield return node;
        }
    }

    /// <summary>
    /// Behandelt die Final-Lücke zwischen dem letzten realen Token und dem Dateiende — der einzige Ort
    /// für Final-Newline und EOF-Trailing-Trim (Kommentar-/Direktivzeilen am Dateiende bleiben erhalten,
    /// hinter dem letzten Inhalt endet die Datei mit genau einer Newline). EOF-Trim und die Normalisierung
    /// der Kommentar-/Direktivzeilen laufen immer; <see cref="NavFormattingOptions.InsertFinalNewline"/>
    /// steuert nur, ob hinter dem letzten Inhalt die abschließende Newline ergänzt wird. Skiped-Läufe
    /// bleiben immer verbatim.
    /// </summary>
    static TextChange? RenderFinalGap(SyntaxTree syntaxTree, GapRenderer renderer, NavFormattingOptions options) {

        var tokens    = syntaxTree.Tokens;
        var endOfFile = tokens[tokens.Count - 1];
        var lastToken = tokens.Count >= 2 ? tokens[tokens.Count - 2] : (SyntaxToken?) null;

        if (lastToken != null && HasSkippedTokens(lastToken.Value.TrailingTrivia) || HasSkippedTokens(endOfFile.LeadingTrivia)) {
            return null;
        }

        var extent    = TextExtent.FromBounds(lastToken?.End ?? 0, endOfFile.End);
        var canonical = renderer.RenderFinalGap(lastToken, endOfFile, options.InsertFinalNewline);

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

    static GapContext CreateContext(SyntaxTree syntaxTree, SyntaxToken prev, SyntaxToken next,
                                    AlignmentMap alignment, FormatterSuppression suppression, NavFormattingOptions options) {

        var extent = TextExtent.FromBounds(prev.End, next.Start);

        return new GapContext(prev, next,
                              indentDepth: ComputeIndentDepth(next),
                              trivia: GapTrivia.Create(prev, next, syntaxTree.SourceText),
                              isSuppressed: suppression.IsSuppressed(extent),
                              alignment: alignment,
                              options: options);
    }

    /// <summary>
    /// Achse-A-Selbsttest (fail-safe): Da der Formatter nie signifikanten Token-Text anfasst, muss
    /// <c>format(x)</c> zum identischen signifikanten Token-Strom (Typ + Text) zurück-parsen, mit
    /// identischer Direktiv-Sequenz und ohne neue Error-Diagnostics. Weicht das ab, ist das <b>immer ein
    /// Bug</b> (kein legitimer Laufzustand) — hart <see cref="Debug.Fail(string)"/>; zusätzlich werden die
    /// Changes verworfen (Eingabe bleibt unverändert) und einmalig auf <c>stderr</c> geloggt (konform zur
    /// Stdio-Log-Regel). Läuft nur bei <see cref="NavFormattingOptions.VerifyResult"/> mit — das setzen
    /// ausschließlich die Tests, da der Re-Parse Parse + Apply grob verdoppelt und die ausgelieferten
    /// (Debug-)Hosts diese Kosten nicht tragen sollen.
    /// </summary>
    static IReadOnlyList<TextChange> Guard(SyntaxTree syntaxTree, IReadOnlyList<TextChange> changes) {

        if (changes.Count == 0) {
            return changes;
        }

        var formatted = new TextChangeWriter().ApplyTextChanges(syntaxTree.SourceText.Text, changes);
        var after     = SyntaxTree.ParseText(formatted);

        if (MeaningPreserved(syntaxTree, after)) {
            return changes;
        }

        Debug.Fail("Formatter-Wächter: Formatierung würde den signifikanten Token-Strom, die Direktiven " +
                   "oder die Diagnostics verändern (Achse-A-Bruch).");

        Console.Error.WriteLine("nav-format: interner Wächter hat eine bedeutungsverändernde Formatierung " +
                                "erkannt und verworfen.");

        return Array.Empty<TextChange>();
    }

    static bool MeaningPreserved(SyntaxTree before, SyntaxTree after) {
        return SignificantTokens(before).SequenceEqual(SignificantTokens(after)) &&
               Directives(before).SequenceEqual(Directives(after)) &&
               ErrorCount(after) <= ErrorCount(before);
    }

    static IEnumerable<(SyntaxTokenType Type, string Text)> SignificantTokens(SyntaxTree syntaxTree) {
        return syntaxTree.Tokens
                         .Where(token => token.Type != SyntaxTokenType.EndOfFile)
                         .Select(token => (token.Type, token.ToString()));
    }

    static IEnumerable<string> Directives(SyntaxTree syntaxTree) {
        return syntaxTree.Directives().Select(directive => directive.ToString());
    }

    static int ErrorCount(SyntaxTree syntaxTree) {
        return syntaxTree.Diagnostics.Count(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
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
    internal static int ComputeIndentDepth(SyntaxToken token) {

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

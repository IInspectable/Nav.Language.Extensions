using System.Collections.Generic;
using System.Collections.Immutable;

using Pharmatechnik.Nav.Language.Internal;
using Pharmatechnik.Nav.Language.Text;

namespace Pharmatechnik.Nav.Language;

/// <summary>
/// Sub-Parser für die Präprozessor-Direktiven eines Nav-Quelltexts. Er läuft über den flachen Roh-Token-Strom
/// des <see cref="NavLexer"/> und erkennt jeden <c>#</c>-Lauf (der Lexer hat die Zeilenanfang-Regel bereits
/// erzwungen) als eigenständigen <see cref="DirectiveTriviaSyntax"/>-Knoten samt lokalen Token. Das Ergebnis
/// ist eine Liste von <see cref="DirectiveRun"/> in Quelltext-Reihenfolge, die <see cref="NavParser.BuildTrivia"/>
/// zu strukturierter <see cref="SyntaxTokenType.DirectiveTrivia"/> faltet.
/// <para/>
/// Bewusst getrennt vom <see cref="NavParser"/>: der Sub-Parser kennt <b>nur</b> die generische Direktiv-Syntax
/// (Keyword-Dispatch) und keine Platzierungs-Semantik. Ob eine Versions-Direktive <i>wirksam</i> ist (ganz oben,
/// nicht doppelt), entscheidet ein nachgelagerter Schritt im <see cref="NavParser"/> aus den erzeugten Läufen.
/// <para/>
/// Der Cursor bewegt sich innerhalb genau eines <c>#</c>-Laufs <c>[hashIndex, _end)</c>. Er kennt bewusst
/// <b>keine</b> Insertion-/Recovery-Mechanik wie der Hauptparser — ein Direktiv-Lauf ist stets vollständig
/// vom Lexer abgegrenzt.
/// </summary>
sealed class NavDirectiveParser {

    readonly SourceText                         _sourceText;
    readonly ImmutableArray<RawToken>           _raw;
    readonly ImmutableArray<Diagnostic>.Builder _diagnostics;

    // Cursor über genau einen #-Lauf [hashIndex, _end): _pos zeigt auf das aktuelle Roh-Token des Laufs,
    // _end (exklusiv) begrenzt ihn. Wird je Lauf neu gesetzt; zwischen den Läufen scannt Parse() selbst.
    int _pos;
    int _end;

    NavDirectiveParser(ImmutableArray<RawToken> raw, SourceText sourceText, ImmutableArray<Diagnostic>.Builder diagnostics) {
        _raw         = raw;
        _sourceText  = sourceText;
        _diagnostics = diagnostics;
    }

    /// <summary>
    /// Parst alle Präprozessor-Direktiven des Roh-Token-Stroms und liefert je <c>#</c>-Lauf einen
    /// <see cref="DirectiveRun"/> in Quelltext-Reihenfolge. Jeder <c>#</c> (den der Lexer an einem
    /// Zeilenanfang erkannt hat) beginnt einen Lauf; die dazwischen liegenden signifikanten Token und
    /// Trivia interessieren den Sub-Parser nicht.
    /// </summary>
    public List<DirectiveRun> Parse() {

        var runs = new List<DirectiveRun>();

        for (var i = 0; i < _raw.Length; i++) {
            if (_raw[i].Type != SyntaxTokenType.HashToken) {
                continue;
            }

            var end  = RunEnd(i);
            var node = ParseDirective(i, end);
            runs.Add(MakeRun(i, end, node));

            i = end - 1; // Die Schleife rückt über den ganzen Direktiv-Lauf hinweg.
        }

        return runs;
    }

    /// <summary>
    /// Erkennt aus dem Direktiv-Lauf <c>[hashIndex, end)</c> anhand des Schlüsselworts unmittelbar hinter dem
    /// <c>#</c> den passenden Knoten. Der Keyword-Dispatch ist der Erweiterungspunkt für spätere Direktiven
    /// (<c>#if</c>, <c>#region</c>, …). Ein unbekanntes oder fehlendes Schlüsselwort ergibt eine
    /// <see cref="BadDirectiveTriviaSyntax"/> samt <c>Nav3000</c>.
    /// </summary>
    DirectiveTriviaSyntax ParseDirective(int hashIndex, int end) {

        _pos = hashIndex;
        _end = end;

        TryEat(SyntaxTokenType.HashToken, out _);

        // Direktiven-Schlüsselwort: ein reines Wort-Token unmittelbar hinter dem '#'.
        var keyword = At(SyntaxTokenType.PreprocessorKeyword) ? _sourceText.Substring(Current.Extent) : null;

        return keyword switch {
            "pragma" => ParsePragma(hashIndex, end),
            _        => BadDirective(hashIndex, end),
        };
    }

    /// <summary>
    /// Parst einen <c>#pragma</c>-Lauf. Subjekt ist das erste Wort-/Zahl-Token hinter <c>pragma</c>; zwischen
    /// beiden darf nur Zwischenraum stehen. Ein Subjekt <c>version</c> ergibt <b>immer</b> eine
    /// <see cref="VersionDirectiveSyntax"/> (die <i>Wirksamkeit</i> entscheidet der <see cref="NavParser"/>
    /// nachgelagert), jedes andere oder fehlende Subjekt (<c>#pragma warning</c> o.Ä.) eine
    /// <see cref="BadDirectiveTriviaSyntax"/> samt <c>Nav3000</c>.
    /// </summary>
    DirectiveTriviaSyntax ParsePragma(int hashIndex, int end) {

        TryEat(SyntaxTokenType.PreprocessorKeyword, out var pragma);

        // Zwischenraum bis zum Subjekt überspringen; das Subjekt ist das erste Wort-/Zahl-Token danach.
        SkipPreprocessorText();

        // Ein Subjekt "version" — und zwischen "pragma" und ihm ausschließlich Zwischenraum (ein
        // '#pragma .version' o.Ä. ist keine Versions-Direktive) — macht die Direktive zur Versions-Direktive.
        if (At(SyntaxTokenType.PreprocessorKeyword)                                                             &&
            _sourceText.Substring(Current.Extent) == "version"                                                 &&
            string.IsNullOrWhiteSpace(_sourceText.Substring(TextExtent.FromBounds(pragma.Extent.End, Current.Extent.Start)))) {
            return AcceptVersion(hashIndex, end, Current);
        }

        return BadDirective(hashIndex, end);
    }

    /// <summary>
    /// Baut aus dem als Versions-Direktive erkannten Lauf <c>[hashIndex, end)</c> die
    /// <see cref="VersionDirectiveSyntax"/> samt lokalen Token. Das Argument hinter <paramref name="subject"/>
    /// (<c>version</c>) validiert <see cref="NavLanguageVersion.TryParse"/> — reine Ziffern nach Trim; ein
    /// fehlender Wert, ein Vorzeichen oder mehrere Werte lösen genau eine <c>Nav3002</c> samt Rückfall auf
    /// <see cref="NavLanguageVersion.Default"/> aus.
    /// </summary>
    VersionDirectiveSyntax AcceptVersion(int hashIndex, int end, RawToken subject) {

        var last         = _raw[end - 1];
        var newLineStart = last.Type == SyntaxTokenType.PreprocessorNewLine ? last.Extent.Start : last.Extent.End;
        var argument     = _sourceText.Substring(TextExtent.FromBounds(subject.Extent.End, newLineStart));
        if (!NavLanguageVersion.TryParse(argument, out var version)) {
            _diagnostics.Add(new Diagnostic(DirectiveLocation(hashIndex, end),
                                            DiagnosticDescriptors.Syntax.Nav3002InvalidPragmaVersion));
            version = NavLanguageVersion.Default;
        }

        var node = new VersionDirectiveSyntax(DirectiveExtent(hashIndex, end), version);
        PopulateLocalTokens(node, hashIndex, end);
        return node;
    }

    /// <summary>
    /// Baut aus dem Lauf <c>[hashIndex, end)</c> eine wirkungslose <see cref="BadDirectiveTriviaSyntax"/>
    /// (unbekannte Direktive) samt lokalen Token und meldet <c>Nav3000</c> über die ganze Direktiv-Breite.
    /// Die Zeilenanfang-Regel erzwingt bereits der Lexer (ein <c>#</c> mitten in der Zeile ist keine
    /// Direktive, sondern ein unbekanntes Zeichen).
    /// </summary>
    BadDirectiveTriviaSyntax BadDirective(int hashIndex, int end) {

        _diagnostics.Add(new Diagnostic(DirectiveLocation(hashIndex, end),
                                        DiagnosticDescriptors.Syntax.Nav3000InvalidPreprocessorDirective));

        var node = new BadDirectiveTriviaSyntax(DirectiveExtent(hashIndex, end));
        PopulateLocalTokens(node, hashIndex, end);
        return node;
    }

    /// <summary>
    /// Bündelt den Direktiv-Lauf <c>[hashIndex, end)</c> zu einem <see cref="DirectiveRun"/>: sein Roh-Index-
    /// Bereich (für <see cref="NavParser.BuildTrivia"/>), sein Inhalts-Extent (die <c>DirectiveTrivia</c>-Breite,
    /// ohne Zeilenende), sein terminierendes Zeilenende (sofern vorhanden) und der zugehörige Knoten.
    /// </summary>
    DirectiveRun MakeRun(int hashIndex, int end, DirectiveTriviaSyntax node) {
        var last          = _raw[end - 1];
        var hasNewLine    = last.Type == SyntaxTokenType.PreprocessorNewLine;
        var newLineExtent = hasNewLine ? last.Extent : TextExtent.Missing;
        return new DirectiveRun(hashIndex, end, DirectiveExtent(hashIndex, end), newLineExtent, node);
    }

    /// <summary>
    /// Extent des Direktiv-Laufs <c>[hashIndex, end)</c> für Diagnose-Zwecke: vom einleitenden <c>#</c> bis
    /// zum Ende des letzten Inhalts-Tokens, <b>ohne</b> das abschließende Zeilenende. So markiert die
    /// Squiggle die ganze Direktive statt nur das <c>#</c>.
    /// </summary>
    TextExtent DirectiveExtent(int hashIndex, int end) {
        var last       = _raw[end - 1];
        var contentEnd = last.Type == SyntaxTokenType.PreprocessorNewLine ? last.Extent.Start : last.Extent.End;
        return TextExtent.FromBounds(_raw[hashIndex].Extent.Start, contentEnd);
    }

    /// <summary>
    /// Location einer Direktiv-Diagnose über die ganze Direktiv-Breite <c>[hashIndex, end)</c>. Sie trägt —
    /// via <see cref="SourceText.GetLocation"/> — eine echte Start-/End-Zeilenposition; nur so ziehen
    /// LSP-Clients (VS Code) die Squiggle über die volle Breite; VS rendert bereits aus dem Extent.
    /// </summary>
    Location DirectiveLocation(int hashIndex, int end) => _sourceText.GetLocation(DirectiveExtent(hashIndex, end));

    /// <summary>
    /// Erzeugt die lokalen Token des Direktiv-Laufs <c>[hashIndex, end)</c> und legt sie am Knoten
    /// <paramref name="node"/> ab (siehe <see cref="DirectiveTriviaSyntax.SetLocalTokens"/>) — mit der aus dem
    /// Token-Typ folgenden <see cref="TextClassification"/>. Das terminierende <c>PreprocessorNewLine</c> zählt
    /// nicht zu den Token; es wird in <see cref="NavParser.BuildTrivia"/> als eigenes
    /// <see cref="SyntaxTokenType.NewLine"/> geführt. Die Token liegen ausschließlich lokal, nicht im flachen
    /// <see cref="SyntaxTree.Tokens"/>-Strom.
    /// </summary>
    void PopulateLocalTokens(DirectiveTriviaSyntax node, int hashIndex, int end) {

        var tokenEnd = _raw[end - 1].Type == SyntaxTokenType.PreprocessorNewLine ? end - 1 : end;

        var localTokens = new List<SyntaxToken>();
        for (var k = hashIndex; k < tokenEnd; k++) {
            var raw = _raw[k];
            if (SyntaxTokenFactory.TryClassifyNonSignificant(raw.Type, out var classification)) {
                localTokens.Add(SyntaxTokenFactory.CreateToken(raw.Extent, raw.Type, classification, node,
                                                               SyntaxTriviaList.Empty, SyntaxTriviaList.Empty));
            }
        }

        node.SetLocalTokens(new SyntaxTokenList(localTokens));
    }

    // -- Cursor über einen #-Lauf [_pos, _end) --------------------------------------------------------------

    /// <summary>Typ des aktuellen Roh-Tokens im Lauf; <see cref="SyntaxTokenType.EndOfFile"/> am Lauf-Ende.</summary>
    SyntaxTokenType At0 => _pos < _end ? _raw[_pos].Type : SyntaxTokenType.EndOfFile;

    /// <summary>Ob das aktuelle Token vom Typ <paramref name="type"/> ist.</summary>
    bool At(SyntaxTokenType type) => At0 == type;

    /// <summary>Ob der Cursor das Ende des Laufs erreicht hat.</summary>
    bool AtRunEnd => _pos >= _end;

    /// <summary>Das aktuelle Roh-Token des Laufs (nur gültig, solange <see cref="AtRunEnd"/> falsch ist).</summary>
    RawToken Current => _raw[_pos];

    /// <summary>
    /// Konsumiert das aktuelle Token, wenn es <paramref name="type"/> entspricht, gibt es über
    /// <paramref name="token"/> zurück und rückt vor (Ergebnis <c>true</c>); andernfalls bleibt der Cursor
    /// stehen (Ergebnis <c>false</c>) — <b>ohne</b> Diagnose. Ein Direktiv-Lauf ist stets vollständig; es gibt
    /// hier kein Missing-Token wie beim Hauptparser.
    /// </summary>
    bool TryEat(SyntaxTokenType type, out RawToken token) {
        if (AtRunEnd || _raw[_pos].Type != type) {
            token = default;
            return false;
        }

        token = _raw[_pos];
        _pos++;
        return true;
    }

    /// <summary>Überspringt zusammenhängende <see cref="SyntaxTokenType.PreprocessorText"/> (Zwischenraum/Satzzeichen).</summary>
    void SkipPreprocessorText() {
        while (!AtRunEnd && _raw[_pos].Type == SyntaxTokenType.PreprocessorText) {
            _pos++;
        }
    }

    /// <summary>
    /// Ende (exklusiv) des Direktiv-Laufs, der beim <c>#</c> an <paramref name="hashIndex"/> beginnt: die
    /// unmittelbar folgenden Präprozessor-Token bis einschließlich des abschließenden PreprocessorNewLine.
    /// </summary>
    int RunEnd(int hashIndex) {
        var end = hashIndex + 1;
        while (end < _raw.Length) {
            var type = _raw[end].Type;
            if (type is SyntaxTokenType.PreprocessorKeyword or SyntaxTokenType.PreprocessorText or SyntaxTokenType.PreprocessorNumber) {
                end++;
                continue;
            }

            if (type == SyntaxTokenType.PreprocessorNewLine) {
                end++;
            }

            break;
        }

        return end;
    }

}
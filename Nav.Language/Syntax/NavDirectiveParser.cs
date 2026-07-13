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

    public NavDirectiveParser(ImmutableArray<RawToken> raw, SourceText sourceText, ImmutableArray<Diagnostic>.Builder diagnostics) {
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
    /// Erkennt aus dem Direktiv-Lauf <c>[hashIndex, end)</c> anhand des Schlüsselwort-Tokens unmittelbar
    /// hinter dem <c>#</c> den passenden Knoten. Der Lexer hat die Direktiv-Schlüsselwörter bereits als
    /// eigene Token-Typen erkannt (<see cref="SyntaxTokenType.VersionKeyword"/>, <see cref="SyntaxTokenType.PragmaKeyword"/>);
    /// der Dispatch läuft daher über die Token-Art, nicht über einen Textvergleich. <c>#version</c> ergibt die
    /// <see cref="VersionDirectiveSyntax"/>. Der Keyword-Dispatch ist der Erweiterungspunkt für spätere Direktiven
    /// (<c>#if</c>, <c>#region</c>, …). Ein unbekanntes oder fehlendes Schlüsselwort ergibt eine
    /// <see cref="BadDirectiveTriviaSyntax"/> samt <c>Nav3000</c>.
    /// </summary>
    DirectiveTriviaSyntax ParseDirective(int hashIndex, int end) {

        _pos = hashIndex;
        _end = end;

        TryEat(SyntaxTokenType.HashToken, out _);

        // Direktiven-Schlüsselwort: ein eigenes Keyword-Token unmittelbar hinter dem '#'.
        if (At(SyntaxTokenType.VersionKeyword)) {
            return ParseVersion(hashIndex, end);
        }

        if (At(SyntaxTokenType.PragmaKeyword)) {
            return ParsePragma(hashIndex, end);
        }

        return BadDirective(hashIndex, end);
    }

    /// <summary>
    /// Parst einen <c>#pragma</c>-Lauf. Es gibt derzeit <b>keine</b> bekannten Pragmas — die Versionsdirektive
    /// ist mit <c>#version</c> eine eigene Direktive und kein Pragma-Subjekt mehr. Ein Subjekt hinter
    /// <c>pragma</c> (das erste Wort-/Zahl-Token) ergibt daher eine wirkungslose <see cref="BadDirectiveTriviaSyntax"/>
    /// samt <c>Nav3001</c> („Unknown pragma"); fehlt das Subjekt ganz (<c>#pragma</c> allein), bleibt es die
    /// generische unbekannte Direktive (<c>Nav3000</c>). Der <c>#pragma</c>-Zweig bleibt als Erweiterungspunkt
    /// für spätere Pragma-Subjekte erhalten.
    /// </summary>
    DirectiveTriviaSyntax ParsePragma(int hashIndex, int end) {

        TryEat(SyntaxTokenType.PragmaKeyword, out _);

        // Zwischenraum bis zum Subjekt überspringen; das Subjekt ist das erste signifikante Token danach.
        SkipPreprocessorText();

        // Ein Subjekt hinter "pragma" ist ein (derzeit stets unbekanntes) Pragma; sein Text speist die
        // Nav3001-Meldung. Ohne Subjekt ist der '#pragma'-Lauf selbst die unbekannte Direktive.
        if (!AtRunEnd && !At(SyntaxTokenType.PreprocessorNewLine)) {
            return UnknownPragma(hashIndex, end, _sourceText.Substring(Current.Extent));
        }

        return BadDirective(hashIndex, end);
    }

    /// <summary>
    /// Baut aus dem als Versions-Direktive erkannten Lauf <c>[hashIndex, end)</c> die
    /// <see cref="VersionDirectiveSyntax"/> samt lokalen Token. Das Argument ist genau <b>ein</b>
    /// <see cref="SyntaxTokenType.PreprocessorNumber"/>-Token (vom Lexer erkannt), dem bis zum Zeilenende nur
    /// Zwischenraum folgen darf; <see cref="NavLanguageVersion.TryParse"/> validiert seinen Wert (Ziffern,
    /// kein Überlauf). Jede Abweichung meldet genau eine <c>Nav3002</c> — positions-präzise:
    /// <list type="bullet">
    ///   <item><description>fehlender Wert ⇒ nullbreit hinter dem <c>version</c>-Schlüsselwort (Insertion-Punkt),
    ///   Rückfall auf <see cref="NavLanguageVersion.Default"/>;</description></item>
    ///   <item><description>Nicht-Zahl bzw. ungültiger Wert ⇒ über den Wert selbst, Rückfall auf
    ///   <see cref="NavLanguageVersion.Default"/>;</description></item>
    ///   <item><description>gültige Zahl mit überzähligem Rest (<c>#version 1 2</c>) ⇒ die Zahl
    ///   <b>gilt</b>, der Rest wird als <see cref="TextClassification.Skiped"/> ausgegraut und über seine
    ///   Spanne gemeldet.</description></item>
    /// </list>
    /// Die Methode ist bewusst dispatch-agnostisch: sie frisst das <c>version</c>-Schlüsselwort und das
    /// Argument, ohne vorauszusetzen, wie der Dispatch dorthin fand.
    /// </summary>
    VersionDirectiveSyntax ParseVersion(int hashIndex, int end) {

        TryEat(SyntaxTokenType.VersionKeyword, out _);
        SkipPreprocessorText();

        var version  = NavLanguageVersion.Default;
        int skipFrom;

        if (TryEat(SyntaxTokenType.PreprocessorNumber, out var number)) {
            if (NavLanguageVersion.TryParse(_sourceText.Substring(number.Extent), out version)) {
                // Gültige Zahl gelesen — alles Weitere bis zum Lauf-Ende ist überzählig.
                SkipPreprocessorText();
                skipFrom = _pos;
                if (!AtRunEnd && !At(SyntaxTokenType.PreprocessorNewLine)) {
                    ReportNav3002(TokensExtent(skipFrom, end));
                }
            } else {
                // Zahl-Token, aber ungültiger Wert (z.B. Überlauf) — die Zahl bleibt gefärbt, Wert = Default.
                version  = NavLanguageVersion.Default;
                skipFrom = _pos;
                ReportNav3002(number.Extent);
            }
        } else {
            // Wert fehlt oder ist keine Zahl: eine Diagnose, der Wert-Slot wird ausgegraut.
            skipFrom = _pos;
            ReportNav3002(AtRunEnd || At(SyntaxTokenType.PreprocessorNewLine)
                              ? TextExtent.FromBounds(InsertionPoint(), InsertionPoint())
                              : TokensExtent(skipFrom, end));
        }

        var node = new VersionDirectiveSyntax(DirectiveExtent(hashIndex, end), version);
        PopulateLocalTokens(node, hashIndex, end, skipFrom);
        return node;
    }

    /// <summary>Meldet eine <c>Nav3002</c> (fehlerhafte Versions-Direktive) über <paramref name="extent"/>.</summary>
    void ReportNav3002(TextExtent extent) {
        _diagnostics.Add(new Diagnostic(_sourceText.GetLocation(extent),
                                        DiagnosticDescriptors.Syntax.Nav3002InvalidVersionDirective));
    }

    /// <summary>
    /// Baut aus dem Lauf <c>[hashIndex, end)</c> eine wirkungslose <see cref="BadDirectiveTriviaSyntax"/> für ein
    /// unbekanntes Pragma und meldet <c>Nav3001</c> („Unknown pragma '<paramref name="pragmaName"/>'") über die
    /// ganze Direktiv-Breite. Es gibt derzeit keine bekannten Pragmas; der Zweig ist der Erweiterungspunkt für
    /// spätere <c>#pragma</c>-Subjekte.
    /// </summary>
    BadDirectiveTriviaSyntax UnknownPragma(int hashIndex, int end, string pragmaName) {

        _diagnostics.Add(new Diagnostic(DirectiveLocation(hashIndex, end),
                                        DiagnosticDescriptors.Syntax.Nav3001UnknownPragma,
                                        pragmaName));

        var node = new BadDirectiveTriviaSyntax(DirectiveExtent(hashIndex, end));
        PopulateLocalTokens(node, hashIndex, end, end);
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
        PopulateLocalTokens(node, hashIndex, end, end);
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
    /// Extent der Roh-Token <c>[from, end)</c> für eine positions-präzise Diagnose: vom Anfang des Tokens an
    /// <paramref name="from"/> bis zum Ende des letzten Inhalts-Tokens des Laufs, <b>ohne</b> das abschließende
    /// Zeilenende. Aufrufer stellen sicher, dass <paramref name="from"/> auf ein Inhalts-Token zeigt.
    /// </summary>
    TextExtent TokensExtent(int from, int end) {
        var last       = _raw[end - 1];
        var contentEnd = last.Type == SyntaxTokenType.PreprocessorNewLine ? last.Extent.Start : last.Extent.End;
        return TextExtent.FromBounds(_raw[from].Extent.Start, contentEnd);
    }

    /// <summary>
    /// Position eines nullbreiten „fehlt"-Vermerks: das Ende des letzten konsumierten <b>signifikanten</b>
    /// Tokens vor dem Cursor (Zwischenraum übersprungen) — nach Roslyn-Konvention hängt „X erwartet" am
    /// vorigen Token, nicht am Anfang des folgenden. Für die Versions-Direktive ist das das Ende des
    /// <c>version</c>-Schlüsselworts.
    /// </summary>
    int InsertionPoint() {
        for (var k = _pos - 1; k >= 0; k--) {
            if (_raw[k].Type is not (SyntaxTokenType.PreprocessorText or SyntaxTokenType.PreprocessorNewLine)) {
                return _raw[k].Extent.End;
            }
        }

        return _raw[_pos < _end ? _pos : _end - 1].Extent.Start;
    }

    /// <summary>
    /// Erzeugt die lokalen Token des Direktiv-Laufs <c>[hashIndex, end)</c> und legt sie am Knoten
    /// <paramref name="node"/> ab (siehe <see cref="StructuredTriviaSyntax.SetLocalTokens"/>) — mit der aus dem
    /// Token-Typ folgenden <see cref="TextClassification"/>. Token ab <paramref name="skipFrom"/> (dem
    /// grammatikalisch überzähligen Rest, den der Parser nicht mehr unterbringen konnte) werden stattdessen
    /// als <see cref="TextClassification.Skiped"/> ausgegraut — analog zu den Panic-Mode-Token des
    /// Hauptparsers: ein Token, das zu keinem Bestandteil der Direktive gehört, soll nicht wie ein gültiger
    /// Wert aussehen. Das terminierende <c>PreprocessorNewLine</c> zählt nicht zu den Token; es wird in
    /// <see cref="NavParser.BuildTrivia"/> als eigenes <see cref="SyntaxTokenType.NewLine"/> geführt. Die
    /// Token liegen ausschließlich lokal, nicht im flachen <see cref="SyntaxTree.Tokens"/>-Strom.
    /// </summary>
    void PopulateLocalTokens(DirectiveTriviaSyntax node, int hashIndex, int end, int skipFrom) {

        var tokenEnd = _raw[end - 1].Type == SyntaxTokenType.PreprocessorNewLine ? end - 1 : end;

        var localTokens = new List<SyntaxToken>();
        for (var k = hashIndex; k < tokenEnd; k++) {
            var raw = _raw[k];
            if (!SyntaxTokenFactory.TryClassifyNonSignificant(raw.Type, out var classification)) {
                continue;
            }

            if (k >= skipFrom) {
                classification = TextClassification.Skiped;
            }

            localTokens.Add(SyntaxTokenFactory.CreateToken(raw.Extent, raw.Type, classification, node,
                                                           SyntaxTriviaList.Empty, SyntaxTriviaList.Empty));
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
    /// unmittelbar folgenden Präprozessor-Token (Autorität <see cref="SyntaxFacts.IsPreprocessorToken"/>)
    /// bis einschließlich des abschließenden PreprocessorNewLine; ein weiteres <c>#</c> beginnt den
    /// nächsten Lauf.
    /// </summary>
    int RunEnd(int hashIndex) {
        var end = hashIndex + 1;
        while (end < _raw.Length) {
            var type = _raw[end].Type;
            if (type == SyntaxTokenType.HashToken || !SyntaxFacts.IsPreprocessorToken(type)) {
                break;
            }

            end++;
            if (type == SyntaxTokenType.PreprocessorNewLine) {
                break;
            }
        }

        return end;
    }

}
using System.Collections.Generic;
using System.Collections.Immutable;

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
    /// <see cref="DirectiveRun"/> in Quelltext-Reihenfolge.
    /// </summary>
    public List<DirectiveRun> Parse() {
        return new List<DirectiveRun>();
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
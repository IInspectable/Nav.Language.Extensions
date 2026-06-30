#region Using Directives

using System;
using System.Collections.Generic;
using System.Collections.Immutable;

using Pharmatechnik.Nav.Language.Text;

#endregion

namespace Pharmatechnik.Nav.Language;

/// <summary>
/// Ein roh gelextes Token: lexikalischer Typ + Quelltext-Ausschnitt. Anders als <see cref="SyntaxToken"/>
/// trägt ein <see cref="RawToken"/> <b>keine</b> kontextabhängige <see cref="TextClassification"/> und
/// keinen Parent — die vergibt erst der Parser. Der Lexer liefert eine flache, lückenlose Folge solcher
/// Token (signifikante <b>und</b> Trivia gemischt), abgeschlossen durch genau ein
/// <see cref="SyntaxTokenType.EndOfFile"/>.
/// </summary>
readonly record struct RawToken(SyntaxTokenType Type, TextExtent Extent) {

    public int Start  => Extent.Start;
    public int Length => Extent.Length;
    public int End    => Extent.End;

    public bool IsTrivia => Type is SyntaxTokenType.Whitespace
        or SyntaxTokenType.NewLine
        or SyntaxTokenType.SingleLineComment
        or SyntaxTokenType.MultiLineComment;

}

/// <summary>
/// Handgeschriebener Lexer für die Nav-Sprache. Ein einziger Durchlauf liefert <b>alle</b> Token als
/// flache Liste inkl. Trivia (Whitespace, Zeilenenden, Kommentare) und Präprozessor-Token, gefolgt von
/// genau einem <see cref="SyntaxTokenType.EndOfFile"/>. Die Folge deckt den Quelltext lückenlos ab
/// (Full-Fidelity): aneinandergehängt ergeben die Ausschnitte wieder exakt den Eingabetext.
/// <para/>
/// Bewusste Eigenheiten, die das beobachtbare Verhalten der bisherigen ANTLR-Pipeline reproduzieren:
/// <list type="bullet">
/// <item>Ein Zeilenkommentar endet <b>vor</b> dem Zeilenende; das Zeilenende wird als eigenes
/// <see cref="SyntaxTokenType.NewLine"/> gelext.</item>
/// <item>Mehrzeichen-Kanten (<c>--&gt;</c>, <c>==&gt;</c>, <c>o-&gt;</c>) werden vor dem Identifier
/// erkannt, sonst verschluckt der Identifier-Scan das <c>o</c>.</item>
/// <item>Ein String-Literal ohne schließendes <c>"</c> zerfällt: das öffnende <c>"</c> wird zu
/// <see cref="SyntaxTokenType.Unknown"/>, der Rest normal weitergelext.</item>
/// </list>
/// </summary>
sealed class NavLexer {

    readonly string _text;
    readonly int    _length;

    int _pos;

    readonly ImmutableArray<RawToken>.Builder _tokens;

    NavLexer(string text) {
        _text   = text ?? String.Empty;
        _length = _text.Length;
        // Heuristische Vorab-Kapazität (~1 Token je 4 Zeichen über den realen Korpus gemessen) erspart dem
        // Builder die wiederholten Verdopplungen beim Wachsen. RawToken ist ein Wert-Struct — der Puffer ist
        // ein einziges Array, kein Heap-Objekt je Token.
        _tokens = ImmutableArray.CreateBuilder<RawToken>(Math.Max(16, _length / 4));
    }

    public static ImmutableArray<RawToken> Lex(string text) {
        return new NavLexer(text).LexAll();
    }

    ImmutableArray<RawToken> LexAll() {

        while (_pos < _length) {
            var before = _pos;
            ScanToken();

            // Fortschritts-Garantie: jeder Scan muss die Position vorrücken, sonst liefe der Lexer
            // endlos (und allokierte Token bis zum OutOfMemory).
            if (_pos <= before) {
                throw new InvalidOperationException($"NavLexer kam an Position {before} nicht voran (Zeichen U+{(int)_text[before]:X4}).");
            }
        }

        // Genau ein abschließendes, nullbreites EndOfFile.
        Add(SyntaxTokenType.EndOfFile, _pos, _pos);

        return _tokens.ToImmutable();
    }

    void ScanToken() {

        var c = _text[_pos];

        if (IsWhitespace(c)) {
            ScanWhitespace();
            return;
        }

        var nlLength = NewLineLength(_pos);
        if (nlLength > 0) {
            AddAndAdvance(SyntaxTokenType.NewLine, nlLength);
            return;
        }

        if (c == '/' && Peek(1) == '/') {
            ScanSingleLineComment();
            return;
        }

        if (c == '/' && Peek(1) == '*') {
            ScanMultiLineComment();
            return;
        }

        // Mehrzeichen-Kanten vor dem Identifier (sonst frisst der Identifier-Scan das 'o' aus 'o->').
        if (Match("-->")) {
            AddAndAdvance(SyntaxTokenType.GoToEdgeKeyword, 3);
            return;
        }

        if (Match("==>")) {
            AddAndAdvance(SyntaxTokenType.NonModalEdgeKeyword, 3);
            return;
        }

        if (Match("o->")) {
            AddAndAdvance(SyntaxTokenType.ModalEdgeKeyword, 3);
            return;
        }

        if (c == '#') {
            ScanPreprocessor();
            return;
        }

        if (c == '"') {
            ScanStringLiteral();
            return;
        }

        if (TryPunctuation(c, out var punctuation)) {
            AddAndAdvance(punctuation, 1);
            return;
        }

        if (SyntaxFacts.IsIdentifierCharacter(c)) {
            ScanIdentifierOrKeyword();
            return;
        }

        // Alles andere ist ein einzelnes unbekanntes Zeichen.
        AddAndAdvance(SyntaxTokenType.Unknown, 1);
    }

    void ScanWhitespace() {
        var start = _pos;
        while (_pos < _length && IsWhitespace(_text[_pos])) {
            _pos++;
        }

        Add(SyntaxTokenType.Whitespace, start, _pos);
    }

    void ScanSingleLineComment() {
        var start = _pos;
        _pos += 2; // '//'
        while (_pos < _length && NewLineLength(_pos) == 0) {
            _pos++;
        }

        if (_pos >= _length) {
            // Kommentar bis EOF — kein abschließendes Zeilenende.
            Add(SyntaxTokenType.SingleLineComment, start, _pos);
            return;
        }

        // Asymmetrische Abspaltung des Zeilenendes (reproduziert das beobachtbare Verhalten der
        // bisherigen Pipeline): Bei CR/LF/CRLF wird nur das LETZTE Newline-Zeichen als eigenes
        // NewLine-Token abgespalten — bei CRLF bleibt das '\r' Teil des Kommentars. NEL/LS/PS
        // beenden die Zeile zwar, bleiben aber vollständig Teil des Kommentar-Tokens (kein NewLine).
        var  nlEnd = _pos + NewLineLength(_pos);
        var last  = _text[nlEnd - 1];

        if (last == '\n' || last == '\r') {
            var prevIsLineFeed = nlEnd - 2 >= start && _text[nlEnd - 2] == '\n';
            var  splitPos       = prevIsLineFeed ? nlEnd - 2 : nlEnd - 1;
            Add(SyntaxTokenType.SingleLineComment, start, splitPos);
            _pos = splitPos; // Das restliche Zeilenende lext die Hauptschleife als NewLine.
        } else {
            Add(SyntaxTokenType.SingleLineComment, start, nlEnd);
            _pos = nlEnd;
        }
    }

    void ScanMultiLineComment() {
        var start = _pos;
        var scan  = _pos + 2; // nach '/*'
        while (scan + 1 < _length && !(_text[scan] == '*' && _text[scan + 1] == '/')) {
            scan++;
        }

        if (scan + 1 < _length && _text[scan] == '*' && _text[scan + 1] == '/') {
            _pos = scan + 2;
            Add(SyntaxTokenType.MultiLineComment, start, _pos);
        } else {
            // Unterminiert (kein '*/' bis EOF): das öffnende '/' wird zu Unknown, der Rest wird
            // normal weitergelext — so verhält sich auch die bisherige Pipeline.
            AddAndAdvance(SyntaxTokenType.Unknown, 1);
        }
    }

    void ScanStringLiteral() {
        var start = _pos;
        var i     = _pos + 1;
        while (i < _length) {
            var ch = _text[i];
            if (ch == '"') {
                _pos = i + 1;
                Add(SyntaxTokenType.StringLiteral, start, _pos);
                return;
            }

            // Nur '"', CR, LF, LS und PS beenden das Literal — NEL (U+0085) bleibt darin.
            if (IsStringLiteralTerminator(ch)) {
                break;
            }

            i++;
        }

        // Kein schließendes '"': das öffnende '"' zerfällt in ein Unknown, der Rest wird weitergelext.
        Add(SyntaxTokenType.Unknown, start, start + 1);
        _pos = start + 1;
    }

    void ScanIdentifierOrKeyword() {
        var start = _pos;

        // Alle Schlüsselwörter bestehen ausschließlich aus ASCII-Kleinbuchstaben. Schon während des Scans
        // mitführen, ob der Lauf überhaupt ein Keyword sein *kann* — sobald ein Zeichen außerhalb 'a'..'z'
        // auftaucht (Großbuchstabe, Ziffer, '.', '_', Umlaut), ist es sicher ein Identifier. Dann entfällt
        // der string-Substring nur fürs Dictionary-Lookup — bei nahezu allen Identifiern (Typnamen,
        // gepunktete Namespaces, camelCase-Namen) die häufigste vermeidbare Allokation des Lexers.
        var couldBeKeyword = true;
        while (_pos < _length && SyntaxFacts.IsIdentifierCharacter(_text[_pos])) {
            var ch = _text[_pos];
            if (ch < 'a' || ch > 'z') {
                couldBeKeyword = false;
            }

            _pos++;
        }

        var length = _pos - start;
        var type   = SyntaxTokenType.Identifier;
        if (couldBeKeyword && length is >= MinKeywordLength and <= MaxKeywordLength &&
            Keywords.TryGetValue(_text.Substring(start, length), out var keyword)) {
            type = keyword;
        }

        Add(type, start, _pos);
    }

    void ScanPreprocessor() {
        // '#'
        Add(SyntaxTokenType.HashToken, _pos, _pos + 1);
        _pos++;

        // Direktiven-Schlüsselwort: ein Lauf reiner Buchstaben (ohne Ziffern/'.'/'_'). Solange es noch
        // nicht gelext wurde, gilt der PreprocessorMode, in dem jedes Zeilenende die Direktive beendet.
        var inTextMode = false;
        if (_pos < _length && IsLetterCharacter(_text[_pos])) {
            var kwStart = _pos;
            while (_pos < _length && IsLetterCharacter(_text[_pos])) {
                _pos++;
            }

            Add(SyntaxTokenType.PreprocessorKeyword, kwStart, _pos);
            inTextMode = true;
        }

        // Direktiven-Rumpf je Zeichen. Ein '\r\n' beendet die Direktive immer als PreprocessorNewLine
        // (Längstmatch). Ein Einzelzeichen-Zeilenende beendet sie nur VOR dem Keyword; im Textmodus
        // bleibt es PreprocessorText (Regel-Reihenfolge der bisherigen Lexer-Grammatik).
        while (_pos < _length) {
            var nlLength = NewLineLength(_pos);

            if (nlLength == 2) {
                AddAndAdvance(SyntaxTokenType.PreprocessorNewLine, 2);
                return;
            }

            if (nlLength == 1 && !inTextMode) {
                AddAndAdvance(SyntaxTokenType.PreprocessorNewLine, 1);
                return;
            }

            AddAndAdvance(SyntaxTokenType.PreprocessorText, 1);
        }
    }

    void Add(SyntaxTokenType type, int start, int end) {
        _tokens.Add(new RawToken(type, TextExtent.FromBounds(start, end)));
    }

    // Hängt ein Token ab der aktuellen Position an und rückt die Position um seine Länge vor. Die
    // Scan*-Methoden führen ihre Position selbst; die direkten Einzeichen-/Operator-Zweige nutzen dies.
    void AddAndAdvance(SyntaxTokenType type, int length) {
        Add(type, _pos, _pos + length);
        _pos += length;
    }

    char Peek(int offset) {
        var index = _pos + offset;
        return index < _length ? _text[index] : '\0';
    }

    bool Match(string s) {
        if (_pos + s.Length > _length) {
            return false;
        }

        for (var k = 0; k < s.Length; k++) {
            if (_text[_pos + k] != s[k]) {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Länge des Zeilenendes an <paramref name="index"/> (2 für <c>\r\n</c>, 1 für die übrigen
    /// NL-Varianten), 0 wenn dort kein Zeilenende beginnt.
    /// </summary>
    int NewLineLength(int index) {
        if (index >= _length) {
            return 0;
        }

        var c = _text[index];
        if (c == '\r') {
            return index + 1 < _length && _text[index + 1] == '\n' ? 2 : 1;
        }

        // NL-Varianten: LF, NEL (U+0085), LS (U+2028), PS (U+2029).
        if (c == '\n' || c == '\u0085' || c == '\u2028' || c == '\u2029') {
            return 1;
        }

        return 0;
    }

    static bool IsStringLiteralTerminator(char c) {
        // Nur CR, LF, LS und PS beenden ein Literal — NEL (U+0085) gehört bewusst NICHT dazu.
        return c == '\r' || c == '\n' || c == '\u2028' || c == '\u2029';
    }

    static bool IsLetterCharacter(char c) {
        return c is >= 'a' and <= 'z' ||
               c is >= 'A' and <= 'Z' ||
               c == 'Ä'               || c == 'Ö' || c == 'Ü' ||
               c == 'ä'               || c == 'ö' || c == 'ü' || c == 'ß';
    }

    static bool IsWhitespace(char c) {
        // Zs-Klasse (exakt die Liste der Grammatik) plus Tab/VT/FF; Zeilenenden sind hier kein Whitespace.
        return c == '\u0009' || c == '\u000B' || c == '\u000C' || c == '\u0020' || c == '\u00A0' || c == '\u1680' || c == '\u180E' || c == '\u2000' || c == '\u2001' || c == '\u2002' || c == '\u2003' || c == '\u2004' || c == '\u2005' || c == '\u2006' || c == '\u2008' || c == '\u2009' || c == '\u200A' || c == '\u202F' || c == '\u3000' || c == '\u205F';
    }

    static bool TryPunctuation(char c, out SyntaxTokenType type) {
        switch (c) {
            case '{':
                type = SyntaxTokenType.OpenBrace;
                return true;
            case '}':
                type = SyntaxTokenType.CloseBrace;
                return true;
            case '(':
                type = SyntaxTokenType.OpenParen;
                return true;
            case ')':
                type = SyntaxTokenType.CloseParen;
                return true;
            case '[':
                type = SyntaxTokenType.OpenBracket;
                return true;
            case ']':
                type = SyntaxTokenType.CloseBracket;
                return true;
            case '<':
                type = SyntaxTokenType.LessThan;
                return true;
            case '>':
                type = SyntaxTokenType.GreaterThan;
                return true;
            case ';':
                type = SyntaxTokenType.Semicolon;
                return true;
            case ',':
                type = SyntaxTokenType.Comma;
                return true;
            case ':':
                type = SyntaxTokenType.Colon;
                return true;
            case '?':
                type = SyntaxTokenType.Questionmark;
                return true;
            default:
                type = SyntaxTokenType.Unknown;
                return false;
        }
    }

    // Längen-Schranken der Wort-Schlüsselwörter ('do'/'if'/'on' = 2 … 'namespaceprefix' = 15) — als
    // billiger Vorab-Filter vor dem Dictionary-Lookup (siehe ScanIdentifierOrKeyword).
    const int MinKeywordLength = 2;
    const int MaxKeywordLength = 15;

    // Die Wort-Schlüsselwörter der Grammatik (exakt die literalen, kleingeschriebenen Formen).
    // Mehrzeichen-Kanten ('-->', 'o->', '==>') sind hier bewusst nicht enthalten — sie werden vor
    // dem Identifier-Scan direkt erkannt.
    static readonly Dictionary<string, SyntaxTokenType> Keywords = new() {
        ["task"]            = SyntaxTokenType.TaskKeyword,
        ["taskref"]         = SyntaxTokenType.TaskrefKeyword,
        ["init"]            = SyntaxTokenType.InitKeyword,
        ["end"]             = SyntaxTokenType.EndKeyword,
        ["choice"]          = SyntaxTokenType.ChoiceKeyword,
        ["dialog"]          = SyntaxTokenType.DialogKeyword,
        ["view"]            = SyntaxTokenType.ViewKeyword,
        ["exit"]            = SyntaxTokenType.ExitKeyword,
        ["on"]              = SyntaxTokenType.OnKeyword,
        ["if"]              = SyntaxTokenType.IfKeyword,
        ["else"]            = SyntaxTokenType.ElseKeyword,
        ["spontaneous"]     = SyntaxTokenType.SpontaneousKeyword,
        ["spont"]           = SyntaxTokenType.SpontKeyword,
        ["do"]              = SyntaxTokenType.DoKeyword,
        ["result"]          = SyntaxTokenType.ResultKeyword,
        ["params"]          = SyntaxTokenType.ParamsKeyword,
        ["base"]            = SyntaxTokenType.BaseKeyword,
        ["namespaceprefix"] = SyntaxTokenType.NamespaceprefixKeyword,
        ["using"]           = SyntaxTokenType.UsingKeyword,
        ["code"]            = SyntaxTokenType.CodeKeyword,
        ["generateto"]      = SyntaxTokenType.GeneratetoKeyword,
        ["notimplemented"]  = SyntaxTokenType.NotimplementedKeyword,
        ["abstractmethod"]  = SyntaxTokenType.AbstractmethodKeyword,
        ["donotinject"]     = SyntaxTokenType.DonotinjectKeyword,
    };

}
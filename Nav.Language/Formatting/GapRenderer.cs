using System.Collections.Generic;
using System.Text;

using Pharmatechnik.Nav.Language.Text;

namespace Pharmatechnik.Nav.Language.Formatting;

/// <summary>
/// Die einzige Stelle, die ein <see cref="GapLayout"/> zusammen mit der zu erhaltenden Trivia
/// (Kommentare, Direktiven) zu einem kanonischen Lücken-String macht — die Regeln bestimmen nur das
/// Skelett und bleiben simpel.
/// </summary>
/// <remarks>
/// <para><b>Vertikalmodell kommentarreicher Lücken:</b> Die Lücke wird entlang ihrer
/// <see cref="SyntaxTokenType.NewLine"/>-Trivia in ihre <b>authored Zeilenstruktur</b> zerlegt:
/// ein Trailing-Segment (auf der Zeile von <c>Prev</c>), null oder mehr Innenzeilen und das
/// Leading-Segment (auf der Zeile von <c>Next</c>). Das Layout bestimmt nur zwei Dinge — ob <c>Next</c>
/// auf derselben Zeile bleibt und seinen horizontalen Ziel-Ort (nichts/Space/Spalte bzw.
/// Einzug/Spalte). Die Innenstruktur (Reihenfolge von Leer-, Kommentar- und Direktivzeilen) wird nie
/// erfunden, entfernt oder umsortiert; normalisiert wird pro Zeile nur der Whitespace:</para>
/// <list type="bullet">
///   <item><description><b>Trailing-Kommentare</b> (vor dem ersten Newline) bleiben auf der Zeile von
///   <c>Prev</c>, genau ein Space davor.</description></item>
///   <item><description><b>Eigene-Zeile-Kommentare</b> stehen auf dem Zeilen-Präfix des Layouts
///   (Block-Einzug bzw. Gruppenspalte); der Kommentar-Text bleibt verbatim. Bei mehrzeiligen
///   <c>/* */</c>-Kommentaren wird nur der Whitespace vor der ersten Zeile normalisiert, das Innere
///   bleibt unangetastet.</description></item>
///   <item><description><b>Direktiven</b> stehen immer auf eigener Zeile ab Spalte 0, Text verbatim —
///   jedes Einrücken oder Verschieben zerstörte sie (Lexer-Gate).</description></item>
///   <item><description><b>Leerzeilen</b> werden nie kollabiert; verlangt das Layout mehr Leerzeilen als
///   der Autor gesetzt hat (<see cref="GapLayout.NewLine.BlankLinesBefore"/> als Minimum), werden die
///   fehlenden unmittelbar vor der Zeile von <c>Next</c> ergänzt.</description></item>
/// </list>
/// <para><b>Renderer-Schranke (Defense-in-Depth):</b> Verlangt ein Layout Same-Line, obwohl die Lücke
/// zeilen-erzwingende Trivia enthält (Newline, <c>//</c>-Kommentar, mehrzeiliger Block-Kommentar oder
/// Direktive), wird nie same-line gerendert — ein <c>//</c>-Kommentar würde das folgende Token
/// verschlucken. Das horizontale Layout degradiert dann zu einem Zeilenumbruch auf
/// <see cref="GapContext.IndentDepth"/> mit erhaltener authored Innenstruktur. Enthält die Lücke eine
/// <see cref="SyntaxTokenType.SkippedTokensTrivia"/>, bleibt sie byte-genau erhalten (verbatim).</para>
/// </remarks>
sealed class GapRenderer {

    readonly SourceText           _sourceText;
    readonly TextEditorSettings   _settings;
    readonly NavFormattingOptions _options;

    public GapRenderer(SourceText sourceText, TextEditorSettings settings, NavFormattingOptions options) {
        _sourceText = sourceText;
        _settings   = settings;
        _options    = options;
    }

    /// <summary>Rendert den kanonischen Lückeninhalt für das entschiedene Layout.</summary>
    public string Render(in GapContext ctx, GapLayout layout) {

        // Skiped-Läufe byte-genau erhalten — hier wird dem Baum nicht getraut, die Safety-Regeln
        // entscheiden über die Behandlung der ganzen Anweisung.
        if (layout is GapLayout.Verbatim || ctx.Trivia.HasSkippedTokens) {
            return _sourceText.Substring(ctx.Extent);
        }

        switch (layout) {
            case GapLayout.Nothing:
                return RenderHorizontal(ctx, separator: "");
            case GapLayout.SingleSpace singleSpace:
                return RenderHorizontal(ctx, separator: " ", allowPullUp: singleSpace.PullUpAuthoredLineBreaks);
            case GapLayout.AlignedColumn:
                return RenderHorizontal(ctx, separator: AlignmentSpaces(ctx, fallback: " "));
            case GapLayout.NewLine newLine:
                return RenderVertical(ctx, newLine.BlankLinesBefore, linePrefix: IndentString(newLine.IndentDepth));
            case GapLayout.NewLineAlignedColumn newLineAligned:
                return RenderVertical(ctx, newLineAligned.BlankLinesBefore, linePrefix: AlignmentSpaces(ctx, fallback: IndentString(ctx.IndentDepth)));
            default:
                // Geschlossenes Vokabular — unbekanntes Layout gibt es nicht; verbatim ist die sichere Antwort.
                return _sourceText.Substring(ctx.Extent);
        }
    }

    /// <summary>
    /// Same-Line-Rendering: Inline-Block-Kommentare bleiben auf der Zeile (je ein Space davor, Inhalt
    /// verbatim), danach der Layout-Trenner. Bei zeilen-erzwingender Trivia greift die Renderer-Schranke;
    /// einzige Ausnahme ist das Hochziehen der Task-/taskref-Kopf-Kanonisierung
    /// (<paramref name="allowPullUp"/>): <b>bloße</b> authored Newlines entfallen — enthält die Lücke
    /// dagegen einen <c>//</c>-Kommentar, einen mehrzeiligen Block-Kommentar oder eine Direktive, wird
    /// nie hochgezogen.
    /// </summary>
    string RenderHorizontal(in GapContext ctx, string separator, bool allowPullUp = false) {

        if (RequiresLineBreak(ctx) && !(allowPullUp && !ctx.Trivia.HasLineBreakingComment && !ctx.Trivia.HasDirective)) {
            // Renderer-Schranke: nie ein Token hinter einen '//'-Kommentar oder auf eine Direktivzeile
            // ziehen — das horizontale Layout degradiert zum Umbruch auf Block-Einzug.
            return RenderVertical(ctx, blankLinesBefore: 0, linePrefix: IndentString(ctx.IndentDepth));
        }

        var sb = new StringBuilder();
        foreach (var trivia in EnumerateTrivia(ctx)) {
            if (trivia.IsComment) {
                sb.Append(' ').Append(CommentText(trivia));
            }
        }

        if (sb.Length > 0 && separator.Length == 0) {
            // Ein Inline-Kommentar bekommt beidseitig ein Space — auch wenn die Token selbst tight wären.
            separator = " ";
        }

        return sb.Append(separator).ToString();
    }

    /// <summary>
    /// Zeilenumbruch-Rendering nach dem Vertikalmodell (siehe Klassen-Doku): authored Zeilenstruktur
    /// erhalten, Whitespace je Zeile normalisieren, fehlende Leerzeilen bis zum Minimum ergänzen,
    /// abschließend das Zeilen-Präfix (Einzug bzw. Spalte) vor <c>Next</c>.
    /// </summary>
    string RenderVertical(in GapContext ctx, int blankLinesBefore, string linePrefix) {

        var lines = SplitLines(ctx);
        var sb    = new StringBuilder();

        // Trailing-Segment: Kommentare bleiben auf der Zeile von Prev, genau ein Space davor.
        foreach (var trivia in lines[0]) {
            if (trivia.IsComment) {
                sb.Append(' ').Append(CommentText(trivia));
            }
        }

        // Innenzeilen in authored Reihenfolge; Leerzeilen dabei zählen, um das Minimum zu ergänzen.
        var blankLines = 0;
        for (var i = 1; i < lines.Count - 1; i++) {
            var content = RenderInteriorLine(lines[i], linePrefix);
            if (content.Length == 0) {
                blankLines++;
            }

            sb.Append(_settings.NewLine).Append(content);
        }

        for (; blankLines < blankLinesBefore; blankLines++) {
            sb.Append(_settings.NewLine);
        }

        // Zeile von Next: Präfix, dann etwaige Inline-Kommentare vor dem Token (je ein Space danach).
        // Bei einer einzeilig authored Lücke (kein Newline) ist das eine Segment bereits als Trailing
        // behandelt — es darf nicht ein zweites Mal (als Leading) emittiert werden.
        sb.Append(_settings.NewLine).Append(linePrefix);
        if (lines.Count > 1) {
            foreach (var trivia in lines[lines.Count - 1]) {
                if (trivia.IsComment) {
                    sb.Append(CommentText(trivia)).Append(' ');
                }
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Rendert den Datei-Anfang: die Leading-Trivia des <b>ersten</b> Tokens liegt vor der ersten
    /// Paar-Lücke und wird gesondert normalisiert — Kommentarzeilen auf dem Einzug des ersten Tokens,
    /// Direktiven ab Spalte 0, Leerzeilen erhalten, das Token selbst beginnt seine Zeile auf
    /// <paramref name="indentDepth"/> (Trailing-Whitespace/Fehl-Einzug davor entfällt).
    /// </summary>
    public string RenderLeadingGap(SyntaxToken firstToken, int indentDepth) {

        var lines  = SplitLines(firstToken.LeadingTrivia);
        var prefix = IndentString(indentDepth);
        var sb     = new StringBuilder();

        for (var i = 0; i < lines.Count - 1; i++) {
            sb.Append(RenderInteriorLine(lines[i], prefix)).Append(_settings.NewLine);
        }

        // Zeile des ersten Tokens: Einzug, dann etwaige Inline-Kommentare vor dem Token.
        sb.Append(prefix);
        foreach (var trivia in lines[lines.Count - 1]) {
            if (trivia.IsComment) {
                sb.Append(CommentText(trivia)).Append(' ');
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Rendert die Final-Lücke zwischen dem letzten realen Token (<c>null</c>, wenn die Datei keines hat)
    /// und dem Dateiende: Trailing-Kommentare bleiben auf der Zeile des letzten Tokens, Kommentar-/
    /// Direktivzeilen und Leerzeilen dazwischen bleiben erhalten (Tiefe 0 bzw. Spalte 0) — aber hinter
    /// dem letzten Inhalt endet die Datei mit <b>genau einer</b> Newline (EOF-Trailing-Trim). Eine Datei
    /// ganz ohne Inhalt (leer bzw. nur Whitespace) bleibt bzw. wird leer.
    /// </summary>
    public string RenderFinalGap(SyntaxToken? lastToken, SyntaxToken endOfFile) {

        var lines = SplitLines(EnumerateFinalTrivia(lastToken, endOfFile));
        var sb    = new StringBuilder();

        var firstInteriorLine = 0;
        if (lastToken != null) {
            // Zeile des letzten Tokens: Trailing-Kommentare bleiben dort, genau ein Space davor.
            foreach (var trivia in lines[0]) {
                if (trivia.IsComment) {
                    sb.Append(' ').Append(CommentText(trivia));
                }
            }

            firstInteriorLine = 1;
        }

        var contents = new List<string>();
        for (var i = firstInteriorLine; i < lines.Count; i++) {
            contents.Add(RenderInteriorLine(lines[i], linePrefix: IndentString(0)));
        }

        // EOF-Trailing-Trim: Leerzeilen hinter dem letzten Inhalt entfallen — dann genau eine Final-Newline.
        while (contents.Count > 0 && contents[contents.Count - 1].Length == 0) {
            contents.RemoveAt(contents.Count - 1);
        }

        for (var i = 0; i < contents.Count; i++) {
            if (i > 0 || lastToken != null) {
                sb.Append(_settings.NewLine);
            }

            sb.Append(contents[i]);
        }

        if (lastToken != null || contents.Count > 0) {
            sb.Append(_settings.NewLine);
        }

        return sb.ToString();
    }

    static IEnumerable<SyntaxTrivia> EnumerateFinalTrivia(SyntaxToken? lastToken, SyntaxToken endOfFile) {

        if (lastToken != null) {
            foreach (var trivia in lastToken.Value.TrailingTrivia) {
                yield return trivia;
            }
        }

        foreach (var trivia in endOfFile.LeadingTrivia) {
            yield return trivia;
        }
    }

    /// <summary>Eine Innenzeile: leer, Direktive ab Spalte 0 (verbatim) oder Kommentar(e) auf dem Zeilen-Präfix.</summary>
    string RenderInteriorLine(List<SyntaxTrivia> line, string linePrefix) {

        var sb = new StringBuilder();
        foreach (var trivia in line) {
            if (trivia.Type == SyntaxTokenType.DirectiveTrivia) {
                // Direktiven immer ab Spalte 0, Text verbatim — nie einrücken (Lexer-Gate).
                return _sourceText.Substring(trivia.Extent);
            }

            if (trivia.IsComment) {
                // Der erste Kommentar der Zeile steht auf dem Zeilen-Präfix; ein mehrzeiliger Block-
                // Kommentar zieht seine Innenzeilen per Delta-Shift mit (relative Einrückung erhalten).
                sb.Append(sb.Length == 0
                              ? linePrefix + OwnLineCommentText(trivia, linePrefix.Length)
                              : " "        + CommentText(trivia));
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Rendert eine hand-gelegte (mehrzeilige) Anweisung als <b>Ganzes verbatim</b>, aber mit um
    /// <paramref name="delta"/> Zeichen mitgeschobenen Innenzeilen-Präfixen — der äußere Einzug wird auf
    /// den Block re-gesetzt (der Delta stammt aus dem Vorpass), das Innere bleibt byte-genau (relative
    /// Form erhalten). Derselbe Mechanismus wie beim mehrzeiligen Block-Kommentar (<see cref="ShiftInteriorLines"/>).
    /// </summary>
    public string RenderRawShifted(in GapContext ctx, int delta) {
        return ShiftInteriorLines(_sourceText.Substring(ctx.Extent), delta);
    }

    /// <summary>
    /// Der zu emittierende Kommentar-Text. Ein <c>//</c>-Kommentar verschluckt beim Lexen das <c>\r</c>
    /// des Zeilenendes (die <see cref="SyntaxTokenType.NewLine"/>-Trivia trägt dann nur das <c>\n</c>) —
    /// da der Renderer das Zeilenende selbst schreibt (<see cref="TextEditorSettings.NewLine"/>), wird
    /// Zeilenend-Whitespace des Kommentars gekappt. Block-Kommentare bleiben verbatim.
    /// </summary>
    string CommentText(in SyntaxTrivia trivia) {
        var text = _sourceText.Substring(trivia.Extent);
        return trivia.Type == SyntaxTokenType.SingleLineComment ? text.TrimEnd() : text;
    }

    /// <summary>
    /// Der Text eines auf eigener Zeile stehenden Kommentars, hinter dem Zeilen-Präfix. Ein
    /// <b>mehrzeiliger</b> <c>/* */</c>-Block-Kommentar wird nicht reflowt, aber seine Folgezeilen werden um
    /// dasselbe Delta mitgeschoben, um das die erste Zeile beim Neu-Einrücken wandert (Ziel-Präfixlänge
    /// minus authored Einrückung) — relative Einrückung des Kommentar-Inneren bleibt erhalten. Einzeilige
    /// Kommentare bleiben unangetastet.
    /// </summary>
    string OwnLineCommentText(in SyntaxTrivia trivia, int targetPrefixLength) {

        var text = CommentText(trivia);

        if (trivia.Type != SyntaxTokenType.MultiLineComment || text.IndexOf('\n') < 0) {
            return text;
        }

        return ShiftInteriorLines(text, targetPrefixLength - AuthoredIndentChars(trivia.Extent.Start));
    }

    /// <summary>Die Anzahl der Whitespace-Zeichen unmittelbar vor <paramref name="position"/> (authored Einrückung der Zeile).</summary>
    int AuthoredIndentChars(int position) {

        var count = 0;
        for (var i = position - 1; i >= 0 && (_sourceText[i] == ' ' || _sourceText[i] == '\t'); i--) {
            count++;
        }

        return count;
    }

    /// <summary>
    /// Verschiebt den führenden Whitespace jeder Zeile <b>nach der ersten</b> um <paramref name="delta"/>
    /// Zeichen — der eine Delta-Shift-Mechanismus für hand-gelegte Anweisungen (äußerer Einzug) und die
    /// Innenzeilen mehrzeiliger Block-Kommentare. Positiv: <paramref name="delta"/> Leerzeichen voranstellen;
    /// negativ: bis zu <c>-delta</c> führende Whitespace-Zeichen entfernen (Clamp bei Spalte 0 — nie
    /// Nicht-Whitespace anfassen). Arbeitet auf den <b>Roh-Whitespace-Präfixen</b> (keine Spaltenarithmetik,
    /// damit Tabs im Inneren eindeutig bleiben); Leerzeilen bleiben unangetastet (kein neuer
    /// Trailing-Whitespace). Idempotent, sobald der Aufrufer <c>delta = 0</c> rechnet.
    /// </summary>
    static string ShiftInteriorLines(string text, int delta) {

        if (delta == 0 || text.IndexOf('\n') < 0) {
            return text;
        }

        var sb          = new StringBuilder(text.Length + (delta > 0 ? delta * 4 : 0));
        var atLineStart = false; // Die erste Zeile bleibt unangetastet.

        var i = 0;
        while (i < text.Length) {

            if (atLineStart) {
                atLineStart = false;

                var wsEnd = i;
                while (wsEnd < text.Length && (text[wsEnd] == ' ' || text[wsEnd] == '\t')) {
                    wsEnd++;
                }

                // Leerzeile = Whitespace, dem ein Zeilenumbruch folgt (nie neuen Trailing-Whitespace
                // erzeugen). Whitespace am Text-Ende ist dagegen der Einzug VOR dem nächsten Token (die
                // letzte Zeile der Lücke) und wird sehr wohl mitgeschoben.
                var isBlankLine = wsEnd < text.Length && (text[wsEnd] == '\n' || text[wsEnd] == '\r');
                if (isBlankLine) {
                    // Leerzeile: Präfix verbatim, nie einen neuen Trailing-Whitespace erzeugen.
                    sb.Append(text, i, wsEnd - i);
                } else if (delta > 0) {
                    sb.Append(' ', delta).Append(text, i, wsEnd - i);
                } else {
                    var keep = System.Math.Max(0, (wsEnd - i) + delta);
                    sb.Append(text, wsEnd - keep, keep);
                }

                i = wsEnd;
                continue;
            }

            var c = text[i];
            sb.Append(c);
            atLineStart = c == '\n';
            i++;
        }

        return sb.ToString();
    }

    /// <summary>
    /// Ob die Lücke Trivia enthält, die einen Zeilenumbruch erzwingt oder enthält: ein Newline, ein
    /// <c>//</c>-Kommentar (läuft bis Zeilenende), ein mehrzeiliger Block-Kommentar oder eine Direktive.
    /// Ein einzeiliger <c>/* */</c>-Kommentar zählt nicht — er verhält sich wie ein Inline-Token.
    /// </summary>
    static bool RequiresLineBreak(in GapContext ctx) =>
        ctx.Trivia.NewLineCount > 0 || ctx.Trivia.HasDirective || ctx.Trivia.HasLineBreakingComment;

    /// <summary>
    /// Zerlegt den Lückeninhalt entlang seiner <see cref="SyntaxTokenType.NewLine"/>-Trivia in die
    /// authored Zeilenstruktur: Index 0 = Trailing-Segment (Zeile von <c>Prev</c>), letzter Index =
    /// Leading-Segment (Zeile von <c>Next</c>). Newlines im Inneren mehrzeiliger Kommentare sind Teil
    /// des Kommentar-Texts und zerteilen nicht.
    /// </summary>
    static List<List<SyntaxTrivia>> SplitLines(in GapContext ctx) {
        return SplitLines(EnumerateTrivia(ctx));
    }

    static List<List<SyntaxTrivia>> SplitLines(IEnumerable<SyntaxTrivia> triviaStream) {

        var lines = new List<List<SyntaxTrivia>> { new() };

        foreach (var trivia in triviaStream) {
            if (trivia.Type == SyntaxTokenType.NewLine) {
                lines.Add(new List<SyntaxTrivia>());
            } else {
                lines[lines.Count - 1].Add(trivia);
            }
        }

        return lines;
    }

    /// <summary>Der Lückeninhalt in Strom-Reihenfolge: <c>Prev.TrailingTrivia ++ Next.LeadingTrivia</c>.</summary>
    static IEnumerable<SyntaxTrivia> EnumerateTrivia(GapContext ctx) {

        foreach (var trivia in ctx.Prev.TrailingTrivia) {
            yield return trivia;
        }

        foreach (var trivia in ctx.Next.LeadingTrivia) {
            yield return trivia;
        }
    }

    /// <summary>Die aufgelöste Space-Zahl der Ausrichtung für diese Lücke, sonst <paramref name="fallback"/>.</summary>
    string AlignmentSpaces(in GapContext ctx, string fallback) {
        // Ausrichtungs-Padding ist immer Leerzeichen, nie Tabs — unabhängig vom Einzugsstil.
        return ctx.Alignment.TryGetSpaces(ctx.Extent.Start, out var spaces) ? new string(' ', spaces) : fallback;
    }

    /// <summary>Der Einzug für <paramref name="depth"/> Stufen gemäß <see cref="NavFormattingOptions.IndentStyle"/>.</summary>
    string IndentString(int depth) {

        if (depth <= 0) {
            return "";
        }

        return _options.IndentStyle == IndentStyle.Tabs
            ? new string('\t', depth)
            : new string(' ', depth * _options.IndentSize);
    }

}

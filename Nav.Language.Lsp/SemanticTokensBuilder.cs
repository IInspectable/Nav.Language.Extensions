#region Using Directives

using System;
using System.Collections.Generic;

using Pharmatechnik.Nav.Language;
using Pharmatechnik.Nav.Language.Text;

#endregion

namespace Pharmatechnik.Nav.Language.Lsp;

/// <summary>
/// Erzeugt LSP-Semantic-Tokens aus der bereits vorhandenen Token-Klassifizierung der Engine
/// (<see cref="SyntaxToken.Classification"/>). Damit ist die Engine die einzige Quelle der
/// Klassifizierung — inklusive kontextabhängiger Kategorien (TaskName, ConnectionPoint …), die eine
/// reine TextMate-Grammar nicht leisten könnte.
/// </summary>
static class SemanticTokensBuilder {

    // Legend: Die Reihenfolge bestimmt den Index, der in der kodierten Token-Liste referenziert wird.
    public static readonly string[] TokenTypes = {
        "keyword",    //  0
        "string",     //  1
        "comment",    //  2
        "type",       //  3
        "class",      //  4
        "variable",   //  5
        "parameter",  //  6
        "operator",   //  7
        "macro",      //  8
        "enumMember", //  9
        "property"    // 10
    };

    public static readonly string[] TokenModifiers = Array.Empty<string>();

    const int None = -1;

    static int MapTokenType(TextClassification classification) => classification switch {
        TextClassification.Keyword             => 0,
        TextClassification.ControlKeyword      => 0,
        TextClassification.StringLiteral       => 1,
        TextClassification.Comment             => 2,
        TextClassification.TypeName            => 3,
        TextClassification.FormName            => 3,
        TextClassification.TaskName            => 4,
        TextClassification.Identifier          => 5,
        TextClassification.ParameterName       => 6,
        TextClassification.Punctuation         => 7,
        TextClassification.PreprocessorKeyword => 8,
        TextClassification.PreprocessorText    => 8,
        TextClassification.ChoiceNode          => 9,
        TextClassification.GuiNode             => 9,
        TextClassification.ConnectionPoint     => 10,
        _                                      => None // Whitespace, Skiped, Unknown, Text, DeadCode
    };

    public static int[] Encode(SyntaxTree syntaxTree) {

        var data = new List<int>();

        var previousLine      = 0;
        var previousCharacter = 0;

        foreach (var span in CollectSpans(syntaxTree)) {

            foreach (var segment in SplitIntoLineSegments(span)) {

                var deltaLine      = segment.Line - previousLine;
                var deltaCharacter = deltaLine == 0 ? segment.Character - previousCharacter : segment.Character;

                data.Add(deltaLine);
                data.Add(deltaCharacter);
                data.Add(segment.Length);
                data.Add(span.TokenType);
                data.Add(0); // keine Modifier

                previousLine      = segment.Line;
                previousCharacter = segment.Character;
            }
        }

        return data.ToArray();
    }

    // Sammelt die zu kodierenden, klassifizierten Spans in Quelltext-Reihenfolge: die signifikanten Token aus
    // dem flachen Strom (Trivia übersprungen) plus die Kommentare aus der angehängten Trivia (Roslyn-Modell).
    // Whitespace/Zeilenenden tragen keine Klassifizierung und entfallen ersatzlos. Das Delta-Encoding verlangt
    // aufsteigende Positionen — daher abschließend nach Start-Offset sortieren.
    static List<ClassifiedSpan> CollectSpans(SyntaxTree syntaxTree) {

        var source = syntaxTree.SourceText;
        var spans  = new List<ClassifiedSpan>();

        foreach (var token in syntaxTree.Tokens) {

            if (SyntaxFacts.IsTrivia(token.Classification)) {
                continue;
            }

            var tokenType = MapTokenType(token.Classification);
            if (tokenType == None || token.IsMissing || token.Length <= 0) {
                continue;
            }

            var location = token.GetLocation();
            if (location == null) {
                continue;
            }

            spans.Add(new ClassifiedSpan(token.Start, token.ToString(), token.Length, tokenType, location));
        }

        var commentType = MapTokenType(TextClassification.Comment);
        foreach (var comment in syntaxTree.Comments()) {

            if (comment.Length <= 0) {
                continue;
            }

            var location = source.GetLocation(comment.Extent);
            if (location == null) {
                continue;
            }

            spans.Add(new ClassifiedSpan(comment.Start, comment.ToString(source), comment.Length, commentType, location));
        }

        spans.Sort((a, b) => a.Start.CompareTo(b.Start));

        return spans;
    }

    // Ein bereits klassifizierter Quelltext-Ausschnitt (signifikantes Token oder Kommentar-Trivia), aus dem die
    // Zeilen-Segmente und das Delta-Encoding erzeugt werden.
    readonly struct ClassifiedSpan {

        public ClassifiedSpan(int start, string text, int length, int tokenType, Location location) {
            Start     = start;
            Text      = text;
            Length    = length;
            TokenType = tokenType;
            Location  = location;
        }

        public int      Start     { get; }
        public string   Text      { get; }
        public int      Length    { get; }
        public int      TokenType { get; }
        public Location Location  { get; }
    }

    readonly struct LineSegment {

        public LineSegment(int line, int character, int length) {
            Line      = line;
            Character = character;
            Length    = length;
        }

        public int Line      { get; }
        public int Character  { get; }
        public int Length     { get; }
    }

    // LSP-Clients melden i.d.R. multilineTokenSupport=false — mehrzeilige Spans (z.B. Blockkommentare)
    // daher pro Zeile in Segmente aufteilen.
    static IEnumerable<LineSegment> SplitIntoLineSegments(ClassifiedSpan span) {

        var location = span.Location;

        if (location.StartLine == location.EndLine) {
            yield return new LineSegment(location.StartLine, location.StartCharacter, span.Length);
            yield break;
        }

        var lines = span.Text.Split('\n');

        for (var i = 0; i < lines.Length; i++) {

            var length = lines[i].TrimEnd('\r').Length;
            if (length <= 0) {
                continue;
            }

            var line      = location.StartLine + i;
            var character = i == 0 ? location.StartCharacter : 0;

            yield return new LineSegment(line, character, length);
        }
    }
}

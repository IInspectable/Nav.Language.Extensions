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

        foreach (var token in syntaxTree.Tokens) {

            var tokenType = MapTokenType(token.Classification);
            if (tokenType == None || token.IsMissing || token.Length <= 0) {
                continue;
            }

            var location = token.GetLocation();
            if (location == null) {
                continue;
            }

            foreach (var segment in SplitIntoLineSegments(token, location)) {

                var deltaLine      = segment.Line - previousLine;
                var deltaCharacter = deltaLine == 0 ? segment.Character - previousCharacter : segment.Character;

                data.Add(deltaLine);
                data.Add(deltaCharacter);
                data.Add(segment.Length);
                data.Add(tokenType);
                data.Add(0); // keine Modifier

                previousLine      = segment.Line;
                previousCharacter = segment.Character;
            }
        }

        return data.ToArray();
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

    // LSP-Clients melden i.d.R. multilineTokenSupport=false — mehrzeilige Tokens (z.B. Blockkommentare)
    // daher pro Zeile in Segmente aufteilen.
    static IEnumerable<LineSegment> SplitIntoLineSegments(SyntaxToken token, Location location) {

        if (location.StartLine == location.EndLine) {
            yield return new LineSegment(location.StartLine, location.StartCharacter, token.Length);
            yield break;
        }

        var lines = token.ToString().Split('\n');

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

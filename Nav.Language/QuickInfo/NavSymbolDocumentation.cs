#region Using Directives

using System.Collections.Generic;
using System.Text;

#endregion

namespace Pharmatechnik.Nav.Language.QuickInfo;

/// <summary>
/// Gewinnt die „Dokumentation" eines Symbols — die Erläuterungszeile der QuickInfo. Für Deklarationen ist
/// das der Quelltext-Kommentar unmittelbar über dem Knoten (das Nav-Pendant zu Roslyns Doc-Comments):
/// maßgeblich ist allein der zusammenhängende Kommentarblock direkt vor dem Knoten; eine Leerzeile trennt
/// ihn ab, weiter oben stehende Kommentare gehören nicht mehr dazu (Grundlage sind die Leading-Trivia des
/// Deklarations-Knotens, echtes Roslyn-Trivia-Modell). Kanten (<see cref="IEdgeModeSymbol"/>) tragen keinen
/// solchen Kommentar, sondern ihre eingebaute Bedeutung (<see cref="IEdgeModeSymbol.Description"/>).
/// Protokoll-frei und damit gemeinsam von VS-QuickInfo und LSP-Hover nutzbar („eine Engine").
/// </summary>
public static class NavSymbolDocumentation {

    /// <summary>
    /// Liefert die Doku-Zeile zum <paramref name="symbol"/>: für eine Kante ihre eingebaute Bedeutung, sonst
    /// den aufbereiteten Kommentartext über der Deklaration (Kommentar-Marker entfernt, je Quellzeile eine
    /// Zeile). <c>null</c>, wenn dort kein Kommentar steht bzw. das Symbol keine im Speicher gehaltene
    /// Deklaration besitzt (z.B. aus einer inkludierten Datei stammende TaskDeclarations). Referenzen lösen
    /// auf ihre Deklaration auf.
    /// </summary>
    public static string? GetDocumentation(ISymbol symbol) {

        // Eine Kante hat keine Quelltext-Doku, sondern erklärt ihre Bedeutung (Modal/Goto, Edge/Continuation).
        if (symbol is IEdgeModeSymbol edgeMode) {
            return edgeMode.Description;
        }

        var syntax = GetDeclarationSyntax(symbol);
        if (syntax == null) {
            return null;
        }

        var comments = GetLeadingCommentBlock(syntax);
        if (comments.Count == 0) {
            return null;
        }

        var sourceText = syntax.SyntaxTree.SourceText;

        var sb = new StringBuilder();
        foreach (var comment in comments) {
            AppendCommentText(sb, comment.ToString(sourceText));
        }

        var text = sb.ToString().Trim();
        return text.Length == 0 ? null : text;
    }

    /// <summary>
    /// Der Deklarations-Knoten, dessen Leading-Trivia die Kommentare trägt. Die Auflösung folgt
    /// derselben Logik wie die Signatur-Anzeige (<see cref="Pharmatechnik.Nav.Language.Text.DisplayPartsBuilder"/>),
    /// damit Doku und Signatur dasselbe Symbol dokumentieren: Aliase und Referenzen lösen auf ihren Knoten
    /// bzw. ihre Deklaration auf; ein Task-Knoten (<c>task Foo;</c>) auf die <b>Task-Definition</b>
    /// (deren Kommentar), nicht auf die Aufrufstelle; in-place deklarierte Knoten auf sich selbst.
    /// </summary>
    static SyntaxNode? GetDeclarationSyntax(ISymbol symbol) {
        return symbol switch {
            // Aliase auf ihren Knoten auflösen.
            ITaskNodeAliasSymbol { TaskNode: { } taskNode } => GetDeclarationSyntax(taskNode),
            IInitNodeAliasSymbol { InitNode: { } initNode } => GetDeclarationSyntax(initNode),

            // Referenz auf ihre Deklaration auflösen, dann erneut auflösen.
            INodeReferenceSymbol { Declaration: { } decl }  => GetDeclarationSyntax(decl),

            // Task-Knoten -> Kommentar der Task-Definition (nicht der Aufrufstelle). Kein Fallback auf
            // die Aufrufstelle: ist die Task-Syntax nicht verfügbar (cross-file taskref), gibt es bewusst
            // keine Doku statt einer falsch zugeordneten Aufrufstellen-Notiz.
            ITaskNodeSymbol taskNode                        => taskNode.Declaration?.Syntax,

            // In-place deklarierte Knoten (init/exit/end/choice/dialog/view).
            INodeSymbol node                                => node.Syntax,

            ITaskDefinitionSymbol task                      => task.Syntax,
            ITaskDeclarationSymbol { Syntax: { } declSyntax } => declSyntax,
            _                                               => null
        };
    }

    /// <summary>
    /// Der zusammenhängende Kommentarblock, der unmittelbar an den Knoten grenzt: gesammelt werden die
    /// letzten aufeinanderfolgenden Kommentarzeilen vor dem Knoten. Eine Leerzeile (zwei Zeilenenden in
    /// Folge) trennt einen vorangehenden Block ab; steht zwischen letztem Kommentar und Knoten eine
    /// Leerzeile, gehört der Block nicht mehr zum Knoten.
    /// </summary>
    static IReadOnlyList<SyntaxTrivia> GetLeadingCommentBlock(SyntaxNode syntax) {

        var block                = new List<SyntaxTrivia>();
        var newLinesSinceComment = 0;

        foreach (var trivia in syntax.GetLeadingTrivia()) {
            switch (trivia.Type) {
                case SyntaxTokenType.SingleLineComment:
                case SyntaxTokenType.MultiLineComment:
                    // Eine Leerzeile vor diesem Kommentar trennt einen vorigen Block ab — neu anfangen.
                    if (newLinesSinceComment >= 2) {
                        block.Clear();
                    }

                    block.Add(trivia);
                    newLinesSinceComment = 0;
                    break;

                case SyntaxTokenType.NewLine:
                    newLinesSinceComment++;
                    break;

                case SyntaxTokenType.Whitespace:
                    // Whitespace zählt nicht als Zeilengrenze.
                    break;

                default:
                    // Anderes (sollte in Trivia nicht vorkommen) bricht die Block-Adjazenz.
                    block.Clear();
                    newLinesSinceComment = 0;
                    break;
            }
        }

        // Trennt eine Leerzeile den letzten Kommentar vom Knoten, ist der Block nicht „direkt darüber".
        if (newLinesSinceComment >= 2) {
            block.Clear();
        }

        return block;
    }

    /// <summary>
    /// Hängt den von Kommentar-Markern befreiten Text einer Kommentar-Trivia an — eine Zeile je
    /// Quellzeile (mehrzeilige Block-Kommentare werden zeilenweise aufgeschlüsselt).
    /// </summary>
    static void AppendCommentText(StringBuilder sb, string raw) {

        raw = raw.Trim();

        if (raw.StartsWith("//")) {
            AppendLine(sb, raw.Substring(2).Trim());
            return;
        }

        if (raw.StartsWith("/*")) {
            var inner = raw.Substring(2);
            if (inner.EndsWith("*/")) {
                inner = inner.Substring(0, inner.Length - 2);
            }

            foreach (var line in inner.Split('\n')) {
                AppendLine(sb, line.Trim().TrimStart('*').Trim());
            }

            return;
        }

        AppendLine(sb, raw);
    }

    static void AppendLine(StringBuilder sb, string line) {
        if (sb.Length > 0) {
            sb.Append('\n');
        }

        sb.Append(line);
    }

}

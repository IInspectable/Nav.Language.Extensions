#nullable enable

#region Using Directives

using System;
using System.Linq;
using System.Collections.Generic;

using Pharmatechnik.Nav.Language.Text;

#endregion

namespace Pharmatechnik.Nav.Language.CodeFixes;

static class SyntaxTreeExtensions {

    public static IEnumerable<TextChange> GetRemoveSyntaxNodeChanges(this SyntaxTree syntaxTree, SyntaxNode syntaxNode, TextEditorSettings textEditorSettings) {

        var fullExtent = syntaxNode.GetFullExtent(onlyWhiteSpace: true);
        yield return TextChange.NewRemove(fullExtent);

        var lineExtent = syntaxTree.SourceText.GetTextLineAtPosition(fullExtent.End - 1).Extent;
        // Prinzipiell enthalten die TrailingTrivia auch das NL Token. Wenn wir aber nicht die einzige Syntax in der Zeile sind,
        // soll das NL erhalten bleiben. Deswegen schieben wir das durch den fullExtent gelöschte NL hier wieder ein.
        if (fullExtent.Start > lineExtent.Start && fullExtent.End == lineExtent.End) {
            yield return TextChange.NewInsert(lineExtent.End, textEditorSettings.NewLine);
        }
    }

    public static IEnumerable<TextChange> GetRenameSourceChanges(this SyntaxTree syntaxTree, ITransition transition, string newSourceName, TextEditorSettings textEditorSettings) {

        if (transition?.SourceReference == null) {
            yield break;
        }

        var replaceText     = newSourceName;
        var replaceLocation = transition.SourceReference.Location;

        var replaceExtent = replaceLocation.Extent;
        if (transition.EdgeMode != null && transition.SourceReference.Location.EndLine == transition.EdgeMode.Location.StartLine) {
            // Erste Nicht-Whitespace-Position nach dem Quellnamen (Kommentar oder Kante) — alles ab dort
            // bleibt erhalten, nur der Abstand zum Quellnamen wird neu gesetzt.
            var firstNonWhitespace = syntaxTree.FirstNonWhitespacePosition(TextExtent.FromBounds(replaceLocation.End, transition.EdgeMode.End));
            if (firstNonWhitespace != null) {
                var contentLocation = syntaxTree.SourceText.GetLocation(TextExtent.FromBounds(firstNonWhitespace.Value, firstNonWhitespace.Value));
                var availableSpace  = replaceLocation.Length + syntaxTree.SourceText.ColumnsBetweenLocations(replaceLocation, contentLocation, textEditorSettings);

                replaceExtent = TextExtent.FromBounds(replaceLocation.Start, firstNonWhitespace.Value);

                var spaces = Math.Max(1, availableSpace - newSourceName.Length);

                replaceText = newSourceName + new string(' ', spaces);
            }
        }

        yield return TextChange.NewReplace(replaceExtent, replaceText);
    }

    public static IEnumerable<TextChange> GetRenameSourceChanges(this SyntaxTree syntaxTree, IExitTransition transition, string newSourceName, TextEditorSettings textEditorSettings) {

        if (transition?.SourceReference == null || transition.ExitConnectionPointReference == null) {
            yield break;
        }

        var replaceText = $"{newSourceName}{SyntaxFacts.Colon}{transition.ExitConnectionPointReference.Name}";
        var replaceLocation = new Location(
            extent: TextExtent.FromBounds(transition.SourceReference.Start, transition.ExitConnectionPointReference.End),
            lineRange: new LineRange(
                start: transition.SourceReference.Location.StartLinePosition,
                end: transition.ExitConnectionPointReference.Location.EndLinePosition),
            filePath: transition.ExitConnectionPointReference.Location.FilePath);

        var replaceExtent = replaceLocation.Extent;
        if (transition.EdgeMode != null && transition.SourceReference.Location.EndLine == transition.EdgeMode.Location.StartLine) {
            // Erste Nicht-Whitespace-Position nach dem Quellnamen (Kommentar oder Kante) — alles ab dort
            // bleibt erhalten, nur der Abstand zum Quellnamen wird neu gesetzt.
            var firstNonWhitespace = syntaxTree.FirstNonWhitespacePosition(TextExtent.FromBounds(replaceLocation.End, transition.EdgeMode.End));
            if (firstNonWhitespace != null) {

                var contentLocation = syntaxTree.SourceText.GetLocation(TextExtent.FromBounds(firstNonWhitespace.Value, firstNonWhitespace.Value));
                var availableSpace  = replaceLocation.Length + syntaxTree.SourceText.ColumnsBetweenLocations(replaceLocation, contentLocation, textEditorSettings);

                replaceExtent = TextExtent.FromBounds(replaceLocation.Start, firstNonWhitespace.Value);

                var spaces = Math.Max(1, availableSpace - replaceText.Length);

                replaceText = replaceText + new string(' ', spaces);
            }
        }

        yield return TextChange.NewReplace(replaceExtent, replaceText);
    }

    public static string ComposeEdge(this SyntaxTree syntaxTree, IEdge templateEdge, string sourceName, string edgeKeyword, string targetName, TextEditorSettings textEditorSettings) {

        string indent = new string(' ', textEditorSettings.TabSize);
        if (templateEdge.SourceReference != null) {
            var templateEdgeLine = syntaxTree.SourceText.GetTextLineAtPosition(templateEdge.SourceReference.Start);
            indent = templateEdgeLine.GetIndentAsSpaces(textEditorSettings.TabSize);
        }

        var whiteSpaceBetweenSourceAndEdgeMode = syntaxTree.WhiteSpaceBetweenSourceAndEdgeMode(templateEdge, sourceName, textEditorSettings);
        var whiteSpaceBetweenEdgeModeAndTarget = syntaxTree.WhiteSpaceBetweenEdgeModeAndTarget(templateEdge, textEditorSettings);

        var exitTransition = $"{indent}{sourceName}{whiteSpaceBetweenSourceAndEdgeMode}{edgeKeyword}{whiteSpaceBetweenEdgeModeAndTarget}{targetName}{SyntaxFacts.Semicolon}";
        return exitTransition;
    }

    public static string WhiteSpaceBetweenSourceAndEdgeMode(this SyntaxTree syntaxTree, IEdge edge, string newSourceName, TextEditorSettings textEditorSettings) {

        if (edge.SourceReference == null || edge.EdgeMode == null) {
            return " ";
        }

        var oldOffset = syntaxTree.SourceText.ColumnsBetweenLocations(edge.SourceReference.Location, edge.EdgeMode.Location, textEditorSettings);

        var oldLength = edge.SourceReference.Location.Length;
        var newLength = newSourceName.Length;
        var offset    = Math.Max(1, oldOffset + oldLength - newLength);

        return new String(' ', offset);
    }

    public static string WhiteSpaceBetweenEdgeModeAndTarget(this SyntaxTree syntaxTree, IEdge edge, TextEditorSettings textEditorSettings) {

        if (edge.EdgeMode == null || edge.TargetReference == null) {
            return " ";
        }

        var offset = syntaxTree.SourceText.ColumnsBetweenLocations(edge.EdgeMode.Location, edge.TargetReference.Location, textEditorSettings);
        return new String(' ', offset);
    }

    /// <summary>
    /// Die Position des ersten Nicht-Whitespace-Inhalts im Bereich — der Beginn des ersten Kommentars
    /// (Kommentare liegen im angehängten Trivia-Modell als Trivia vor, nicht mehr als Strom-Token) oder,
    /// falls kein Kommentar vorausgeht, des ersten signifikanten Tokens. <c>null</c>, wenn der Bereich nur
    /// aus Whitespace/Zeilenende besteht.
    /// </summary>
    public static int? FirstNonWhitespacePosition(this SyntaxTree syntaxTree, TextExtent extent) {

        int? result = null;

        var token = syntaxTree.Tokens[extent]
                             .FirstOrDefault(t => !SyntaxFacts.IsTrivia(t.Type) && t.Type != SyntaxTokenType.EndOfFile);
        if (!token.IsMissing) {
            result = token.Start;
        }

        foreach (var trivia in syntaxTree.DescendantTrivia()) {
            if (trivia.Start >= extent.End) {
                break; // Trivia kommen aufsteigend — ab hier liegt nichts mehr im Bereich.
            }

            if (trivia.IsComment && trivia.Start >= extent.Start) {
                result = result == null ? trivia.Start : Math.Min(result.Value, trivia.Start);
                break;
            }
        }

        return result;
    }

    public static int ColumnsBetweenKeywordAndIdentifier(this SyntaxTree syntaxTree, INodeSymbol node, string? newKeyword, TextEditorSettings textEditorSettings) {

        var locations = KeywordAndIdentifierFinder.Find(node.Syntax);
        if (locations == null) {
            return 1;
        }

        var oldOffset = syntaxTree.SourceText.ColumnsBetweenLocations(locations.Item1, locations.Item2, textEditorSettings);

        var oldLength  = locations.Item1.Length;
        var newLength  = newKeyword?.Length ?? oldLength;
        var spaceCount = Math.Max(1, oldOffset + oldLength - newLength);

        return spaceCount;
    }

    sealed class KeywordAndIdentifierFinder: SyntaxNodeVisitor<Tuple<Location, Location>?> {

        public static Tuple<Location, Location>? Find(NodeDeclarationSyntax nodeDeclaration) {

            var finder = new KeywordAndIdentifierFinder();
            return finder.Visit(nodeDeclaration);
        }

        public override Tuple<Location, Location>? VisitChoiceNodeDeclaration(ChoiceNodeDeclarationSyntax choiceNodeDeclarationSyntax) {
            return SafeCreateTuple(choiceNodeDeclarationSyntax.ChoiceKeyword, choiceNodeDeclarationSyntax.Identifier);
        }

        public override Tuple<Location, Location>? VisitEndNodeDeclaration(EndNodeDeclarationSyntax endNodeDeclarationSyntax) {
            // End hat keinen Identifier
            return DefaultVisit(endNodeDeclarationSyntax);
        }

        public override Tuple<Location, Location>? VisitExitNodeDeclaration(ExitNodeDeclarationSyntax exitNodeDeclarationSyntax) {
            return SafeCreateTuple(exitNodeDeclarationSyntax.ExitKeyword, exitNodeDeclarationSyntax.Identifier);
        }

        public override Tuple<Location, Location>? VisitInitNodeDeclaration(InitNodeDeclarationSyntax initNodeDeclarationSyntax) {
            return SafeCreateTuple(initNodeDeclarationSyntax.InitKeyword, initNodeDeclarationSyntax.Identifier);
        }

        public override Tuple<Location, Location>? VisitDialogNodeDeclaration(DialogNodeDeclarationSyntax dialogNodeDeclarationSyntax) {
            return SafeCreateTuple(dialogNodeDeclarationSyntax.DialogKeyword, dialogNodeDeclarationSyntax.Identifier);
        }

        public override Tuple<Location, Location>? VisitTaskNodeDeclaration(TaskNodeDeclarationSyntax taskNodeDeclarationSyntax) {
            return SafeCreateTuple(taskNodeDeclarationSyntax.TaskKeyword, taskNodeDeclarationSyntax.Identifier);
        }

        public override Tuple<Location, Location>? VisitViewNodeDeclaration(ViewNodeDeclarationSyntax viewNodeDeclarationSyntax) {
            return SafeCreateTuple(viewNodeDeclarationSyntax.ViewKeyword, viewNodeDeclarationSyntax.Identifier);
        }

        Tuple<Location, Location>? SafeCreateTuple(SyntaxToken token1, SyntaxToken token2) {
            if (token1.IsMissing || token2.IsMissing) {
                return null;
            }

            // Nicht-fehlende Tokens haben ein Parent → SyntaxTree != null → GetLocation() ist non-null.
            return new Tuple<Location, Location>(token1.GetLocation()!, token2.GetLocation()!);
        }

    }

}
#region Using Directives

using System;
using System.Linq;
using System.Collections.Generic;

using Pharmatechnik.Nav.Language.Text;

#endregion

namespace Pharmatechnik.Nav.Language.CodeFixes;

/// <summary>
/// Interne Werkzeugkiste für <see cref="CodeFix"/>: Erweiterungsmethoden auf <see cref="SyntaxTree"/>, die
/// die wiederkehrenden Edit-Muster der Fixes berechnen (Knoten entfernen, Quell-/Ziel-Referenzen
/// umbenennen, eine Kante zusammensetzen, ausrichtende Whitespace-Abstände ermitteln). Alle Methoden
/// liefern nur Werte — <see cref="Pharmatechnik.Nav.Language.Text.TextChange"/>-Folgen bzw. Zeichenketten —
/// und mutieren nichts. Die <c>protected</c>-Helfer in <see cref="CodeFix"/> reichen an sie durch.
/// </summary>
static class SyntaxTreeExtensions {

    /// <summary>
    /// Liefert das Edit-Set, das den angegebenen Knoten samt umgebendem Whitespace entfernt. Ein Zeilenende,
    /// das durch den vollen Extent mitgelöscht würde, obwohl in der Zeile weiterer Inhalt steht, wird über
    /// eine zusätzliche Einfüge-Änderung wieder hergestellt — so bleibt das Zeilenende nur erhalten, wenn der
    /// Knoten nicht allein in seiner Zeile stand.
    /// </summary>
    /// <param name="syntaxTree">Der Syntaxbaum der Datei.</param>
    /// <param name="syntaxNode">Der zu entfernende Knoten.</param>
    /// <param name="textEditorSettings">Editor-Einstellungen (liefert u.a. das einzusetzende Zeilenende).</param>
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

    /// <summary>
    /// Liefert das Edit-Set, das den Quellnamen einer Transition durch <paramref name="newSourceName"/>
    /// ersetzt. Liegen Quellname und Kanten-Modus in derselben Zeile, wird der Abstand bis zum ersten
    /// nachfolgenden Nicht-Whitespace-Inhalt (Kommentar oder Kante) so nachgeführt, dass die
    /// Spalten-Ausrichtung erhalten bleibt (mindestens 1 Leerzeichen). Ohne Quell-Referenz ist das Edit-Set leer.
    /// </summary>
    /// <param name="syntaxTree">Der Syntaxbaum der Datei.</param>
    /// <param name="transition">Die betroffene Transition.</param>
    /// <param name="newSourceName">Der neue Quellname.</param>
    /// <param name="textEditorSettings">Editor-Einstellungen für die Spaltenberechnung.</param>
    public static IEnumerable<TextChange> GetRenameSourceChanges(this SyntaxTree syntaxTree, ITransition transition, string newSourceName, TextEditorSettings textEditorSettings) {

        if (transition.SourceReference == null) {
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

    /// <summary>
    /// Wie <see cref="GetRenameSourceChanges(SyntaxTree, ITransition, string, TextEditorSettings)"/>, jedoch
    /// für eine Exit-Transition: ersetzt Quellname samt Exit-Verbindungspunkt (Form
    /// <c>Quelle:Verbindungspunkt</c>) und führt bei Kante in derselben Zeile den Abstand nach. Fehlt die
    /// Quell- oder die Exit-Verbindungspunkt-Referenz, ist das Edit-Set leer.
    /// </summary>
    /// <param name="syntaxTree">Der Syntaxbaum der Datei.</param>
    /// <param name="transition">Die betroffene Exit-Transition.</param>
    /// <param name="newSourceName">Der neue Quellname.</param>
    /// <param name="textEditorSettings">Editor-Einstellungen für die Spaltenberechnung.</param>
    public static IEnumerable<TextChange> GetRenameSourceChanges(this SyntaxTree syntaxTree, IExitTransition transition, string newSourceName, TextEditorSettings textEditorSettings) {

        if (transition.SourceReference == null || transition.ExitConnectionPointReference == null) {
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

    /// <summary>
    /// Setzt den Quelltext einer vollständigen Kante zusammen: <c>Einrückung + Quellname + Abstand +
    /// Kanten-Schlüsselwort + Abstand + Zielname + Semikolon</c>. Einrückung und Abstände werden aus der
    /// <paramref name="templateEdge"/> abgeleitet (deren Zeilen-Einrückung bzw. Spalten-Ausrichtung); fehlt
    /// deren Quell-Referenz, wird als Einrückung eine Tabulatorbreite verwendet.
    /// </summary>
    /// <param name="syntaxTree">Der Syntaxbaum der Datei.</param>
    /// <param name="templateEdge">Die Vorlage-Kante für Einrückung und Abstände.</param>
    /// <param name="sourceName">Der Quellname.</param>
    /// <param name="edgeKeyword">Das Kanten-Schlüsselwort.</param>
    /// <param name="targetName">Der Zielname.</param>
    /// <param name="textEditorSettings">Editor-Einstellungen (Tabulatorbreite, Spaltenberechnung).</param>
    /// <returns>Der zusammengesetzte Kanten-Quelltext.</returns>
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

    /// <summary>
    /// Berechnet den ausrichtenden Whitespace zwischen Quellname und Kanten-Modus für einen um
    /// <paramref name="newSourceName"/> ersetzten Quellnamen: Der bisherige Abstand wird um die Längendifferenz
    /// alter/neuer Name korrigiert (mindestens 1 Leerzeichen), sodass der Kanten-Modus möglichst in derselben
    /// Spalte bleibt. Fehlt Quell-Referenz oder Kanten-Modus, wird ein einzelnes Leerzeichen geliefert.
    /// </summary>
    /// <param name="syntaxTree">Der Syntaxbaum der Datei.</param>
    /// <param name="edge">Die betroffene Kante.</param>
    /// <param name="newSourceName">Der neue Quellname.</param>
    /// <param name="textEditorSettings">Editor-Einstellungen für die Spaltenberechnung.</param>
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

    /// <summary>
    /// Liefert den Whitespace zwischen Kanten-Modus und Ziel, der die bisherige Spalten-Ausrichtung des Ziels
    /// beibehält. Fehlt Kanten-Modus oder Ziel-Referenz, wird ein einzelnes Leerzeichen geliefert.
    /// </summary>
    /// <param name="syntaxTree">Der Syntaxbaum der Datei.</param>
    /// <param name="edge">Die betroffene Kante.</param>
    /// <param name="textEditorSettings">Editor-Einstellungen für die Spaltenberechnung.</param>
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

    /// <summary>
    /// Berechnet die Anzahl Leerzeichen zwischen dem Knoten-Schlüsselwort (z.B. <c>task</c>, <c>view</c>) und
    /// dem Bezeichner, sodass beim Ersetzen des Schlüsselworts durch <paramref name="newKeyword"/> die
    /// Spalten-Ausrichtung des Bezeichners erhalten bleibt (mindestens 1). Ohne
    /// Schlüsselwort/Bezeichner-Paar (z.B. bei einem <c>end</c>-Knoten ohne Bezeichner oder fehlenden Token)
    /// wird 1 geliefert.
    /// </summary>
    /// <param name="syntaxTree">Der Syntaxbaum der Datei.</param>
    /// <param name="node">Der betroffene Knoten.</param>
    /// <param name="newKeyword">Das neue Schlüsselwort, oder <c>null</c>, um die bisherige Länge beizubehalten.</param>
    /// <param name="textEditorSettings">Editor-Einstellungen für die Spaltenberechnung.</param>
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

    /// <summary>
    /// Visitor, der zu einer Knoten-Deklaration das Paar (Schlüsselwort-Location, Bezeichner-Location)
    /// bestimmt — die Grundlage für <see cref="ColumnsBetweenKeywordAndIdentifier"/>. Liefert <c>null</c>,
    /// wenn eines der beiden Token fehlt oder der Knoten keinen Bezeichner besitzt (z.B. <c>end</c>).
    /// </summary>
    sealed class KeywordAndIdentifierFinder: SyntaxNodeVisitor<Tuple<Location, Location>?> {

        /// <summary>Ermittelt das Schlüsselwort-/Bezeichner-Location-Paar der Knoten-Deklaration, oder <c>null</c>.</summary>
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
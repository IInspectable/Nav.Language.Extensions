#region Using Directives

using System;
using System.Collections.Generic;

using Pharmatechnik.Nav.Language.Text;

#endregion

namespace Pharmatechnik.Nav.Language.CodeFixes; 

/// <summary>
/// Die abstrakte Basis aller Nav-Fixes — das VS-freie Gegenstück zu Roslyns <c>CodeAction</c>. Ein
/// <see cref="CodeFix"/> beschreibt eine anwendbare Quelltext-Änderung: Er hält seinen Eingabe-Kontext
/// (<see cref="Context"/>) und deklariert abstrakt seine Metadaten (<see cref="Name"/>,
/// <see cref="Impact"/>, <see cref="ApplicableTo"/>, <see cref="Prio"/>, <see cref="Category"/>). Ein Fix
/// <em>berechnet</em> nur eine Folge von <see cref="Pharmatechnik.Nav.Language.Text.TextChange"/> und
/// mutiert nichts selbst; das Anwenden übernimmt der jeweilige Host. Die drei Familien
/// <see cref="StyleFix.StyleCodeFix"/>, <see cref="ErrorFix.ErrorCodeFix"/> und
/// <see cref="Refactoring.RefactoringCodeFix"/> erben von dieser Basis. Die <c>protected</c>-Helfer bilden
/// wiederkehrende Edit-Muster (Entfernen, Einfügen, Umbenennen, Kanten-Aufbau, Whitespace-/Einrückungs-
/// Berechnung) und delegieren dazu meist an <see cref="SyntaxTreeExtensions"/>.
/// </summary>
public abstract class CodeFix {

    /// <summary>Initialisiert die Basis mit dem Eingabe-Kontext des Fixes.</summary>
    /// <param name="context">Der Kontext (Bereich, semantisches Modell, Editor-Einstellungen).</param>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> ist <c>null</c>.</exception>
    protected CodeFix(CodeFixContext context) {
        Context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <summary>Der Eingabe-Kontext dieses Fixes (Bereich, semantisches Modell, Editor-Einstellungen).</summary>
    public CodeFixContext Context { get; }

    /// <summary>Der dem Nutzer angezeigte Titel des Fixes (dient dem Host zugleich als Undo-Beschreibung).</summary>
    public abstract string          Name         { get; }
    /// <summary>Die Tragweite der Änderung (steuert z.B. das Warn-Icon des Vorschlags).</summary>
    public abstract CodeFixImpact   Impact       { get; }
    /// <summary>Der Quelltext-Bereich, für den der Fix gilt (Anker des Vorschlags), oder <c>null</c>, wenn nicht anwendbar.</summary>
    public abstract TextExtent?     ApplicableTo { get; }
    /// <summary>Die Priorität innerhalb des Vorschlags-Sets (höher = zuerst angeboten).</summary>
    public abstract CodeFixPrio     Prio         { get; }
    /// <summary>Die fachliche Familie des Fixes (steuert Gruppierung und Host-Abbildung, z.B. quickfix vs. refactor).</summary>
    public abstract CodeFixCategory Category     { get; }

    /// <summary>Das semantische Modell der Datei — durchgereicht aus dem <see cref="Context"/>.</summary>
    public CodeGenerationUnit       CodeGenerationUnit => Context.CodeGenerationUnit;
    /// <summary>Die Syntaxwurzel der Datei (aus der <see cref="CodeGenerationUnit"/>).</summary>
    public CodeGenerationUnitSyntax Syntax             => CodeGenerationUnit.Syntax;
    /// <summary>Der Syntaxbaum der Datei, gegen den die Edit-Helfer arbeiten.</summary>
    public SyntaxTree               SyntaxTree         => Syntax.SyntaxTree;

    /// <summary>
    /// Liefert das Edit-Set, das den angegebenen Bereich entfernt — eine einzelne Entfernen-Änderung, bzw.
    /// gar keine, wenn der Bereich fehlt (<see cref="TextExtent.IsMissing"/>).
    /// </summary>
    /// <param name="extent">Der zu entfernende Quelltext-Bereich.</param>
    /// <returns>Eine <see cref="Pharmatechnik.Nav.Language.Text.TextChange"/>-Folge (leer bei fehlendem Bereich).</returns>
    protected static IEnumerable<TextChange> GetRemoveChanges(TextExtent extent) {
        if (extent.IsMissing) {
            yield break;
        }

        yield return TextChange.NewRemove(extent);
    }

    /// <summary>
    /// Liefert das Edit-Set, das <paramref name="newText"/> an der angegebenen Position einfügt — bzw. gar
    /// keine Änderung, wenn <paramref name="newText"/> <c>null</c> ist.
    /// </summary>
    /// <param name="position">Die Einfügeposition im Quelltext.</param>
    /// <param name="newText">Der einzufügende Text, oder <c>null</c> für „keine Änderung".</param>
    /// <returns>Eine <see cref="Pharmatechnik.Nav.Language.Text.TextChange"/>-Folge (leer bei <c>null</c>-Text).</returns>
    protected static IEnumerable<TextChange> GetInsertChanges(int position, string? newText) {
        if (newText == null) {
            yield break;
        }

        yield return TextChange.NewInsert(position, newText);
    }

    /// <summary>
    /// Liefert das Edit-Set, das den angegebenen Syntaxknoten samt umgebendem Whitespace entfernt; ein
    /// alleinstehendes Zeilenende wird dabei bewahrt. Delegiert an
    /// <see cref="SyntaxTreeExtensions.GetRemoveSyntaxNodeChanges"/>.
    /// </summary>
    /// <param name="syntaxNode">Der zu entfernende Knoten.</param>
    protected IEnumerable<TextChange> GetRemoveSyntaxNodeChanges(SyntaxNode syntaxNode) {
        return SyntaxTree.GetRemoveSyntaxNodeChanges(syntaxNode, Context.TextEditorSettings);
    }

    /// <summary>
    /// Liefert das Edit-Set, das den Quellnamen einer Transition umbenennt und dabei den Abstand zur Kante
    /// erhält. Delegiert an
    /// <see cref="SyntaxTreeExtensions.GetRenameSourceChanges(SyntaxTree, ITransition, string, TextEditorSettings)"/>.
    /// </summary>
    /// <param name="transition">Die betroffene Transition.</param>
    /// <param name="newSourceName">Der neue Quellname.</param>
    protected IEnumerable<TextChange> GetRenameSourceChanges(ITransition transition, string newSourceName) {
        return SyntaxTree.GetRenameSourceChanges(transition, newSourceName, Context.TextEditorSettings);
    }

    /// <summary>
    /// Wie <see cref="GetRenameSourceChanges(ITransition, string)"/>, jedoch für eine Exit-Transition
    /// (Quellname samt Exit-Verbindungspunkt). Delegiert an
    /// <see cref="SyntaxTreeExtensions.GetRenameSourceChanges(SyntaxTree, IExitTransition, string, TextEditorSettings)"/>.
    /// </summary>
    /// <param name="transition">Die betroffene Exit-Transition.</param>
    /// <param name="newSourceName">Der neue Quellname.</param>
    protected IEnumerable<TextChange> GetRenameSourceChanges(IExitTransition transition, string newSourceName) {
        return SyntaxTree.GetRenameSourceChanges(transition, newSourceName, Context.TextEditorSettings);
    }

    /// <summary>
    /// Liefert das Edit-Set, das das Ziel-Symbol einer Kante umbenennt (Kurzform für
    /// <see cref="GetRenameSymbolChanges"/> auf <see cref="IEdge.TargetReference"/>).
    /// </summary>
    /// <param name="transition">Die Kante, deren Ziel umbenannt wird.</param>
    /// <param name="newSourceName">Der neue Zielname.</param>
    protected static IEnumerable<TextChange> GetRenameTargetChanges(IEdge transition, string newSourceName) {
        return GetRenameSymbolChanges(transition.TargetReference, newSourceName);
    }

    /// <summary>
    /// Liefert das Edit-Set, das den Namen des angegebenen Symbols an seiner Deklarations-/Referenzstelle
    /// ersetzt — bzw. gar keine Änderung, wenn das Symbol <c>null</c> ist oder bereits <paramref name="newName"/> heißt.
    /// </summary>
    /// <param name="symbol">Das umzubenennende Symbol, oder <c>null</c>.</param>
    /// <param name="newName">Der neue Name.</param>
    protected static IEnumerable<TextChange> GetRenameSymbolChanges(ISymbol? symbol, string newName) {
        if (symbol == null || symbol.Name == newName) {
            yield break;
        }

        yield return TextChange.NewReplace(symbol.Location.Extent, newName);
    }

    /// <summary>
    /// Setzt den Quelltext einer vollständigen Kante zusammen (Einrückung, Quellname, Kanten-Schlüsselwort,
    /// Ziel und abschließendes Semikolon), wobei sich Einrückung und Abstände an einer Vorlage-Kante
    /// orientieren. Delegiert an <see cref="SyntaxTreeExtensions.ComposeEdge"/>.
    /// </summary>
    /// <param name="templateEdge">Die Vorlage-Kante, aus der Einrückung und Abstände abgeleitet werden.</param>
    /// <param name="sourceName">Der Quellname.</param>
    /// <param name="edgeKeyword">Das Kanten-Schlüsselwort.</param>
    /// <param name="targetName">Der Zielname.</param>
    /// <returns>Der zusammengesetzte Kanten-Quelltext.</returns>
    protected string ComposeEdge(IEdge templateEdge, string sourceName, string edgeKeyword, string targetName) {
        return SyntaxTree.ComposeEdge(templateEdge, sourceName, edgeKeyword, targetName, Context.TextEditorSettings);
    }

    /// <summary>
    /// Berechnet den Whitespace zwischen Quellname und Kanten-Modus so, dass die Spalten-Ausrichtung des
    /// neuen Namens der bisherigen entspricht. Delegiert an
    /// <see cref="SyntaxTreeExtensions.WhiteSpaceBetweenSourceAndEdgeMode"/>.
    /// </summary>
    /// <param name="edge">Die betroffene Kante.</param>
    /// <param name="newSourceName">Der neue Quellname, dessen Länge in die Abstandsberechnung eingeht.</param>
    protected string WhiteSpaceBetweenSourceAndEdgeMode(IEdge edge, string newSourceName) {
        return SyntaxTree.WhiteSpaceBetweenSourceAndEdgeMode(edge, newSourceName, Context.TextEditorSettings);
    }

    /// <summary>
    /// Berechnet den Whitespace zwischen Kanten-Modus und Ziel, sodass die bisherige Spalten-Ausrichtung
    /// erhalten bleibt. Delegiert an <see cref="SyntaxTreeExtensions.WhiteSpaceBetweenEdgeModeAndTarget"/>.
    /// </summary>
    /// <param name="edge">Die betroffene Kante.</param>
    protected string WhiteSpaceBetweenEdgeModeAndTarget(IEdge edge) {
        return SyntaxTree.WhiteSpaceBetweenEdgeModeAndTarget(edge, Context.TextEditorSettings);
    }

    /// <summary>
    /// Berechnet die Anzahl Leerzeichen zwischen Knoten-Schlüsselwort und Bezeichner, sodass beim Ersetzen
    /// des Schlüsselworts die Spalten-Ausrichtung des Bezeichners erhalten bleibt (mindestens 1). Delegiert
    /// an <see cref="SyntaxTreeExtensions.ColumnsBetweenKeywordAndIdentifier"/>.
    /// </summary>
    /// <param name="node">Der betroffene Knoten.</param>
    /// <param name="newKeyword">Das neue Schlüsselwort, oder <c>null</c>, um die bisherige Länge beizubehalten.</param>
    protected int ColumnsBetweenKeywordAndIdentifier(INodeSymbol node, string? newKeyword = null) {
        return SyntaxTree.ColumnsBetweenKeywordAndIdentifier(node, newKeyword, Context.TextEditorSettings);
    }

    /// <summary>
    /// Liefert die führende Einrückung der angegebenen Quelltextzeile als Leerzeichen-Kette (Tabs anhand der
    /// <see cref="TextEditorSettings.TabSize"/> in Leerzeichen umgerechnet).
    /// </summary>
    /// <param name="sourceTextLine">Die Zeile, deren Einrückung ermittelt wird.</param>
    protected string GetIndentAsSpaces(SourceTextLine sourceTextLine) {
        return sourceTextLine.GetIndentAsSpaces(Context.TextEditorSettings.TabSize);
    }

}
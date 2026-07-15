#region Using Directives

using System;
using System.Linq;
using System.Collections.Generic;

using Pharmatechnik.Nav.Language.Text;

#endregion

namespace Pharmatechnik.Nav.Language.CodeFixes; 

/// <summary>
/// Der Eingabe-Kontext, mit dem ein <see cref="CodeFix"/> (und der ihn ermittelnde Provider) arbeitet:
/// bündelt den betrachteten Quelltext-Bereich (<see cref="Range"/>), das semantische Modell der Datei
/// (<see cref="CodeGenerationUnit"/>) und die <see cref="TextEditorSettings"/> (Einrückung, Zeilenende).
/// Die Such-Methoden (<see cref="FindSymbols(bool)"/>, <see cref="FindNodes{T}(bool)"/>,
/// <see cref="FindTokens"/>) grenzen Symbole, Knoten und Token auf diesen Bereich ein.
/// </summary>
public sealed class CodeFixContext {

    /// <summary>
    /// Erzeugt einen Kontext für den angegebenen Bereich innerhalb der übergebenen
    /// <see cref="Pharmatechnik.Nav.Language.CodeGenerationUnit"/>.
    /// </summary>
    /// <param name="range">Der betrachtete Quelltext-Bereich (z.B. Cursor-Position oder Auswahl).</param>
    /// <param name="codeGenerationUnit">Das semantische Modell der Datei, gegen das gesucht wird.</param>
    /// <param name="textEditorSettings">Editor-Einstellungen (Einrückung, Zeilenende) für die erzeugten Edits.</param>
    /// <exception cref="System.ArgumentNullException"><paramref name="codeGenerationUnit"/> oder
    /// <paramref name="textEditorSettings"/> ist <c>null</c>.</exception>
    /// <exception cref="System.ArgumentOutOfRangeException"><paramref name="range"/> reicht über das Ende
    /// des Quelltexts hinaus.</exception>
    public CodeFixContext(TextExtent range, CodeGenerationUnit codeGenerationUnit, TextEditorSettings textEditorSettings) {

        CodeGenerationUnit = codeGenerationUnit ?? throw new ArgumentNullException(nameof(codeGenerationUnit));
        TextEditorSettings = textEditorSettings ?? throw new ArgumentNullException(nameof(textEditorSettings));
        Range              = range;

        if (range.End > codeGenerationUnit.Syntax.SyntaxTree.SourceText.Length) {
            throw new ArgumentOutOfRangeException(nameof(range));
        }
    }

    /// <summary>Der betrachtete Quelltext-Bereich (Cursor-Position oder Auswahl), auf den sich der Fix bezieht.</summary>
    public TextExtent         Range              { get; }
    /// <summary>Das semantische Modell der Datei, gegen das die Such-Methoden auflösen.</summary>
    public CodeGenerationUnit CodeGenerationUnit { get; }
    /// <summary>Die für die erzeugten Edits maßgeblichen Editor-Einstellungen (Einrückung, Zeilenende).</summary>
    public TextEditorSettings TextEditorSettings { get; }

    /// <summary>
    /// Liefert die Symbole, die im Bereich <see cref="Range"/> liegen (aufgelöst über den Symbol-Index der
    /// <see cref="CodeGenerationUnit"/>).
    /// </summary>
    public IEnumerable<ISymbol> FindSymbols(bool includeOverlapping = false) {
        return CodeGenerationUnit.Symbols[Range];
    }

    /// <summary>
    /// Wie <see cref="FindSymbols(bool)"/>, aber gefiltert auf Symbole vom Typ <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">Der gesuchte Symboltyp.</typeparam>
    public IEnumerable<T> FindSymbols<T>(bool includeOverlapping = false) where T : ISymbol {
        return FindSymbols().OfType<T>();
    }

    /// <summary>
    /// Liefert die Syntaxknoten vom Typ <typeparamref name="T"/>, die den Token im Bereich
    /// <see cref="Range"/> als Parent zugeordnet sind.
    /// </summary>
    /// <typeparam name="T">Der gesuchte Knotentyp.</typeparam>
    /// <param name="includeOverlapping">Bei <c>false</c> (Standard) werden nur Knoten geliefert, deren
    /// Ausdehnung sich tatsächlich mit <see cref="Range"/> überschneidet; bei <c>true</c> auch die übrigen
    /// Kandidaten aus den Token-Parents.</param>
    public IEnumerable<T> FindNodes<T>(bool includeOverlapping = false) where T : SyntaxNode {
        var candidates = FindTokens().Select(t => t.Parent).OfType<T>();
        if (!includeOverlapping) {
            return candidates.Where(node => Range.IntersectsWith(node.Extent));
        }

        return candidates;
    }

    /// <summary>Liefert die Token, die im Bereich <see cref="Range"/> liegen.</summary>
    public IEnumerable<SyntaxToken> FindTokens() {
        return CodeGenerationUnit.Syntax.SyntaxTree.Tokens[Range];
    }

    /// <summary>
    /// Gibt an, ob im Bereich <see cref="Range"/> mindestens ein Knoten vom Typ <typeparamref name="T"/>
    /// liegt (siehe <see cref="FindNodes{T}(bool)"/>).
    /// </summary>
    /// <typeparam name="T">Der gesuchte Knotentyp.</typeparam>
    public bool ContainsNodes<T>() where T : SyntaxNode {
        return FindNodes<T>().Any();
    }

}
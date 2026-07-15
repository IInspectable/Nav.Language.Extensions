#region Using Directives

using System;
using System.Collections.Generic;

using Pharmatechnik.Nav.Language.Text;

#endregion

namespace Pharmatechnik.Nav.Language.CodeFixes.Refactoring; 

/// <summary>
/// Benennt einen Init-Knoten um. Da ein Init-Knoten nur über seinen <see cref="IInitNodeSymbol.Alias"/>
/// adressierbar ist, benennt der Fix — je nach Vorhandensein — entweder den bestehenden Alias um oder
/// fügt einen neuen Alias hinter dem <c>init</c>-Schlüsselwort ein.
/// </summary>
sealed class InitNodeRenameCodeFix: RenameCodeFix<IInitNodeSymbol> {

    internal InitNodeRenameCodeFix(IInitNodeSymbol initNodeAlias, ISymbol originatingSymbol, CodeFixContext context)
        : base(initNodeAlias, originatingSymbol, context) {
    }

    ITaskDefinitionSymbol ContainingTask => InitNode.ContainingTask;
    IInitNodeSymbol       InitNode       => Symbol;

    /// <summary>Der Anzeigename des Fixes: „Rename Init".</summary>
    public override string        Name   => "Rename Init";
    /// <summary>Immer <see cref="CodeFixImpact.None"/> — der Init-Alias bleibt Nav-intern.</summary>
    public override CodeFixImpact Impact => CodeFixImpact.None;

    /// <summary>
    /// Prüft <paramref name="symbolName"/> als neuen Init-Namen gegen den umgebenden Task. Der
    /// unveränderte Name gilt als zulässig (<c>null</c>).
    /// </summary>
    public override string? ValidateSymbolName(string? symbolName) {
        // De facto kein Rename, aber OK
        if (symbolName == InitNode.Name) {
            return null;
        }

        return ContainingTask.ValidateNewNodeName(symbolName);
    }

    /// <summary>
    /// Liefert die Änderungen: existiert bereits ein <see cref="IInitNodeSymbol.Alias"/>, wird dieser
    /// umbenannt, andernfalls hinter dem <c>init</c>-Schlüsselwort eingefügt. Zusätzlich werden die
    /// Quell-Referenzen der ausgehenden (<see cref="ISourceNodeSymbol.Outgoings"/>) Transitions angepasst.
    /// </summary>
    public override IEnumerable<TextChange> GetTextChanges(string? newName) {

        newName = newName?.Trim() ?? String.Empty;

        var validationMessage = ValidateSymbolName(newName);
        if (!String.IsNullOrEmpty(validationMessage)) {
            throw new ArgumentException(validationMessage, nameof(newName));
        }

        var textChanges = new List<TextChange>();

        if (InitNode.Alias != null) {
            // Alias umbenennen
            textChanges.AddRange(GetRenameSymbolChanges(InitNode.Alias, newName));
        } else {
            // Alias hinzufügen
            textChanges.AddRange(GetInsertChanges(InitNode.Syntax.InitKeyword.End, $" {newName}"));
        }

        // Die Choice-Referenzen auf der "linken Seite"
        foreach (var transition in InitNode.Outgoings) {
            var textChange = GetRenameSourceChanges(transition, newName);
            textChanges.AddRange(textChange);
        }

        return textChanges;
    }

}
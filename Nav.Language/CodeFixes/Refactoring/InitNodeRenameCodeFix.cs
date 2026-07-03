#region Using Directives

using System;
using System.Collections.Generic;

using Pharmatechnik.Nav.Language.Text;

#endregion

namespace Pharmatechnik.Nav.Language.CodeFixes.Refactoring; 

sealed class InitNodeRenameCodeFix: RenameCodeFix<IInitNodeSymbol> {

    internal InitNodeRenameCodeFix(IInitNodeSymbol initNodeAlias, ISymbol originatingSymbol, CodeFixContext context)
        : base(initNodeAlias, originatingSymbol, context) {
    }

    ITaskDefinitionSymbol ContainingTask => InitNode.ContainingTask;
    IInitNodeSymbol       InitNode       => Symbol;

    public override string        Name   => "Rename Init";
    public override CodeFixImpact Impact => CodeFixImpact.None;

    public override string? ValidateSymbolName(string? symbolName) {
        // De facto kein Rename, aber OK
        if (symbolName == InitNode.Name) {
            return null;
        }

        return ContainingTask.ValidateNewNodeName(symbolName);
    }

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
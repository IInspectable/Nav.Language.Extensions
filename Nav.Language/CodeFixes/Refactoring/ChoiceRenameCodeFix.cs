#nullable enable

#region Using Directives

using System;
using System.Collections.Generic;

using Pharmatechnik.Nav.Language.Text;

#endregion

namespace Pharmatechnik.Nav.Language.CodeFixes.Refactoring; 

sealed class ChoiceRenameCodeFix: RenameNodeCodeFix<IChoiceNodeSymbol> {

    internal ChoiceRenameCodeFix(IChoiceNodeSymbol choiceNodeSymbol, ISymbol originatingSymbol, CodeFixContext context)
        : base(choiceNodeSymbol, originatingSymbol, context) {
    }

    public IChoiceNodeSymbol ChoiceNodeSymbol => Symbol;

    public override string        Name   => "Rename Choice";
    public override CodeFixImpact Impact => CodeFixImpact.None;

    public override IEnumerable<TextChange> GetTextChanges(string? newName) {

        newName = newName?.Trim() ?? String.Empty;

        var validationMessage = ValidateSymbolName(newName);
        if (!String.IsNullOrEmpty(validationMessage)) {
            throw new ArgumentException(validationMessage, nameof(newName));
        }

        var textChanges = new List<TextChange>();
        // Die Choice Deklaration
        textChanges.AddRange(GetRenameSymbolChanges(ChoiceNodeSymbol, newName));

        // Die Choice-Referenzen auf der "linken Seite"
        foreach (var transition in ChoiceNodeSymbol.Outgoings) {
            var textChange = GetRenameSourceChanges(transition, newName);
            textChanges.AddRange(textChange);
        }

        // Die Choice-Referenzen auf der "rechten Seite"
        foreach (var transition in ChoiceNodeSymbol.Incomings) {
            var textChange = GetRenameTargetChanges(transition, newName);
            textChanges.AddRange(textChange);
        }

        return textChanges;
    }

}
#nullable enable

#region Using Directives

using System;
using System.Collections.Generic;

using Pharmatechnik.Nav.Language.Text;

#endregion

namespace Pharmatechnik.Nav.Language.CodeFixes.Refactoring; 

sealed class DialogNodeRenameCodeFix: RenameNodeCodeFix<IDialogNodeSymbol> {

    internal DialogNodeRenameCodeFix(IDialogNodeSymbol dialogNodeSymbol, ISymbol originatingSymbol, CodeFixContext context)
        : base(dialogNodeSymbol, originatingSymbol, context) {
    }

    public IDialogNodeSymbol DialogNode => Symbol;

    public override string        Name   => "Rename Dialog";
    public override CodeFixImpact Impact => CodeFixImpact.High;

    public override IEnumerable<TextChange> GetTextChanges(string? newName) {

        newName = newName?.Trim() ?? String.Empty;

        var validationMessage = ValidateSymbolName(newName);
        if (!String.IsNullOrEmpty(validationMessage)) {
            throw new ArgumentException(validationMessage, nameof(newName));
        }

        var textChanges = new List<TextChange>();
        // Die Dialog Node
        textChanges.AddRange(GetRenameSymbolChanges(DialogNode, newName));

        // Die Dialog-Referenzen auf der "linken Seite"
        foreach (var transition in DialogNode.Outgoings) {
            var textChange = GetRenameSourceChanges(transition, newName);
            textChanges.AddRange(textChange);
        }

        // Die Dialog-Referenzen auf der "rechten Seite"
        foreach (var transition in DialogNode.Incomings) {
            var textChange = GetRenameTargetChanges(transition, newName);
            textChanges.AddRange(textChange);
        }

        return textChanges;
    }

}
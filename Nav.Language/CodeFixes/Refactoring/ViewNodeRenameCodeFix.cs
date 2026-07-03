#nullable enable

#region Using Directives

using System;
using System.Collections.Generic;

using Pharmatechnik.Nav.Language.Text;

#endregion

namespace Pharmatechnik.Nav.Language.CodeFixes.Refactoring; 

sealed class ViewNodeRenameCodeFix: RenameNodeCodeFix<IViewNodeSymbol> {

    internal ViewNodeRenameCodeFix(IViewNodeSymbol viewNodeSymbol, ISymbol originatingSymbol, CodeFixContext context)
        : base(viewNodeSymbol, originatingSymbol, context) {
    }

    public IViewNodeSymbol ViewNode => Symbol;

    public override string        Name   => "Rename View";
    public override CodeFixImpact Impact => CodeFixImpact.High;

    public override IEnumerable<TextChange> GetTextChanges(string? newName) {

        newName = newName?.Trim() ?? String.Empty;

        var validationMessage = ValidateSymbolName(newName);
        if (!String.IsNullOrEmpty(validationMessage)) {
            throw new ArgumentException(validationMessage, nameof(newName));
        }

        var textChanges = new List<TextChange>();
        // Die Dialog Node
        textChanges.AddRange(GetRenameSymbolChanges(ViewNode, newName));

        // Die Dialog-Referenzen auf der "linken Seite"
        foreach (var transition in ViewNode.Outgoings) {
            var textChange = GetRenameSourceChanges(transition, newName);
            textChanges.AddRange(textChange);
        }

        // Die Dialog-Referenzen auf der "rechten Seite"
        foreach (var transition in ViewNode.Incomings) {
            var textChange = GetRenameTargetChanges(transition, newName);
            textChanges.AddRange(textChange);
        }

        return textChanges;
    }

}
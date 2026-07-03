#region Using Directives

using System;
using System.Collections.Generic;

using Pharmatechnik.Nav.Language.Text;

#endregion

namespace Pharmatechnik.Nav.Language.CodeFixes.Refactoring; 

sealed class ExitNodeRenameCodeFix: RenameNodeCodeFix<IExitNodeSymbol> {

    internal ExitNodeRenameCodeFix(IExitNodeSymbol exitNodeSymbol, ISymbol originatingSymbol, CodeFixContext context)
        : base(exitNodeSymbol, originatingSymbol, context) {
    }

    public IExitNodeSymbol ExitNode => Symbol;

    public override string        Name   => "Rename Exit";
    public override CodeFixImpact Impact => CodeFixImpact.High;

    public override IEnumerable<TextChange> GetTextChanges(string? newName) {

        newName = newName?.Trim() ?? String.Empty;

        var validationMessage = ValidateSymbolName(newName);
        if (!String.IsNullOrEmpty(validationMessage)) {
            throw new ArgumentException(validationMessage, nameof(newName));
        }

        var textChanges = new List<TextChange>();
        // Das Exit selbst
        textChanges.AddRange(GetRenameSymbolChanges(ExitNode, newName));

        // Die Exit-Referenzen auf der "rechten Seite"
        foreach (var transition in ExitNode.Incomings) {
            var textChange = GetRenameTargetChanges(transition, newName);
            textChanges.AddRange(textChange);
        }

        return textChanges;
    }

}
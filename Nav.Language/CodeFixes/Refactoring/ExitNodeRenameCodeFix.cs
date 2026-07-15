#region Using Directives

using System;
using System.Collections.Generic;

using Pharmatechnik.Nav.Language.Text;

#endregion

namespace Pharmatechnik.Nav.Language.CodeFixes.Refactoring; 

/// <summary>
/// Benennt einen Exit-Knoten samt Deklaration und aller eingehenden Transitions-Referenzen um. Ein Exit
/// ist ein Endknoten und hat daher nur eingehende, keine ausgehenden Transitions.
/// </summary>
sealed class ExitNodeRenameCodeFix: RenameNodeCodeFix<IExitNodeSymbol> {

    internal ExitNodeRenameCodeFix(IExitNodeSymbol exitNodeSymbol, ISymbol originatingSymbol, CodeFixContext context)
        : base(exitNodeSymbol, originatingSymbol, context) {
    }

    /// <summary>Der umzubenennende Exit-Knoten (das <see cref="RenameCodeFix{T}.Symbol"/>).</summary>
    public IExitNodeSymbol ExitNode => Symbol;

    /// <summary>Der Anzeigename des Fixes: „Rename Exit".</summary>
    public override string        Name   => "Rename Exit";
    /// <summary>
    /// <see cref="CodeFixImpact.High"/> — der Exit-Name prägt den generierten C#-Code, das Umbenennen
    /// wirkt sich daher über die Nav-Datei hinaus aus.
    /// </summary>
    public override CodeFixImpact Impact => CodeFixImpact.High;

    /// <summary>
    /// Liefert die Änderungen für die Exit-Deklaration sowie die Ziel-Referenzen der eingehenden
    /// (<see cref="ITargetNodeSymbol.Incomings"/>) Transitions.
    /// </summary>
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
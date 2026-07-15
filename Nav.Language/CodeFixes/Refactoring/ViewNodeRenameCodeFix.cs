#region Using Directives

using System;
using System.Collections.Generic;

using Pharmatechnik.Nav.Language.Text;

#endregion

namespace Pharmatechnik.Nav.Language.CodeFixes.Refactoring; 

/// <summary>
/// Benennt einen View-Knoten samt Deklaration und aller ein- und ausgehenden Transitions-Referenzen um.
/// </summary>
sealed class ViewNodeRenameCodeFix: RenameNodeCodeFix<IViewNodeSymbol> {

    internal ViewNodeRenameCodeFix(IViewNodeSymbol viewNodeSymbol, ISymbol originatingSymbol, CodeFixContext context)
        : base(viewNodeSymbol, originatingSymbol, context) {
    }

    /// <summary>Der umzubenennende View-Knoten (das <see cref="RenameCodeFix{T}.Symbol"/>).</summary>
    public IViewNodeSymbol ViewNode => Symbol;

    /// <summary>Der Anzeigename des Fixes: „Rename View".</summary>
    public override string        Name   => "Rename View";
    /// <summary>
    /// <see cref="CodeFixImpact.High"/> — der View-Name prägt den generierten C#-Code, das Umbenennen
    /// wirkt sich daher über die Nav-Datei hinaus aus.
    /// </summary>
    public override CodeFixImpact Impact => CodeFixImpact.High;

    /// <summary>
    /// Liefert die Änderungen für die View-Deklaration sowie die Quell-Referenzen der ausgehenden
    /// (<see cref="ISourceNodeSymbol.Outgoings"/>) und die Ziel-Referenzen der eingehenden
    /// (<see cref="ITargetNodeSymbol.Incomings"/>) Transitions.
    /// </summary>
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
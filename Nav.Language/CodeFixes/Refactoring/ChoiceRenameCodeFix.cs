#region Using Directives

using System;
using System.Collections.Generic;

using Pharmatechnik.Nav.Language.Text;

#endregion

namespace Pharmatechnik.Nav.Language.CodeFixes.Refactoring; 

/// <summary>
/// Benennt einen Choice-Knoten samt seiner Deklaration und aller ein- und ausgehenden
/// Transitions-Referenzen um.
/// </summary>
sealed class ChoiceRenameCodeFix: RenameNodeCodeFix<IChoiceNodeSymbol> {

    internal ChoiceRenameCodeFix(IChoiceNodeSymbol choiceNodeSymbol, ISymbol originatingSymbol, CodeFixContext context)
        : base(choiceNodeSymbol, originatingSymbol, context) {
    }

    /// <summary>Der umzubenennende Choice-Knoten (das <see cref="RenameCodeFix{T}.Symbol"/>).</summary>
    public IChoiceNodeSymbol ChoiceNodeSymbol => Symbol;

    /// <summary>Der Anzeigename des Fixes: „Rename Choice".</summary>
    public override string        Name   => "Rename Choice";
    /// <summary>Immer <see cref="CodeFixImpact.None"/> — ein Choice ist rein Nav-intern, ohne C#-Auswirkung.</summary>
    public override CodeFixImpact Impact => CodeFixImpact.None;

    /// <summary>
    /// Liefert die Änderungen für die Choice-Deklaration sowie die Quell-Referenzen der ausgehenden
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
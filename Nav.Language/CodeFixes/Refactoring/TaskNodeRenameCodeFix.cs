#region Using Directives

using System;
using System.Collections.Generic;

using Pharmatechnik.Nav.Language.Text;

#endregion

namespace Pharmatechnik.Nav.Language.CodeFixes.Refactoring; 

/// <summary>
/// Benennt einen Task-Knoten (die Verwendung einer Task innerhalb einer anderen) über seinen
/// <see cref="ITaskNodeSymbol.Alias"/> um. Existiert kein Alias, wird einer hinter dem
/// Task-Bezeichner eingefügt; die referenzierte Task-Deklaration selbst bleibt unberührt.
/// </summary>
sealed class TaskNodeRenameCodeFix: RenameCodeFix<ITaskNodeSymbol> {

    internal TaskNodeRenameCodeFix(ITaskNodeSymbol taskNode, ISymbol originatingSymbol, CodeFixContext context)
        : base(taskNode, originatingSymbol, context) {
    }

    /// <summary>Die Task-Definition, in der der Task-Knoten liegt.</summary>
    public ITaskDefinitionSymbol ContainingTask => TaskNode.ContainingTask;
    /// <summary>Der umzubenennende Task-Knoten (das <see cref="RenameCodeFix{T}.Symbol"/>).</summary>
    public ITaskNodeSymbol       TaskNode       => Symbol;

    /// <summary>Der Anzeigename des Fixes: „Rename Task Node".</summary>
    public override string        Name   => "Rename Task Node";
    /// <summary><see cref="CodeFixImpact.Medium"/> — betrifft die lokale Verwendung, nicht die Task-Deklaration.</summary>
    public override CodeFixImpact Impact => CodeFixImpact.Medium;

    /// <summary>
    /// Prüft <paramref name="symbolName"/> als neuen Knotennamen gegen den umgebenden Task. Der
    /// unveränderte Name gilt als zulässig (<c>null</c>).
    /// </summary>
    public override string? ValidateSymbolName(string? symbolName) {
        // De facto kein Rename, aber OK
        if (symbolName == TaskNode.Name) {
            return null;
        }

        return ContainingTask.ValidateNewNodeName(symbolName);
    }

    /// <summary>
    /// Liefert die Änderungen: existiert bereits ein <see cref="ITaskNodeSymbol.Alias"/>, wird dieser
    /// umbenannt, andernfalls hinter dem Task-Bezeichner eingefügt. Zusätzlich werden die Quell-Referenzen
    /// der ausgehenden (<see cref="ISourceNodeSymbol.Outgoings"/>) und die Ziel-Referenzen der eingehenden
    /// (<see cref="ITargetNodeSymbol.Incomings"/>) Transitions angepasst.
    /// </summary>
    public override IEnumerable<TextChange> GetTextChanges(string? newName) {

        newName = newName?.Trim() ?? String.Empty;

        var validationMessage = ValidateSymbolName(newName);
        if (!String.IsNullOrEmpty(validationMessage)) {
            throw new ArgumentException(validationMessage, nameof(newName));
        }

        var textChanges = new List<TextChange>();

        if (TaskNode.Alias != null) {
            // Alias umbenennen
            textChanges.AddRange(GetRenameSymbolChanges(TaskNode.Alias, newName));
        } else {
            // Alias hinzufügen
            textChanges.AddRange(GetInsertChanges(TaskNode.Syntax.Identifier.End, $" {newName}"));
        }

        // Die Task-Referenzen auf der "linken Seite"
        foreach (var transition in TaskNode.Outgoings) {
            var textChange = GetRenameSourceChanges(transition, newName);
            textChanges.AddRange(textChange);
        }

        // Die Task-Referenzen auf der "rechten Seite"
        foreach (var transition in TaskNode.Incomings) {
            var textChange = GetRenameTargetChanges(transition, newName);
            textChanges.AddRange(textChange);
        }

        return textChanges;
    }

}
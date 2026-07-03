#nullable enable

#region Using Directives

using System;
using System.Collections.Generic;

using Pharmatechnik.Nav.Language.Text;

#endregion

namespace Pharmatechnik.Nav.Language.CodeFixes.Refactoring; 

sealed class TaskNodeRenameCodeFix: RenameCodeFix<ITaskNodeSymbol> {

    internal TaskNodeRenameCodeFix(ITaskNodeSymbol taskNode, ISymbol originatingSymbol, CodeFixContext context)
        : base(taskNode, originatingSymbol, context) {
    }

    public ITaskDefinitionSymbol ContainingTask => TaskNode.ContainingTask;
    public ITaskNodeSymbol       TaskNode       => Symbol;

    public override string        Name   => "Rename Task Node";
    public override CodeFixImpact Impact => CodeFixImpact.Medium;

    public override string? ValidateSymbolName(string? symbolName) {
        // De facto kein Rename, aber OK
        if (symbolName == TaskNode.Name) {
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
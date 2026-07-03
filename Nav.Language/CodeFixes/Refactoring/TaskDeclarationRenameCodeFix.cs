#nullable enable

#region Using Directives

using System;
using System.Collections.Generic;
using System.Linq;

using Pharmatechnik.Nav.Language.Text;

#endregion

namespace Pharmatechnik.Nav.Language.CodeFixes.Refactoring; 

sealed class TaskDeclarationRenameCodeFix: RenameCodeFix<ITaskDeclarationSymbol> {

    internal TaskDeclarationRenameCodeFix(ITaskDeclarationSymbol taskDeclarationSymbol, ISymbol originatingSymbol, CodeFixContext context)
        : base(taskDeclarationSymbol, originatingSymbol, context) {
    }

    public ITaskDeclarationSymbol TaskDeclaration => Symbol;

    public override string        Name   => "Rename Task";
    public override CodeFixImpact Impact => CodeFixImpact.High;

    public override string? ValidateSymbolName(string? symbolName) {
        // De facto kein Rename, aber OK
        if (symbolName == TaskDeclaration.Name) {
            return null;
        }

        symbolName = symbolName?.Trim();

        if (!SyntaxFacts.IsValidIdentifier(symbolName)) {
            return DiagnosticDescriptors.Semantic.Nav2000IdentifierExpected.MessageFormat;
        }

        var declaredNames = CodeGenerationUnit.TaskDeclarations.Select(td => td.Name);
        if (declaredNames.Contains(symbolName)) {
            return String.Format(DiagnosticDescriptors.Semantic.Nav0020TaskWithName0AlreadyDeclared.MessageFormat, symbolName);
        }

        return null;
    }

    public override IEnumerable<TextChange> GetTextChanges(string? newName) {

        newName = newName?.Trim() ?? String.Empty;

        var validationMessage = ValidateSymbolName(newName);
        if (!String.IsNullOrEmpty(validationMessage)) {
            throw new ArgumentException(validationMessage, nameof(newName));
        }

        var textChanges = new List<TextChange>();
        // Die Declaration selbst
        textChanges.AddRange(GetRenameSymbolChanges(TaskDeclaration, newName));

        foreach (var taskNode in TaskDeclaration.References) {

            // Die Task Node selbst
            textChanges.AddRange(GetRenameSymbolChanges(taskNode, newName));

            // Wenn der Knoten einen Alias hat, dann sind wir hier fertig
            if (taskNode.Alias != null) {
                continue;
            }

            // Die Task-Referenzen auf der "linken Seite"
            foreach (var transition in taskNode.Outgoings) {
                var textChange = GetRenameSourceChanges(transition, newName);
                textChanges.AddRange(textChange);
            }

            // Die Task-Referenzen auf der "rechten Seite"
            foreach (var transition in taskNode.Incomings) {
                var textChange = GetRenameTargetChanges(transition, newName);
                textChanges.AddRange(textChange);
            }
        }

        return textChanges;
    }

}
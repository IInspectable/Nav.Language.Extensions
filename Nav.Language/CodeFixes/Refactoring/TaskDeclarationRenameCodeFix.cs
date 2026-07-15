#region Using Directives

using System;
using System.Collections.Generic;
using System.Linq;

using Pharmatechnik.Nav.Language.Text;

#endregion

namespace Pharmatechnik.Nav.Language.CodeFixes.Refactoring; 

/// <summary>
/// Benennt eine Task-Deklaration samt aller ihrer Verwendungen um: die Deklaration selbst, jeden
/// referenzierenden Task-Knoten und — sofern dieser keinen eigenen Alias trägt — dessen ein- und
/// ausgehende Transitions-Referenzen.
/// </summary>
sealed class TaskDeclarationRenameCodeFix: RenameCodeFix<ITaskDeclarationSymbol> {

    internal TaskDeclarationRenameCodeFix(ITaskDeclarationSymbol taskDeclarationSymbol, ISymbol originatingSymbol, CodeFixContext context)
        : base(taskDeclarationSymbol, originatingSymbol, context) {
    }

    /// <summary>Die umzubenennende Task-Deklaration (das <see cref="RenameCodeFix{T}.Symbol"/>).</summary>
    public ITaskDeclarationSymbol TaskDeclaration => Symbol;

    /// <summary>Der Anzeigename des Fixes: „Rename Task".</summary>
    public override string        Name   => "Rename Task";
    /// <summary>
    /// <see cref="CodeFixImpact.High"/> — der Task-Name prägt den generierten C#-Code, das Umbenennen
    /// wirkt sich daher über die Nav-Datei hinaus aus.
    /// </summary>
    public override CodeFixImpact Impact => CodeFixImpact.High;

    /// <summary>
    /// Prüft <paramref name="symbolName"/> als neuen Task-Namen: er muss ein gültiger Bezeichner
    /// (sonst <see cref="DiagnosticDescriptors.Semantic"/> <c>Nav2000</c>) und im Dokument noch nicht
    /// vergeben sein (sonst <c>Nav0020</c>). Der unveränderte Name gilt als zulässig (<c>null</c>).
    /// </summary>
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

    /// <summary>
    /// Liefert die Änderungen für die Deklaration sowie für jeden referenzierenden Task-Knoten
    /// (<see cref="ITaskDeclarationSymbol.References"/>); trägt ein Knoten keinen eigenen Alias, werden
    /// zusätzlich die Quell-Referenzen seiner ausgehenden (<see cref="ISourceNodeSymbol.Outgoings"/>) und die
    /// Ziel-Referenzen seiner eingehenden (<see cref="ITargetNodeSymbol.Incomings"/>) Transitions angepasst.
    /// </summary>
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
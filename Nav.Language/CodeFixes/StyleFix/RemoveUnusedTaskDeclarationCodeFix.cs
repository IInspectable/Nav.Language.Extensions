#nullable enable

#region Using Directives

using System;
using System.Collections.Generic;
using System.Linq;

using Pharmatechnik.Nav.Language.Text;

#endregion

namespace Pharmatechnik.Nav.Language.CodeFixes.StyleFix; 

public class RemoveUnusedTaskDeclarationCodeFix: StyleCodeFix {

    internal RemoveUnusedTaskDeclarationCodeFix(ITaskDeclarationSymbol taskDeclarationSymbol, CodeFixContext context)
        : base(context) {
        TaskDeclaration = taskDeclarationSymbol ?? throw new ArgumentNullException(nameof(taskDeclarationSymbol));
    }

    public ITaskDeclarationSymbol TaskDeclaration { get; }

    public override string        Name         => "Remove Unused Task Declaration";
    public override CodeFixImpact Impact       => CodeFixImpact.None;
    public override TextExtent?   ApplicableTo => null;
    public override CodeFixPrio   Prio         => CodeFixPrio.Medium;

    internal bool CanApplyFix() {
        return TaskDeclaration.References.Count == 0                                     &&
               TaskDeclaration.Syntax           != null                                  &&
               TaskDeclaration.Origin           == TaskDeclarationOrigin.TaskDeclaration &&
               TaskDeclaration.IsIncluded       == false;
    }

    public override IList<TextChange> GetTextChanges() {
        if (!CanApplyFix()) {
            throw new InvalidOperationException();
        }

        var textChanges = new List<TextChange?>();
        // CanApplyFix (oben geprüft) garantiert TaskDeclaration.Syntax != null.
        foreach (var textChange in GetRemoveSyntaxNodeChanges(TaskDeclaration.Syntax!)) {
            textChanges.Add(textChange);
        }

        return textChanges.OfType<TextChange>().ToList();
    }

}
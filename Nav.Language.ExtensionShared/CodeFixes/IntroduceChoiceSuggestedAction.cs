#region Using Directives

using System;
using System.Threading;

using Microsoft.VisualStudio.Imaging.Interop;

using Pharmatechnik.Nav.Language.CodeFixes.Refactoring;
using Pharmatechnik.Nav.Language.Extension.Images;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.CodeFixes; 

class IntroduceChoiceSuggestedAction : CodeFixSuggestedAction<IntroduceChoiceCodeFix> {

    public IntroduceChoiceSuggestedAction(IntroduceChoiceCodeFix codeFix,
                                          CodeFixSuggestedActionParameter parameter,
                                          CodeFixSuggestedActionContext context)
        : base(context, parameter, codeFix) {
    }

    public override ImageMoniker IconMoniker => ImageMonikers.InsertNode;
    public override string       DisplayText => CodeFix.Name;

    protected override void Apply(CancellationToken cancellationToken) {

        var choiceName = Context.DialogService.ShowInputDialog(
            promptText    : "Name:",
            title         : CodeFix.Name,
            defaultResonse: CodeFix.SuggestChoiceName(),
            iconMoniker   : ImageMonikers.ChoiceNode,
            validator     : CodeFix.ValidateChoiceName
        )?.Trim();

        if (String.IsNullOrEmpty(choiceName)) {
            return;
        }

        ApplyTextChanges(CodeFix.GetTextChanges(choiceName));

        // TODO Selection Logik?
    }          
}
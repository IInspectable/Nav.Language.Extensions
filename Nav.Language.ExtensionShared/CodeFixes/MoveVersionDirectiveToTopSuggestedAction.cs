#region Using Directives

using System.Threading;

using Microsoft.VisualStudio.Imaging.Interop;

using Pharmatechnik.Nav.Language.CodeFixes.ErrorFix;
using Pharmatechnik.Nav.Language.Extension.Images;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.CodeFixes;

class MoveVersionDirectiveToTopSuggestedAction: CodeFixSuggestedAction<MoveVersionDirectiveToTopCodeFix> {

    public MoveVersionDirectiveToTopSuggestedAction(MoveVersionDirectiveToTopCodeFix codeFix,
                                                    CodeFixSuggestedActionParameter parameter,
                                                    CodeFixSuggestedActionContext context)
        : base(context, parameter, codeFix) {
    }

    public override ImageMoniker IconMoniker => ImageMonikers.MoveDirectiveToTop;
    public override string       DisplayText => CodeFix.Name;

    protected override void Apply(CancellationToken cancellationToken) {

        ApplyTextChanges(CodeFix.GetTextChanges());
    }

}

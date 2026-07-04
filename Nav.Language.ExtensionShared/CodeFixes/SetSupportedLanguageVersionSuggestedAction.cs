#region Using Directives

using System.Threading;

using Microsoft.VisualStudio.Imaging.Interop;

using Pharmatechnik.Nav.Language.CodeFixes.ErrorFix;
using Pharmatechnik.Nav.Language.Extension.Images;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.CodeFixes;

class SetSupportedLanguageVersionSuggestedAction: CodeFixSuggestedAction<SetSupportedLanguageVersionCodeFix> {

    public SetSupportedLanguageVersionSuggestedAction(SetSupportedLanguageVersionCodeFix codeFix,
                                                      CodeFixSuggestedActionParameter parameter,
                                                      CodeFixSuggestedActionContext context)
        : base(context, parameter, codeFix) {
    }

    public override ImageMoniker IconMoniker => ImageMonikers.SetLanguageVersion;
    public override string       DisplayText => CodeFix.Name;

    protected override void Apply(CancellationToken cancellationToken) {

        ApplyTextChanges(CodeFix.GetTextChanges());
    }

}

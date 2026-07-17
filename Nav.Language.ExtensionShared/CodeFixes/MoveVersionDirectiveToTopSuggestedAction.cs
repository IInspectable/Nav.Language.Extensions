#region Using Directives

using System.Threading;

using Microsoft.VisualStudio.Imaging.Interop;

using Pharmatechnik.Nav.Language.CodeFixes.ErrorFix;
using Pharmatechnik.Nav.Language.Extension.Images;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.CodeFixes;

/// <summary>
/// VS-Lightbulb-Aktion (Suggested Action) für den Engine-CodeFix
/// <see cref="MoveVersionDirectiveToTopCodeFix"/>: behebt eine deplatzierte <c>#version</c>-Direktive
/// (<c>Nav3003</c>), indem sie diese an den Dateikopf verschiebt bzw. — falls dort bereits eine wirksame
/// Direktive steht — entfernt. Angeboten wird die Aktion vom
/// <see cref="MoveVersionDirectiveToTopSuggestedActionProvider"/>.
/// </summary>
class MoveVersionDirectiveToTopSuggestedAction: CodeFixSuggestedAction<MoveVersionDirectiveToTopCodeFix> {

    public MoveVersionDirectiveToTopSuggestedAction(MoveVersionDirectiveToTopCodeFix codeFix,
                                                    CodeFixSuggestedActionParameter parameter,
                                                    CodeFixSuggestedActionContext context)
        : base(context, parameter, codeFix) {
    }

    /// <summary>Das Lightbulb-Icon der Aktion — <see cref="ImageMonikers.MoveDirectiveToTop"/>.</summary>
    public override ImageMoniker IconMoniker => ImageMonikers.MoveDirectiveToTop;
    /// <summary>Der in der Lightbulb angezeigte Text — der Anzeigename des Fixes
    /// (<see cref="MoveVersionDirectiveToTopCodeFix.Name"/>).</summary>
    public override string       DisplayText => CodeFix.Name;

    /// <summary>Wendet die vom Fix berechneten Textänderungen auf den Editor-Puffer an.</summary>
    /// <param name="cancellationToken">Token zum Abbrechen der Ausführung.</param>
    protected override void Apply(CancellationToken cancellationToken) {

        ApplyTextChanges(CodeFix.GetTextChanges());
    }

}

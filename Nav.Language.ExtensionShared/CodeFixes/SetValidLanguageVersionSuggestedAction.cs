#region Using Directives

using System.Threading;

using Microsoft.VisualStudio.Imaging.Interop;

using Pharmatechnik.Nav.Language.CodeFixes.ErrorFix;
using Pharmatechnik.Nav.Language.Extension.Images;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.CodeFixes;

/// <summary>
/// VS-Lightbulb-Aktion (Suggested Action) für den Engine-CodeFix
/// <see cref="SetValidLanguageVersionCodeFix"/>: ersetzt einen fehlenden oder ungültigen Versionswert einer
/// wirksamen <c>#version</c>-Direktive (<c>Nav3002</c>) durch die höchste unterstützte Version. Angeboten
/// wird die Aktion vom <see cref="SetValidLanguageVersionSuggestedActionProvider"/>.
/// </summary>
class SetValidLanguageVersionSuggestedAction: CodeFixSuggestedAction<SetValidLanguageVersionCodeFix> {

    public SetValidLanguageVersionSuggestedAction(SetValidLanguageVersionCodeFix codeFix,
                                                  CodeFixSuggestedActionParameter parameter,
                                                  CodeFixSuggestedActionContext context)
        : base(context, parameter, codeFix) {
    }

    /// <summary>Das Lightbulb-Icon der Aktion — <see cref="ImageMonikers.SetLanguageVersion"/>.</summary>
    public override ImageMoniker IconMoniker => ImageMonikers.SetLanguageVersion;
    /// <summary>Der in der Lightbulb angezeigte Text — der Anzeigename des Fixes
    /// (<see cref="SetValidLanguageVersionCodeFix.Name"/>).</summary>
    public override string       DisplayText => CodeFix.Name;

    /// <summary>Wendet die vom Fix berechneten Textänderungen auf den Editor-Puffer an.</summary>
    /// <param name="cancellationToken">Token zum Abbrechen der Ausführung.</param>
    protected override void Apply(CancellationToken cancellationToken) {

        ApplyTextChanges(CodeFix.GetTextChanges());
    }

}

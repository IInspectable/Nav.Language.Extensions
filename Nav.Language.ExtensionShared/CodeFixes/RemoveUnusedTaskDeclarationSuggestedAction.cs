#region Using Directives

using System.Threading;

using Microsoft.VisualStudio.Imaging.Interop;

using Pharmatechnik.Nav.Language.CodeFixes.StyleFix;
using Pharmatechnik.Nav.Language.Extension.Images;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.CodeFixes; 

/// <summary>
/// VS-Lightbulb-Aktion (Suggested Action) für den Engine-Stil-CodeFix
/// <see cref="RemoveUnusedTaskDeclarationCodeFix"/>: entfernt eine ungenutzte Task-Deklaration
/// (<c>taskref</c>), auf die es keine einzige Referenz gibt. Angeboten wird die Aktion vom
/// <see cref="RemoveUnusedTaskDeclarationSuggestedActionProvider"/>.
/// </summary>
class RemoveUnusedTaskDeclarationSuggestedAction : CodeFixSuggestedAction<RemoveUnusedTaskDeclarationCodeFix> {

    public RemoveUnusedTaskDeclarationSuggestedAction(RemoveUnusedTaskDeclarationCodeFix codeFix,
                                                      CodeFixSuggestedActionParameter parameter,
                                                      CodeFixSuggestedActionContext context)
        : base(context, parameter, codeFix) {
    }

    /// <summary>Das Lightbulb-Icon der Aktion — <see cref="ImageMonikers.RemoveUnusedSymbol"/>.</summary>
    public override ImageMoniker IconMoniker => ImageMonikers.RemoveUnusedSymbol;
    /// <summary>Der in der Lightbulb angezeigte Text — der Anzeigename des Fixes
    /// (<see cref="RemoveUnusedTaskDeclarationCodeFix.Name"/>).</summary>
    public override string       DisplayText => CodeFix.Name;

    /// <summary>Wendet die vom Fix berechneten Textänderungen auf den Editor-Puffer an.</summary>
    /// <param name="cancellationToken">Token zum Abbrechen der Ausführung.</param>
    protected override void Apply(CancellationToken cancellationToken) {

        ApplyTextChanges(CodeFix.GetTextChanges());                
    }
}
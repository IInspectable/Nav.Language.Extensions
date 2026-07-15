#region Using Directives

using System.Threading;

using Microsoft.VisualStudio.Imaging.Interop;
using Microsoft.VisualStudio.Shell;

using Pharmatechnik.Nav.Language.CodeFixes.ErrorFix;
using Pharmatechnik.Nav.Language.Extension.Common;
using Pharmatechnik.Nav.Language.Extension.Images;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.CodeFixes; 

/// <summary>
/// VS-Lightbulb-Aktion (Suggested Action) für den Engine-CodeFix
/// <see cref="AddMissingExitTransitionCodeFix"/>: ergänzt eine fehlende Exit-Transition für einen offenen
/// Exit-Verbindungspunkt eines eingebetteten Task-Knotens (<c>Nav0025</c>). Nach dem Anwenden der
/// Textänderungen setzt die Aktion zusätzlich die Auswahl auf das neu eingefügte Sprungziel (etwa den
/// <c>TO_BE_FILLED</c>-Platzhalter). Angeboten wird die Aktion vom
/// <see cref="AddMissingExitTransitionSuggestedActionProvider"/>.
/// </summary>
class AddMissingExitTransitionSuggestedAction : CodeFixSuggestedAction<AddMissingExitTransitionCodeFix> {

    public AddMissingExitTransitionSuggestedAction(AddMissingExitTransitionCodeFix codeFix,
                                                   CodeFixSuggestedActionParameter parameter,
                                                   CodeFixSuggestedActionContext context)
        : base(context, parameter, codeFix) {
    }

    /// <summary>Das Lightbulb-Icon der Aktion — <see cref="ImageMonikers.AddEdge"/>.</summary>
    public override ImageMoniker IconMoniker => ImageMonikers.AddEdge;
    /// <summary>Der in der Lightbulb angezeigte Text, benannt nach dem betroffenen Exit-Verbindungspunkt
    /// (<see cref="AddMissingExitTransitionCodeFix.ConnectionPoint"/>).</summary>
    public override string       DisplayText => $"Add missing edge for exit '{CodeFix.ConnectionPoint.Name}'";

    /// <summary>
    /// Wendet die vom Fix berechneten Textänderungen auf den Editor-Puffer an, aktualisiert das
    /// Semantik-Modell synchron und setzt die Auswahl auf das Ziel der neu ergänzten Exit-Transition
    /// (<see cref="AddMissingExitTransitionCodeFix.TryGetSelectionAfterChanges"/>), sofern auffindbar.
    /// </summary>
    /// <param name="cancellationToken">Token zum Abbrechen der Ausführung.</param>
    protected override void Apply(CancellationToken cancellationToken) {

        ApplyTextChanges(CodeFix.GetTextChanges());

        ThreadHelper.ThrowIfNotOnUIThread();

        var codeGenerationUnitAndSnapshot = SemanticModelService.TryGet(Parameter.TextBuffer)?.UpdateSynchronously();
        if(codeGenerationUnitAndSnapshot == null) {
            return;
        }

        var selection =CodeFix.TryGetSelectionAfterChanges(codeGenerationUnitAndSnapshot.CodeGenerationUnit);
        if(!selection.IsMissing) {
            Parameter.TextView.SetSelection(selection.ToSnapshotSpan(codeGenerationUnitAndSnapshot.Snapshot));
        }            
    }
}
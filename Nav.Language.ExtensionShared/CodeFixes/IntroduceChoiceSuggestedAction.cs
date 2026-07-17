#region Using Directives

using System;
using System.Threading;

using Microsoft.VisualStudio.Imaging.Interop;

using Pharmatechnik.Nav.Language.CodeFixes.Refactoring;
using Pharmatechnik.Nav.Language.Extension.Images;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.CodeFixes; 

/// <summary>
/// VS-Lightbulb-Aktion (Suggested Action) für den Engine-Refactoring-CodeFix
/// <see cref="IntroduceChoiceCodeFix"/>: schaltet vor dem Ziel einer Transition eine neue Choice ein.
/// Die Aktion erfragt zuvor per Eingabe-Dialog den Namen der Choice (vorbelegt mit
/// <see cref="IntroduceChoiceCodeFix.SuggestChoiceName"/>, geprüft über
/// <see cref="IntroduceChoiceCodeFix.ValidateChoiceName"/>) und wendet die Änderungen nur bei bestätigter
/// Eingabe an. Angeboten wird die Aktion vom <see cref="IntroduceChoiceSuggestedActionProvider"/>.
/// </summary>
class IntroduceChoiceSuggestedAction : CodeFixSuggestedAction<IntroduceChoiceCodeFix> {

    public IntroduceChoiceSuggestedAction(IntroduceChoiceCodeFix codeFix,
                                          CodeFixSuggestedActionParameter parameter,
                                          CodeFixSuggestedActionContext context)
        : base(context, parameter, codeFix) {
    }

    /// <summary>Das Lightbulb-Icon der Aktion — <see cref="ImageMonikers.InsertNode"/>.</summary>
    public override ImageMoniker IconMoniker => ImageMonikers.InsertNode;
    /// <summary>Der in der Lightbulb angezeigte Text — der Anzeigename des Fixes
    /// (<see cref="IntroduceChoiceCodeFix.Name"/>).</summary>
    public override string       DisplayText => CodeFix.Name;

    /// <summary>
    /// Erfragt per Eingabe-Dialog (<see cref="CodeFixSuggestedActionContext.DialogService"/>) den Namen der
    /// neuen Choice und wendet — bei nicht-leerer Eingabe — die vom Fix für diesen Namen berechneten
    /// Textänderungen auf den Editor-Puffer an.
    /// </summary>
    /// <param name="cancellationToken">Token zum Abbrechen der Ausführung.</param>
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
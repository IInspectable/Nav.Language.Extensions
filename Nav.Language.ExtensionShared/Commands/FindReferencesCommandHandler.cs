#region Using Directives

using System;
using System.ComponentModel.Composition;
using System.Threading.Tasks;

using Microsoft.VisualStudio.Commanding;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Editor.Commanding.Commands;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;
using Microsoft.VisualStudio.Utilities;

using Pharmatechnik.Nav.Language.CodeAnalysis.FindReferences;
using Pharmatechnik.Nav.Language.Extension.Common;
using Pharmatechnik.Nav.Language.Extension.FindReferences;
using Pharmatechnik.Nav.Language.FindReferences;

using Task = System.Threading.Tasks.Task;
using ThreadHelper = Microsoft.VisualStudio.Shell.ThreadHelper;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.Commands; 

/// <summary>
/// Command-Handler für „Find All References" (Shift+F12) in Nav-Dateien. Ermittelt das Symbol unter dem
/// Cursor und sucht dessen Referenzen sowohl innerhalb der Nav-Quellen (Engine-Kern
/// <see cref="ReferenceFinder"/>) als auch im generierten C#-Code über die Roslyn-Brücke
/// (<see cref="WfsReferenceFinder"/>). Die Treffer laufen als Live-Stream über den
/// <see cref="FindReferencesPresenter"/> in das VS-Werkzeugfenster „Find All References".
/// </summary>
[Export(typeof(ICommandHandler))]
[ContentType(NavLanguageContentDefinitions.ContentType)]
[Name(CommandHandlerNames.FindReferencesCommandHandler)]
class FindReferencesCommandHandler: ICommandHandler<FindReferencesCommandArgs> {

    readonly FindReferencesPresenter _referencesPresenter;

    [ImportingConstructor]
    public FindReferencesCommandHandler(FindReferencesPresenter referencesPresenter) {
        _referencesPresenter = referencesPresenter;

    }

    /// <summary>Im Command-System angezeigter Name des Befehls.</summary>
    public string DisplayName => "Find All References";

    /// <summary>Verfügbar nur für einen <see cref="IWpfTextView"/>, sonst <see cref="CommandState.Unavailable"/>.</summary>
    public CommandState GetCommandState(FindReferencesCommandArgs args) {
        return args.TextView is IWpfTextView ? CommandState.Available : CommandState.Unavailable;
    }

    /// <summary>
    /// Startet die Referenzsuche: öffnet einen Präsentations-Kontext und stößt die asynchrone Suche
    /// (<see cref="FindAllReferencesAsync"/>) im Hintergrund an. Gibt <see langword="false"/> zurück, damit
    /// die Standard-Behandlung nicht unterdrückt wird.
    /// </summary>
    public bool ExecuteCommand(FindReferencesCommandArgs args, CommandExecutionContext executionContext) {

        ThreadHelper.ThrowIfNotOnUIThread();

        var codeGenerationUnitAndSnapshot = GetCodeGenerationUnit(args.SubjectBuffer);
        var context                       = _referencesPresenter.StartSearch();

        FindAllReferencesAsync(args, codeGenerationUnitAndSnapshot, context).FileAndForget("nav/extension/findreferences");

        return false;

    }

    /// <summary>
    /// Sucht auf einem Hintergrund-Thread alle Referenzen des Symbols unter dem Cursor. Bricht ergebnislos
    /// ab, wenn dort kein Symbol liegt; andernfalls werden Nav-interne Referenzen
    /// (<see cref="ReferenceFinder.FindReferencesAsync"/>) und C#-seitige Referenzen
    /// (<see cref="WfsReferenceFinder.FindReferencesAsync"/>) ermittelt und in den
    /// <paramref name="context"/> gemeldet.
    /// </summary>
    async Task FindAllReferencesAsync(FindReferencesCommandArgs args, CodeGenerationUnitAndSnapshot codeGenerationUnitAndSnapshot, FindReferencesContext context) {

        var originatingSymbol = args.TextView.TryFindSymbolUnderCaret(codeGenerationUnitAndSnapshot);

        try {

            if (originatingSymbol == null) {
                // Search found no results.
                return;
            }

            // switch to a background thread
            await TaskScheduler.Default;

            var solution = await NavLanguagePackage.GetSolutionAsync(context.CancellationToken).ConfigureAwait(false);
            var fra      = new FindReferencesArgs(originatingSymbol, codeGenerationUnitAndSnapshot.CodeGenerationUnit, solution, context);

            await ReferenceFinder.FindReferencesAsync(fra).ConfigureAwait(false);
            await WfsReferenceFinder.FindReferencesAsync(NavLanguagePackage.Workspace.CurrentSolution, fra).ConfigureAwait(false);

        } catch (OperationCanceledException) {
        } finally {
            await context.OnCompletedAsync();
        }

    }

    /// <summary>
    /// Liefert das aktuelle, mit dem Snapshot synchronisierte Semantikmodell
    /// (<see cref="CodeGenerationUnitAndSnapshot"/>) des Puffers über den <see cref="SemanticModelService"/>.
    /// </summary>
    static CodeGenerationUnitAndSnapshot GetCodeGenerationUnit(ITextBuffer textBuffer) {

        ThreadHelper.ThrowIfNotOnUIThread();

        var semanticModelService      = SemanticModelService.GetOrCreateSingelton(textBuffer);
        var generationUnitAndSnapshot = semanticModelService.UpdateSynchronously();

        return generationUnitAndSnapshot;
    }

}
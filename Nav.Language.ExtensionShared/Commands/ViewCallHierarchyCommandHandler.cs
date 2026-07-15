#region Using Directives

using System.ComponentModel.Composition;

using Microsoft.VisualStudio.CallHierarchy.Package.Definitions;
using Microsoft.VisualStudio.Commanding;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Editor.Commanding.Commands;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Utilities;

using Pharmatechnik.Nav.Language.CallHierarchy;
using Pharmatechnik.Nav.Language.Extension.CallHierarchy;
using Pharmatechnik.Nav.Language.Extension.Common;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.Commands;

/// <summary>
/// Behandelt die Geste „View Call Hierarchy" (Ctrl+K, Ctrl+T) für Nav-Dateien: ankert die Aufrufhierarchie
/// an der Task unter dem Cursor und öffnet VS' eingebautes Call-Hierarchy-Toolfenster.
/// <para>
/// Präsentation läuft — wie bei Roslyn — über den VS-Service <see cref="SCallHierarchy"/>/
/// <see cref="ICallHierarchy"/> (<see cref="ICallHierarchy.ShowToolWindow"/> +
/// <see cref="ICallHierarchy.AddRootItem"/>), nicht über einen <c>ICallHierarchyPresenter</c> (der nimmt
/// Roslyns konkretes Item) und nicht über ein eigenes Toolfenster.
/// </para>
/// </summary>
[Export(typeof(ICommandHandler))]
[ContentType(NavLanguageContentDefinitions.ContentType)]
[Name(CommandHandlerNames.ViewCallHierarchyCommandHandler)]
class ViewCallHierarchyCommandHandler: ICommandHandler<ViewCallHierarchyCommandArgs> {

    const string NoTaskMessage    = "Den Cursor auf eine Task setzen, um die Aufrufhierarchie anzuzeigen.";
    const string CannotOpenMessage = "Das Aufrufhierarchie-Fenster konnte nicht geöffnet werden.";

    readonly SVsServiceProvider _serviceProvider;

    [ImportingConstructor]
    public ViewCallHierarchyCommandHandler(SVsServiceProvider serviceProvider) {
        _serviceProvider = serviceProvider;
    }

    /// <summary>Im Command-System angezeigter Name des Befehls.</summary>
    public string DisplayName => "View Call Hierarchy";

    /// <summary>Verfügbar nur für einen <see cref="IWpfTextView"/>, sonst <see cref="CommandState.Unavailable"/>.</summary>
    public CommandState GetCommandState(ViewCallHierarchyCommandArgs args) {
        return args.TextView is IWpfTextView ? CommandState.Available : CommandState.Unavailable;
    }

    /// <summary>
    /// Bereitet über den Engine-Kern <see cref="NavCallHierarchyService"/> die Aufrufhierarchie für die Task
    /// unter dem Cursor auf, erzeugt daraus ein Wurzel-Element (<see cref="NavCallHierarchyItemFactory"/>) und
    /// zeigt es im VS-Aufrufhierarchie-Fenster. Ohne Task unter dem Cursor bzw. bei fehlendem Dienst wird eine
    /// Hinweismeldung ausgegeben.
    /// </summary>
    public bool ExecuteCommand(ViewCallHierarchyCommandArgs args, CommandExecutionContext executionContext) {

        ThreadHelper.ThrowIfNotOnUIThread();

        var codeGenerationUnitAndSnapshot = GetCodeGenerationUnit(args.SubjectBuffer);

        var caretPoint = args.TextView.GetCaretPoint(s => s.ContentType.IsOfType(NavLanguageContentDefinitions.ContentType));
        if (caretPoint == null) {
            return false;
        }

        var task = NavCallHierarchyService.PrepareCallHierarchy(codeGenerationUnitAndSnapshot.CodeGenerationUnit, caretPoint.Value.Position);
        if (task == null) {
            ShellUtil.ShowInfoMessage(NoTaskMessage);
            return true;
        }

        var root = NavCallHierarchyItemFactory.FromDefinition(task);
        if (root == null) {
            return true;
        }

        if (_serviceProvider.GetService(typeof(SCallHierarchy)) is not ICallHierarchy callHierarchy) {
            ShellUtil.ShowErrorMessage(CannotOpenMessage);
            return true;
        }

        callHierarchy.ShowToolWindow();
        callHierarchy.AddRootItem(root);

        return true;
    }

    /// <summary>Liefert das aktuelle, mit dem Snapshot synchronisierte Semantikmodell des Puffers über den <see cref="SemanticModelService"/>.</summary>
    static CodeGenerationUnitAndSnapshot GetCodeGenerationUnit(ITextBuffer textBuffer) {

        ThreadHelper.ThrowIfNotOnUIThread();

        var semanticModelService = SemanticModelService.GetOrCreateSingelton(textBuffer);
        return semanticModelService.UpdateSynchronously();
    }

}

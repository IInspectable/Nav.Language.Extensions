#region Using Directives

using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Imaging.Interop;
using Microsoft.VisualStudio.Language.Intellisense;

using Pharmatechnik.Nav.Language.CodeFixes;
using Pharmatechnik.Nav.Language.Text;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.CodeFixes; 

/// <summary>
/// Die abstrakte Basis, die einen Engine-<see cref="CodeFix"/> als VS-Lightbulb-Aktion
/// (<see cref="ISuggestedAction"/>) an den Editor anbietet. Sie hält den geteilten
/// <see cref="CodeFixSuggestedActionContext"/> (Dienste) und den aufruf-spezifischen
/// <see cref="CodeFixSuggestedActionParameter"/> (Snapshot, <see cref="Microsoft.VisualStudio.Text.Editor.ITextView"/>) und übersetzt die
/// Fix-Metadaten (<see cref="Prio"/>, <see cref="Category"/>, <see cref="ApplicableToSpan"/>) in die vom
/// VS-SDK erwartete Form. Das eigentliche Anwenden delegiert <see cref="Invoke"/> an das abstrakte
/// <see cref="Apply"/>; die konkrete, an einen Fix-Typ gebundene Ableitung ist
/// <see cref="CodeFixSuggestedAction{T}"/>.
/// </summary>
abstract class CodeFixSuggestedAction: ISuggestedAction {

    /// <summary>Initialisiert die Basis mit dem geteilten Dienst-Kontext und dem aufruf-spezifischen Parameter.</summary>
    /// <param name="context">Der geteilte Kontext (Wait-Indicator, <see cref="ITextChangeService"/>, Dialog-Dienst).</param>
    /// <param name="parameter">Der aufruf-spezifische Parameter (Bereich, Snapshot, <see cref="Microsoft.VisualStudio.Text.Editor.ITextView"/>).</param>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> oder <paramref name="parameter"/> ist <c>null</c>.</exception>
    protected CodeFixSuggestedAction(CodeFixSuggestedActionContext context, CodeFixSuggestedActionParameter parameter) {
        Context   = context   ?? throw new ArgumentNullException(nameof(context));
        Parameter = parameter ?? throw new ArgumentNullException(nameof(parameter));
    }

    /// <summary>Der geteilte Dienst-Kontext (u.a. <see cref="ITextChangeService"/> für das Anwenden).</summary>
    protected CodeFixSuggestedActionContext   Context   { get; }
    /// <summary>Der aufruf-spezifische Parameter (Bereich, Snapshot, <see cref="Microsoft.VisualStudio.Text.Editor.ITextView"/>).</summary>
    protected CodeFixSuggestedActionParameter Parameter { get; }

    /// <summary>Der Quelltext-Bereich, an dem der Vorschlag verankert ist, oder <c>null</c>, wenn nicht anwendbar.</summary>
    public abstract Span?           ApplicableToSpan { get; }
    /// <summary>Die Priorität innerhalb des Vorschlags-Sets (höher = weiter oben angeboten).</summary>
    public abstract CodeFixPrio     Prio             { get; }
    /// <summary>Die fachliche Familie des Fixes (steuert Gruppierung und Icon des Vorschlags).</summary>
    public abstract CodeFixCategory Category         { get; }
    /// <summary>Der dem Nutzer in der Lightbulb angezeigte Titel des Vorschlags.</summary>
    public abstract string          DisplayText      { get; }
    /// <summary>Die Beschreibung des Vorschlags in der Undo-Historie.</summary>
    public abstract string          UndoDescription  { get; }

    /// <summary>Das Icon des Vorschlags; standardmäßig keines (<c>default</c>).</summary>
    public virtual ImageMoniker IconMoniker        => default;
    /// <summary>Der barrierefreie Text zum <see cref="IconMoniker"/>; standardmäßig keiner.</summary>
    public virtual string       IconAutomationText => null;
    /// <summary>Der neben dem Vorschlag angezeigte Tastenkürzel-Text; standardmäßig keiner.</summary>
    public virtual string       InputGestureText   => null;

    /// <summary>Gibt gehaltene Ressourcen frei. Die Basis hält keine und tut nichts.</summary>
    public virtual void Dispose() {

    }

    /// <summary>
    /// Liefert die Telemetrie-Kennung des Vorschlags. Nav-Fixes nehmen an der VS-Telemetrie nicht teil und
    /// liefern stets <c>false</c>.
    /// </summary>
    /// <param name="telemetryId">Wird auf <see cref="Guid.Empty"/> gesetzt.</param>
    /// <returns>Immer <c>false</c>.</returns>
    public bool TryGetTelemetryId(out Guid telemetryId) {
        telemetryId = Guid.Empty;
        return false;
    }

    /// <summary>Gibt an, ob der Vorschlag geschachtelte Unter-Sets besitzt; Nav-Fixes haben keine.</summary>
    public virtual bool HasActionSets => false;

    /// <summary>Liefert etwaige geschachtelte Vorschlags-Sets. Nav-Fixes haben keine und liefern <c>null</c>.</summary>
    /// <param name="cancellationToken">Token zum Abbrechen der Anfrage.</param>
    /// <returns>Ein Task mit <c>null</c>.</returns>
    public virtual Task<IEnumerable<SuggestedActionSet>> GetActionSetsAsync(CancellationToken cancellationToken) {
        return Task.FromResult<IEnumerable<SuggestedActionSet>>(null);
    }

    /// <summary>Gibt an, ob der Vorschlag eine Vorschau anbietet; Nav-Fixes bieten keine.</summary>
    public virtual bool HasPreview => false;

    /// <summary>Liefert die Vorschau des Vorschlags. Nav-Fixes bieten keine und liefern <c>null</c>.</summary>
    /// <param name="cancellationToken">Token zum Abbrechen der Anfrage.</param>
    /// <returns>Ein Task mit <c>null</c>.</returns>
    public virtual Task<object> GetPreviewAsync(CancellationToken cancellationToken) {
        return Task.FromResult<object>(null);
    }

    /// <summary>Wendet den Vorschlag an, wenn der Nutzer ihn in der Lightbulb auswählt.</summary>
    /// <param name="cancellationToken">Token zum Abbrechen der Anwendung.</param>
    public abstract void Invoke(CancellationToken cancellationToken);

    /// <summary>Führt die eigentliche Quelltext-Änderung aus; von der Ableitung je Fix-Typ implementiert.</summary>
    /// <param name="cancellationToken">Token zum Abbrechen der Anwendung.</param>
    protected abstract void Apply(CancellationToken cancellationToken);

    /// <summary>
    /// Wendet die von einem Fix berechneten <see cref="TextChange"/>s über den
    /// <see cref="ITextChangeService"/> auf den <see cref="Microsoft.VisualStudio.Text.Editor.ITextView"/> an — als eine widerrufbare
    /// Bearbeitung mit <see cref="UndoDescription"/> als Undo-Beschreibung.
    /// </summary>
    /// <param name="textChanges">Das vom Fix berechnete Edit-Set.</param>
    /// <returns>Der resultierende <see cref="ITextSnapshot"/> nach dem Anwenden.</returns>
    protected ITextSnapshot ApplyTextChanges(IEnumerable<TextChange> textChanges) {

        var textChangesAndSnapshot = new TextChangesAndSnapshot(
            textChanges: textChanges,
            snapshot: Parameter.CodeGenerationUnitAndSnapshot.Snapshot);

        return Context.TextChangeService.ApplyTextChanges(
            textView: Parameter.TextView,
            undoDescription: UndoDescription,
            textChangesAndSnapshot: textChangesAndSnapshot,
            waitMessage: DisplayText);
    }

}
#region Using Directives

using System;
using System.Threading;

using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;

using Pharmatechnik.Nav.Language.CodeFixes;
using Pharmatechnik.Nav.Language.Extension.Common;
using Pharmatechnik.Nav.Language.Text;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.CodeFixes; 

/// <summary>
/// Die an einen konkreten Fix-Typ <typeparamref name="T"/> gebundene Basis einer
/// <see cref="CodeFixSuggestedAction"/>. Sie leitet die Vorschlags-Metadaten
/// (<see cref="UndoDescription"/>, <see cref="ApplicableToSpan"/>, <see cref="Prio"/>,
/// <see cref="Category"/>) unmittelbar aus dem gehaltenen <see cref="CodeFix"/> ab und implementiert
/// <see cref="Invoke"/> so, dass nach dem Anwenden das semantische Modell des Buffers aktualisiert wird.
/// Das eigentliche Berechnen und Anwenden der Edits bleibt der konkreten Ableitung (deren
/// <see cref="CodeFixSuggestedAction.Apply"/>) überlassen.
/// </summary>
/// <typeparam name="T">Der konkrete Fix-Typ (Ableitung von <see cref="CodeFix"/>).</typeparam>
abstract class CodeFixSuggestedAction<T>: CodeFixSuggestedAction where T : CodeFix {

    /// <summary>Initialisiert die Aktion mit dem Dienst-Kontext, dem Aufruf-Parameter und dem anzuwendenden Fix.</summary>
    /// <param name="context">Der geteilte Dienst-Kontext.</param>
    /// <param name="parameter">Der aufruf-spezifische Parameter (Bereich, Snapshot, TextView).</param>
    /// <param name="codeFix">Der konkrete Fix, dessen Metadaten und Edits die Aktion abbildet.</param>
    /// <exception cref="ArgumentNullException"><paramref name="codeFix"/> ist <c>null</c>.</exception>
    protected CodeFixSuggestedAction(CodeFixSuggestedActionContext context, CodeFixSuggestedActionParameter parameter, T codeFix): base(context, parameter) {
        CodeFix = codeFix ?? throw new ArgumentNullException(nameof(codeFix));
    }

    /// <summary>Der von dieser Aktion angebotene Engine-Fix.</summary>
    public T CodeFix { get; }

    /// <summary>Die Undo-Beschreibung — der <see cref="CodeFix.Name"/> des Fixes.</summary>
    public sealed override string          UndoDescription  => CodeFix.Name;
    /// <summary>Der Anker-Bereich des Vorschlags — der <see cref="CodeFix.ApplicableTo"/>-Bereich, in den aktuellen Snapshot übersetzt.</summary>
    public sealed override Span?           ApplicableToSpan => GetSnapshotSpan(CodeFix.ApplicableTo);
    /// <summary>Die Priorität des Vorschlags — die <see cref="CodeFix.Prio"/> des Fixes.</summary>
    public sealed override CodeFixPrio     Prio             => CodeFix.Prio;
    /// <summary>Die fachliche Familie des Vorschlags — die <see cref="CodeFix.Category"/> des Fixes.</summary>
    public sealed override CodeFixCategory Category         => CodeFix.Category;

    /// <summary>
    /// Wendet den Fix an (<see cref="CodeFixSuggestedAction.Apply"/>) und aktualisiert anschließend auf dem
    /// UI-Thread das semantische Modell des Buffers synchron, damit Folge-Features sofort auf dem neuen Stand
    /// arbeiten.
    /// </summary>
    /// <param name="cancellationToken">Token zum Abbrechen der Anwendung.</param>
    public sealed override void Invoke(CancellationToken cancellationToken) {

        Apply(cancellationToken);

        ThreadHelper.ThrowIfNotOnUIThread();

        SemanticModelService.TryGet(Parameter.TextBuffer)?.UpdateSynchronously();
    }

    /// <summary>Übersetzt einen Engine-<see cref="TextExtent"/> in einen <see cref="SnapshotSpan"/> des aktuellen Snapshots.</summary>
    /// <param name="lineExtent">Der zu übersetzende Bereich, oder <c>null</c>.</param>
    /// <returns>Der entsprechende <see cref="SnapshotSpan"/>, oder <c>null</c>, wenn kein Bereich vorliegt.</returns>
    SnapshotSpan? GetSnapshotSpan(TextExtent? lineExtent) {
        return lineExtent?.ToSnapshotSpan(Parameter.CodeGenerationUnitAndSnapshot.Snapshot);
    }

}
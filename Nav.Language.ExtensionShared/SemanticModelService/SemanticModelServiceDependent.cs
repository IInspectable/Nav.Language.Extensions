#region Using Directives

using System;

using Microsoft.VisualStudio.Text;

#endregion

namespace Pharmatechnik.Nav.Language.Extension;

/// <summary>
/// Basisklasse für Puffer-gebundene Bausteine, die auf das Semantikmodell des <see cref="SemanticModelService"/>
/// aufsetzen. Sie bezieht den Puffer-Singleton und meldet sich auf dessen
/// <see cref="SemanticModelService.SemanticModelChanging"/>/<see cref="SemanticModelService.SemanticModelChanged"/>-Events
/// an; Ableitungen überschreiben <see cref="OnSemanticModelChanging"/>/<see cref="OnSemanticModelChanged"/>,
/// um auf ein neues <see cref="CodeGenerationUnitAndSnapshot"/> zu reagieren (etwa Tagger und Adorner).
/// </summary>
abstract class SemanticModelServiceDependent: IDisposable {

    /// <summary>
    /// Bindet den Baustein an den <see cref="SemanticModelService"/> von <paramref name="textBuffer"/> und
    /// abonniert dessen Änderungs-Events.
    /// </summary>
    protected SemanticModelServiceDependent(ITextBuffer textBuffer) {

        TextBuffer           = textBuffer;
        SemanticModelService = SemanticModelService.GetOrCreateSingelton(textBuffer);

        SemanticModelService.SemanticModelChanging += OnSemanticModelChanging;
        SemanticModelService.SemanticModelChanged  += OnSemanticModelChanged;
    }

    /// <summary>
    /// Meldet die abonnierten <see cref="SemanticModelService"/>-Events wieder ab. Ableitungen, die selbst
    /// Ressourcen halten, überschreiben und rufen <c>base.Dispose()</c>.
    /// </summary>
    public virtual void Dispose() {
        SemanticModelService.SemanticModelChanging -= OnSemanticModelChanging;
        SemanticModelService.SemanticModelChanged  -= OnSemanticModelChanged;
    }

    /// <summary>Der zugrunde liegende Puffer.</summary>
    public ITextBuffer TextBuffer { get; }

    /// <summary>Der Puffer-gebundene <see cref="SemanticModelService"/>, dessen Modell dieser Baustein nutzt.</summary>
    public SemanticModelService SemanticModelService { get; }

    /// <summary>
    /// Wird aufgerufen, wenn das bisherige <see cref="CodeGenerationUnitAndSnapshot"/> veraltet. Basis tut nichts.
    /// </summary>
    protected virtual void OnSemanticModelChanging(object sender, EventArgs e) {
    }

    /// <summary>
    /// Wird aufgerufen, wenn ein neues <see cref="CodeGenerationUnitAndSnapshot"/> bereitsteht. Basis tut nichts.
    /// </summary>
    protected virtual void OnSemanticModelChanged(object sender, SnapshotSpanEventArgs e) {
    }

}
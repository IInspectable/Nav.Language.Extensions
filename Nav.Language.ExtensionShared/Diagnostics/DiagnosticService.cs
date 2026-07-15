#region Using Directives

using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;

using JetBrains.Annotations;

using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Text.Outlining;
using Microsoft.VisualStudio.ComponentModelHost;

using Pharmatechnik.Nav.Language.Extension.Common;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.Diagnostics; 

/// <summary>
/// Pro-Ansicht geführter Dienst, der die aktuellen Diagnosen einer <c>.nav</c>-Datei bündelt und
/// bereitstellt. Er aggregiert die <see cref="DiagnosticErrorTag"/>s über einen
/// <see cref="ITagAggregator{T}"/>, gruppiert sie nach <see cref="DiagnosticSeverity"/> und bietet
/// Abfragen (Anzahl/Vorhandensein je Schweregrad, schlimmster Schweregrad) sowie das Navigieren zur
/// nächsten Diagnose. Speist die Anzeigen <see cref="DiagnosticSummaryMargin"/> und
/// <see cref="DiagnosticStripeMargin"/>. Wird per <see cref="GetOrCreate"/> als Singleton je
/// <see cref="IWpfTextView"/> gehalten.
/// </summary>
sealed class DiagnosticService : IDisposable {

    readonly IWpfTextView                       _textView;
    readonly ITagAggregator<DiagnosticErrorTag> _errorTagAggregator;
    readonly IOutliningManagerService           _outliningManagerService;
    bool                                        _waitingForAnalysis;

    [NotNull]
    IReadOnlyDictionary<DiagnosticSeverity, ReadOnlyCollection<IMappingTagSpan<DiagnosticErrorTag>>> _diagnosticMapping;
    DiagnosticSeverity? _worstSeverity;
        
    DiagnosticService(IWpfTextView textView, IComponentModel componentModel) {
        var viewTagAggregatorFactoryService = componentModel.GetService<IViewTagAggregatorFactoryService>();
            
        _textView                = textView;
        _outliningManagerService = componentModel.GetService<IOutliningManagerService>();
        _errorTagAggregator      = viewTagAggregatorFactoryService.CreateTagAggregator<DiagnosticErrorTag>(textView);
        _diagnosticMapping       = new Dictionary<DiagnosticSeverity, ReadOnlyCollection<IMappingTagSpan<DiagnosticErrorTag>>>();
        _waitingForAnalysis      = true;
            
        _textView.Closed                       += OnTextViewClosed;
        _textView.TextBuffer.Changed           += OnTextBufferChanged;
        _errorTagAggregator.BatchedTagsChanged += OnBatchedTagsChanged;

        // Evtl. gibt es bereits einen Syntaxbaum...
        Invalidate();
    }

    /// <summary>
    /// Liefert den <see cref="DiagnosticService"/> der Ansicht und legt ihn beim ersten Zugriff als
    /// Singleton in der Property-Sammlung der <see cref="IWpfTextView"/> an.
    /// </summary>
    public static DiagnosticService GetOrCreate(IWpfTextView textView) {
        var componentModel = NavLanguagePackage.GetGlobalService<SComponentModel, IComponentModel>();
        return textView.Properties.GetOrCreateSingletonProperty(() =>
                                                                    new DiagnosticService(textView, componentModel));
    }

    /// <summary>Meldet den Dienst von allen Ereignissen ab und gibt den Tag-Aggregator frei.</summary>
    public void Dispose() {
        _textView.Properties.RemoveProperty(this);

        _textView.Closed                       -= OnTextViewClosed;
        _textView.TextBuffer.Changed           -= OnTextBufferChanged;
        _errorTagAggregator.BatchedTagsChanged -= OnBatchedTagsChanged;
        _errorTagAggregator?.Dispose();
    }

    void OnTextBufferChanged(object sender, TextContentChangedEventArgs e) {
        OnDiagnosticsChanging();
    }

    void OnTextViewClosed(object sender, EventArgs e) {
        Dispose();
    }

    void OnBatchedTagsChanged(object sender, BatchedTagsChangedEventArgs e) {
        UpdateDiagnostics();
    }
        
    /// <summary>Wird ausgelöst, sobald sich Eingaben ändern und die Diagnosen neu berechnet werden (Analyse läuft an).</summary>
    public event EventHandler DiagnosticsChanging;

    void OnDiagnosticsChanging() {

        _waitingForAnalysis = true;

        DiagnosticsChanging?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Wird ausgelöst, sobald die Diagnosen neu berechnet vorliegen (Analyse abgeschlossen).</summary>
    public event EventHandler DiagnosticsChanged;

    void OnDiagnosticsChanged() {

        _waitingForAnalysis = false;

        DiagnosticsChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Der schlimmste aktuell vorhandene <see cref="DiagnosticSeverity"/> oder <see langword="null"/>, wenn keine Diagnosen vorliegen.</summary>
    [CanBeNull]
    public DiagnosticSeverity? WorstSeverity {
        get { return _worstSeverity; }
    }

    /// <summary><see langword="true"/>, wenn weder Fehler noch Warnungen vorliegen.</summary>
    public bool NoErrorsOrWarnings {
        get {
            return CountDiagnosticsWithSeverity(DiagnosticSeverity.Error)   == 0 &&
                   CountDiagnosticsWithSeverity(DiagnosticSeverity.Warning) == 0;
        }
    }

    /// <summary><see langword="true"/>, solange die Analyse läuft und noch kein Ergebnis vorliegt.</summary>
    public bool WaitingForAnalysis {
        get { return _waitingForAnalysis; }
    }

    /// <summary>
    /// Verwirft die berechneten Diagnosen und stößt über die beteiligten <see cref="SemanticModelService"/>
    /// eine Neuberechnung an.
    /// </summary>
    public void Invalidate() {

        OnDiagnosticsChanging();

        foreach(var modelService in _textView.BufferGraph
                                             .GetTextBuffers(tb => SemanticModelService.TryGet(tb) != null)
                                             .Select(SemanticModelService.TryGet)) {
            modelService?.Invalidate();
        }
    }

    /// <summary>Gibt an, ob es Diagnosen mit dem angegebenen <paramref name="severity"/> gibt.</summary>
    public bool HasDiagnosticsWithSeverity(DiagnosticSeverity severity) {
        return _diagnosticMapping.ContainsKey(severity);            
    }

    /// <summary>Zählt die Diagnosen mit dem angegebenen <paramref name="severity"/>.</summary>
    public int CountDiagnosticsWithSeverity(DiagnosticSeverity severity) {
        return HasDiagnosticsWithSeverity(severity) ? _diagnosticMapping[severity].Count: 0;
    }

    /// <summary>Liefert die (nach Position sortierten) Diagnose-Tag-Spans mit dem angegebenen <paramref name="severity"/>.</summary>
    public IEnumerable<IMappingTagSpan<DiagnosticErrorTag>> GetDiagnosticsWithSeverity(DiagnosticSeverity severity) {
        return (HasDiagnosticsWithSeverity(severity)? _diagnosticMapping[severity] : null) 
            ?? Enumerable.Empty<IMappingTagSpan<DiagnosticErrorTag>>();
    }

    /// <summary><see langword="true"/>, wenn überhaupt eine Diagnose zum Anspringen vorliegt.</summary>
    public bool CanGoToNextDiagnostic {
        get { return _diagnosticMapping.Count > 0; }
    }

    /// <summary>
    /// Springt zur nächsten Diagnose, wobei die Schweregrade in der Reihenfolge Error, Warning, Suggestion
    /// abgearbeitet werden.
    /// </summary>
    public bool GoToNextDiagnostic() {
            
        var severities = new[] {
            DiagnosticSeverity.Error,
            DiagnosticSeverity.Warning,
            DiagnosticSeverity.Suggestion};

        return severities.Where(HasDiagnosticsWithSeverity)
                         .Select(GoToNextDiagnostic)
                         .FirstOrDefault();           
    }

    /// <summary>
    /// Springt zur nächsten Diagnose des angegebenen <paramref name="severity"/> hinter der Caret-Position;
    /// hinter der letzten wird zur ersten umgebrochen. Liefert <see langword="false"/>, wenn es keine solche
    /// Diagnose gibt.
    /// </summary>
    public bool GoToNextDiagnostic(DiagnosticSeverity severity) {

        if(!HasDiagnosticsWithSeverity(severity)) {
            return false;
        }

        var caretPos = _textView.Caret.Position.BufferPosition;
            
        // TODO noch optimieren / überprüfen
        foreach(var tagSpan in GetDiagnosticsWithSeverity(severity)
                   .Select(mappingTagSpan => _textView.MapToSingleSnapshotSpan(mappingTagSpan))) {

            if(tagSpan?.Span.Start > caretPos) {
                return GoToDiagnostic(tagSpan);
            }
        }

        var firstMappingTagSpan = GetDiagnosticsWithSeverity(severity).First();
        var ts                  = _textView.MapToSingleSnapshotSpan(firstMappingTagSpan);            
        return GoToDiagnostic(ts);
    }
                
    /// <summary>
    /// Fragt alle <see cref="DiagnosticErrorTag"/>s über den Aggregator ab, gruppiert sie nach Schweregrad
    /// (je Gruppe nach Position sortiert), ermittelt den schlimmsten Schweregrad und meldet die Änderung.
    /// </summary>
    void UpdateDiagnostics() {

        var mappingSpan = _textView.BufferGraph.CreateMappingSpan(
            new SnapshotSpan(_textView.TextSnapshot, 0, _textView.TextSnapshot.Length), 
            SpanTrackingMode.EdgeInclusive);

        var diagnosticMapping = _errorTagAggregator.GetTags(mappingSpan)
                                                   .GroupBy(tagSpan => tagSpan.Tag.Diagnostic.Severity)
                                                   .ToDictionary(
                                                        grouping => grouping.Key,
                                                        grouping => grouping.OrderBy(tags => tags.Tag.Diagnostic.Location.Start)
                                                                            .ToList()
                                                                            .AsReadOnly());

        _diagnosticMapping = diagnosticMapping;
        _worstSeverity     = diagnosticMapping.Keys.GetWorst();

        OnDiagnosticsChanged();
    }

    bool GoToDiagnostic(ITagSpan<DiagnosticErrorTag> tagSpan) {
        if (tagSpan == null) {
            return false;
        }
        return _textView.TryMoveCaretToAndEnsureVisible(tagSpan.Span.Start, _outliningManagerService);
    }
}
#region Using Directives

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.Immutable;

using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Language.Intellisense;

using Pharmatechnik.Nav.Language.CodeFixes;
using Pharmatechnik.Nav.Language.Extension.Common;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.CodeFixes; 

//// ISuggestedActionsSource2
//class CatSet: ISuggestedActionCategorySet {

//    public CatSet() {
//        ISuggestedActionCategoryRegistryService d=null;
//        var set = d.CreateSuggestedActionCategorySet(PredefinedSuggestedActionCategoryNames.Refactoring);
//    }
//    public IEnumerator<string> GetEnumerator() {
//        yield return PredefinedSuggestedActionCategoryNames.Refactoring; //Schraubenzieher
//        yield return PredefinedSuggestedActionCategoryNames.ErrorFix;    // Birne mit Error
//        yield return PredefinedSuggestedActionCategoryNames.StyleFix;    // Birne
//        yield return PredefinedSuggestedActionCategoryNames.CodeFix;     // Birne
//    }

//    public bool Contains(string categoryName) {
//        return categoryName == null || PredefinedSuggestedActionCategoryNames.Refactoring == categoryName;
//    }

//    IEnumerator IEnumerable.GetEnumerator() {
//        return GetEnumerator();
//    }

//}

/// <summary>
/// Die pro <see cref="ITextView"/> vom <see cref="CodeFixSuggestedActionsSourceProvider"/> erzeugte
/// Lightbulb-Quelle (<see cref="ISuggestedActionsSource2"/>). Sie fragt über den
/// <see cref="ICodeFixSuggestedActionProviderService"/> die Engine-Fixes für den abgefragten Bereich ab,
/// gruppiert sie nach <see cref="CodeFixCategory"/> und Anker-Bereich zu <see cref="SuggestedActionSet"/>s,
/// sortiert diese nach Nähe zum Caret und entfernt titelgleiche Dubletten. Als
/// <see cref="SemanticModelServiceDependent"/> verwirft sie ihren Cache bei jeder Änderung des semantischen
/// Modells und meldet dem Editor über <see cref="SuggestedActionsChanged"/>, dass er neu abfragen soll.
/// </summary>
partial class CodeFixSuggestedActionsSource: SemanticModelServiceDependent, ISuggestedActionsSource2 {

    readonly ISuggestedActionCategoryRegistryService _suggestedActionCategoryRegistryService;
    readonly ICodeFixSuggestedActionProviderService  _codeFixSuggestedActionProviderService;
    readonly ITextView                               _textView;

    volatile SuggestedActionSetsAndRange _cachedSuggestedActionSets;

    /// <summary>Erzeugt die Lightbulb-Quelle für einen konkreten Buffer und View.</summary>
    /// <param name="textBuffer">Der Buffer, dessen semantisches Modell die Quelle beobachtet.</param>
    /// <param name="suggestedActionCategoryRegistryService">VS-Dienst zum Bilden von Kategorie-Sets.</param>
    /// <param name="codeFixSuggestedActionProviderService">Die Aggregations-Fassade über alle Fix-Provider.</param>
    /// <param name="textView">Der Editor-View, für den Vorschläge angeboten werden.</param>
    public CodeFixSuggestedActionsSource(ITextBuffer textBuffer,
                                         ISuggestedActionCategoryRegistryService suggestedActionCategoryRegistryService,
                                         ICodeFixSuggestedActionProviderService codeFixSuggestedActionProviderService,
                                         ITextView textView)
        : base(textBuffer) {
        _suggestedActionCategoryRegistryService = suggestedActionCategoryRegistryService;
        _codeFixSuggestedActionProviderService  = codeFixSuggestedActionProviderService;
        _textView                               = textView;
    }

    /// <summary>Signalisiert dem Editor, dass sich die verfügbaren Vorschläge geändert haben und neu abgefragt werden sollten.</summary>
    public event EventHandler<EventArgs> SuggestedActionsChanged;

    /// <summary>
    /// Liefert die Telemetrie-Kennung der Quelle. Nav-Fixes nehmen an der VS-Telemetrie nicht teil und
    /// liefern stets <c>false</c>.
    /// </summary>
    /// <param name="telemetryId">Wird auf <see cref="Guid.Empty"/> gesetzt.</param>
    /// <returns>Immer <c>false</c>.</returns>
    public bool TryGetTelemetryId(out Guid telemetryId) {
        telemetryId = Guid.Empty;
        return false;
    }

    /// <summary>
    /// Ermittelt asynchron die Menge der Vorschlags-Kategorien im Bereich (für Icon/Filterung der
    /// Lightbulb). Ist <see cref="PredefinedSuggestedActionCategoryNames.Refactoring"/> nicht angefragt,
    /// werden Refactoring- und Style-Kategorien ausgeblendet.
    /// </summary>
    /// <param name="requestedActionCategories">Die vom Editor angefragten Kategorien.</param>
    /// <param name="range">Der abgefragte Quelltext-Bereich.</param>
    /// <param name="cancellationToken">Token zum Abbrechen der Anfrage.</param>
    /// <returns>Das Set der im Bereich vertretenen Kategorien.</returns>
    public Task<ISuggestedActionCategorySet> GetSuggestedActionCategoriesAsync(ISuggestedActionCategorySet requestedActionCategories, SnapshotSpan range, CancellationToken cancellationToken) {

        return Task.Factory.StartNew(GetSuggestedActionCategorySet,
                                     cancellationToken,
                                     TaskCreationOptions.None, TaskScheduler.Default);

        ISuggestedActionCategorySet GetSuggestedActionCategorySet() {

            var actions = GetOrCreateFixSuggestedActions(range, cancellationToken);

            var categories = actions.GroupBy(a => a.Category)
                                    .Select(g => g.Key)
                                    .ToList();

            if (!requestedActionCategories.Contains(PredefinedSuggestedActionCategoryNames.Refactoring)) {
                categories.Remove(CodeFixCategory.Refactoring);
                categories.Remove(CodeFixCategory.StyleFix);
            }

            var suggestions = categories.Select(ToCategoryName);

            return _suggestedActionCategoryRegistryService.CreateSuggestedActionCategorySet(suggestions);
        }

    }

    /// <summary>Bildet eine <see cref="CodeFixCategory"/> auf den zugehörigen VS-Kategorienamen (<see cref="PredefinedSuggestedActionCategoryNames"/>) ab.</summary>
    /// <param name="category">Die abzubildende Fix-Kategorie.</param>
    /// <returns>Der VS-Kategoriename; für Unbekanntes <see cref="PredefinedSuggestedActionCategoryNames.Any"/>.</returns>
    string ToCategoryName(CodeFixCategory category) {
        switch (category) {
            case CodeFixCategory.CodeFix:
                return PredefinedSuggestedActionCategoryNames.CodeFix;
            case CodeFixCategory.ErrorFix:
                return PredefinedSuggestedActionCategoryNames.ErrorFix;
            case CodeFixCategory.StyleFix:
                return PredefinedSuggestedActionCategoryNames.StyleFix;
            case CodeFixCategory.Refactoring:
                return PredefinedSuggestedActionCategoryNames.Refactoring;
            default:
                return PredefinedSuggestedActionCategoryNames.Any;
        }
    }

    /// <summary>Prüft asynchron, ob im Bereich überhaupt Vorschläge vorliegen (entscheidet, ob die Lightbulb erscheint).</summary>
    /// <param name="requestedActionCategories">Die vom Editor angefragten Kategorien.</param>
    /// <param name="range">Der abgefragte Quelltext-Bereich.</param>
    /// <param name="cancellationToken">Token zum Abbrechen der Anfrage.</param>
    /// <returns><c>true</c>, wenn mindestens ein Vorschlag existiert.</returns>
    public Task<bool> HasSuggestedActionsAsync(ISuggestedActionCategorySet requestedActionCategories, SnapshotSpan range, CancellationToken cancellationToken) {
        return Task.Factory.StartNew(() => {
                                         var actions = GetOrCreateFixSuggestedActions(range, cancellationToken);
                                         return actions.Any();
                                     },
                                     cancellationToken,
                                     TaskCreationOptions.None, TaskScheduler.Default);
    }

    /// <summary>
    /// Liefert die anzuzeigenden <see cref="SuggestedActionSet"/>s: Die Fixes im Bereich werden nach
    /// <see cref="CodeFixCategory"/> gruppiert und je Kategorie über
    /// <see cref="BuildSuggestedActionSets"/> zu nach Caret-Nähe sortierten, dublettenfreien Sets gebaut.
    /// </summary>
    /// <param name="requestedActionCategories">Die vom Editor angefragten Kategorien.</param>
    /// <param name="range">Der abgefragte Quelltext-Bereich.</param>
    /// <param name="cancellationToken">Token zum Abbrechen der Anfrage.</param>
    /// <returns>Die anzuzeigenden Vorschlags-Sets.</returns>
    public IEnumerable<SuggestedActionSet> GetSuggestedActions(ISuggestedActionCategorySet requestedActionCategories, SnapshotSpan range, CancellationToken cancellationToken) {

        var caretPoint = _textView.GetCaretPoint();
        var actions    = GetOrCreateFixSuggestedActions(range, cancellationToken);

        // Nach Katergorie gruppieren
        var actionsByCategory = actions.GroupBy(a => a.Category)
                                       .ToList();

        return actionsByCategory.SelectMany(actionsByCat => BuildSuggestedActionSets(
                                                actionsByCat.Key,
                                                actionsByCat, 
                                                range, 
                                                caretPoint));
    }

    /// <summary>
    /// Baut aus den Aktionen einer Kategorie die Vorschlags-Sets: Gruppierung nach Anker-Bereich, absteigende
    /// Sortierung nach <see cref="CodeFixSuggestedAction.Prio"/> je Set, Sortierung der Sets nach Nähe zum
    /// Caret (<see cref="SuggestedActionSetComparer"/>) und Entfernen titelgleicher Dubletten.
    /// </summary>
    /// <param name="category">Die Kategorie, deren Aktionen gebündelt werden.</param>
    /// <param name="suggestedActionSets">Die Aktionen dieser Kategorie.</param>
    /// <param name="range">Der abgefragte Bereich (Anker-Fallback, wenn eine Aktion keinen eigenen hat).</param>
    /// <param name="caretPoint">Die aktuelle Caret-Position für die Nähe-Sortierung, oder <c>null</c>.</param>
    /// <returns>Die sortierten, dublettenfreien Vorschlags-Sets.</returns>
    private IEnumerable<SuggestedActionSet> BuildSuggestedActionSets(CodeFixCategory category, IEnumerable<CodeFixSuggestedAction> suggestedActionSets, SnapshotSpan range, SnapshotPoint? caretPoint) {

        // Nach Span Gruppieren
        var groupedActions = suggestedActionSets.GroupBy(action => action.ApplicableToSpan);
        var actionSets     = new List<SuggestedActionSet>();
        foreach (var actionsInSpan in groupedActions) {
            var orderedActions = actionsInSpan.OrderByDescending(codeFixSuggestedAction => codeFixSuggestedAction.Prio);
            actionSets.Add(new SuggestedActionSet(
                               categoryName: ToCategoryName(category),
                               actions: orderedActions,
                               title: "Hi",
                               applicableToSpan: actionsInSpan.Key ?? range));
        }

        // Sortierung nach Nähe zum Caret Point
        var orderedSuggestionSets = actionSets.OrderBy(s => s, new SuggestedActionSetComparer(caretPoint, range));
        // Doppelte Actions entfernen. Es bleibt nur die zum Caret nächste Action bestehen.
        var filteredSets = FilterDuplicateTitles(orderedSuggestionSets);

        return filteredSets;
    }

    /// <summary>
    /// Entfernt titelgleiche Dubletten über alle Sets hinweg: Da die Sets bereits nach Caret-Nähe sortiert
    /// sind, bleibt je Titel nur die dem Caret nächste Aktion bestehen.
    /// </summary>
    /// <param name="actionSets">Die nach Caret-Nähe sortierten Vorschlags-Sets.</param>
    /// <returns>Die entdoppelten Sets (leere Sets werden ausgelassen).</returns>
    IEnumerable<SuggestedActionSet> FilterDuplicateTitles(IEnumerable<SuggestedActionSet> actionSets) {

        var result = new List<SuggestedActionSet>();

        var seenTitles = new HashSet<string>();

        foreach (var set in actionSets) {
            var filteredSet = FilterDuplicateTitles(set, seenTitles);
            if (filteredSet != null) {
                result.Add(filteredSet);
            }
        }

        return result.ToImmutableArray();
    }

    /// <summary>
    /// Filtert aus einem einzelnen Set die Aktionen heraus, deren <see cref="ISuggestedAction.DisplayText"/>
    /// bereits gesehen wurde, und vermerkt die verbleibenden in <paramref name="seenTitles"/>.
    /// </summary>
    /// <param name="actionSet">Das zu filternde Set.</param>
    /// <param name="seenTitles">Die bereits vergebenen Titel (wird fortgeschrieben).</param>
    /// <returns>Ein Set mit den verbliebenen Aktionen, oder <c>null</c>, wenn keine übrig bleibt.</returns>
    SuggestedActionSet FilterDuplicateTitles(SuggestedActionSet actionSet, HashSet<string> seenTitles) {

        var actions = new List<ISuggestedAction>();

        foreach (var action in actionSet.Actions) {
            if (seenTitles.Add(action.DisplayText)) {
                actions.Add(action);
            }
        }

        return actions.Count == 0
            ? null
            : new SuggestedActionSet(
                categoryName: actionSet.CategoryName,
                actions: actions,
                title: actionSet.Title,
                priority: actionSet.Priority,
                applicableToSpan: actionSet.ApplicableToSpan);
    }

    /// <summary>
    /// Reagiert auf eine Änderung des semantischen Modells: verwirft den Cache und meldet dem Editor über
    /// <see cref="SuggestedActionsChanged"/>, dass er neu abfragen soll.
    /// </summary>
    /// <param name="sender">Die Ereignisquelle.</param>
    /// <param name="e">Der geänderte Bereich.</param>
    protected override void OnSemanticModelChanged(object sender, SnapshotSpanEventArgs e) {
        base.OnSemanticModelChanged(sender, e);
        _cachedSuggestedActionSets = null;
        InvalidateSuggestedActions();
    }

    /// <summary>Prüft, ob der Cache für den angegebenen Bereich noch gilt (gleicher Bereich, nicht <c>null</c>).</summary>
    /// <param name="cache">Der zu prüfende Cache-Eintrag, oder <c>null</c>.</param>
    /// <param name="range">Der abgefragte Bereich.</param>
    /// <returns><c>true</c>, wenn der Cache den Bereich abdeckt.</returns>
    static bool IsCacheValid(SuggestedActionSetsAndRange cache, SnapshotSpan range) {
        if (cache == null) {
            return false;
        }

        return cache.Range == range;
    }

    /// <summary>Verwirft den Cache und feuert <see cref="SuggestedActionsChanged"/>.</summary>
    void InvalidateSuggestedActions() {
        _cachedSuggestedActionSets = null;
        SuggestedActionsChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Liefert die Fixes für den Bereich aus dem Cache oder berechnet sie über <see cref="BuildSuggestedActions"/> neu.</summary>
    /// <param name="range">Der abgefragte Bereich.</param>
    /// <param name="cancellationToken">Token zum Abbrechen der Berechnung.</param>
    /// <returns>Die Fix-Aktionen des Bereichs (ggf. leer).</returns>
    private ImmutableList<CodeFixSuggestedAction> GetOrCreateFixSuggestedActions(SnapshotSpan range, CancellationToken cancellationToken) {

        var cachedActionSets = _cachedSuggestedActionSets;
        ImmutableList<CodeFixSuggestedAction> suggestedActionSets =
            IsCacheValid(cachedActionSets, range) ? cachedActionSets.SuggestedActionSets : BuildSuggestedActions(range, cancellationToken);
        return suggestedActionSets;
    }

    /// <summary>
    /// Berechnet die Fixes für den Bereich neu: Nur wenn ein aktuelles semantisches Modell zum Snapshot des
    /// Bereichs vorliegt, baut sie über den <see cref="ICodeFixSuggestedActionProviderService"/> die Aktionen
    /// und legt sie im Cache ab; andernfalls wird der Cache geleert und eine leere Liste geliefert.
    /// </summary>
    /// <param name="range">Der abgefragte Bereich.</param>
    /// <param name="cancellationToken">Token zum Abbrechen der Berechnung.</param>
    /// <returns>Die berechneten Fix-Aktionen (leer, wenn kein aktuelles Modell vorliegt oder abgebrochen wurde).</returns>
    protected ImmutableList<CodeFixSuggestedAction> BuildSuggestedActions(SnapshotSpan range, CancellationToken cancellationToken) {

        var codeGenerationUnitAndSnapshot = SemanticModelService?.CodeGenerationUnitAndSnapshot;
        if (codeGenerationUnitAndSnapshot == null || !codeGenerationUnitAndSnapshot.IsCurrent(range.Snapshot)) {
            _cachedSuggestedActionSets = null;
            return ImmutableList<CodeFixSuggestedAction>.Empty;
        }

        var parameter  = new CodeFixSuggestedActionParameter(range, codeGenerationUnitAndSnapshot, _textView);
        var actionsets = _codeFixSuggestedActionProviderService.GetCodeFixSuggestedActions(parameter, cancellationToken).ToImmutableList();

        if (cancellationToken.IsCancellationRequested || actionsets.Count == 0) {
            return ImmutableList<CodeFixSuggestedAction>.Empty;
        }

        var actionsetsAndRange = new SuggestedActionSetsAndRange(range, actionsets);

        _cachedSuggestedActionSets = actionsetsAndRange;

        return actionsetsAndRange.SuggestedActionSets;
    }

    /// <summary>Der Cache-Eintrag der Quelle: die für einen bestimmten <see cref="SnapshotSpan"/>-Bereich berechneten Fix-Aktionen.</summary>
    sealed class SuggestedActionSetsAndRange {

        /// <summary>Bindet die berechneten Aktionen an den Bereich, für den sie gelten.</summary>
        /// <param name="range">Der Bereich, für den die Aktionen berechnet wurden.</param>
        /// <param name="suggestedActionSets">Die für den Bereich berechneten Aktionen.</param>
        public SuggestedActionSetsAndRange(SnapshotSpan range, ImmutableList<CodeFixSuggestedAction> suggestedActionSets) {
            Range               = range;
            SuggestedActionSets = suggestedActionSets;
        }

        /// <summary>Der Bereich, für den die Aktionen gültig sind.</summary>
        public SnapshotSpan                          Range               { get; }
        /// <summary>Die für den <see cref="Range"/> berechneten Fix-Aktionen.</summary>
        public ImmutableList<CodeFixSuggestedAction> SuggestedActionSets { get; }

    }

}
#region Using Directives

using System.Collections.Immutable;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using JetBrains.Annotations;

using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion.Data;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;

using Pharmatechnik.Nav.Language.Completion;
using Pharmatechnik.Nav.Language.Extension.QuickInfo;
using Pharmatechnik.Nav.Language.Text;
using Pharmatechnik.Nav.Utilities.IO;

using ThreadHelper = Microsoft.VisualStudio.Shell.ThreadHelper;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.Completion; 

/// <summary>
/// Basisklasse der VS-Host-Completion-Quelle. Erfüllt den VS-SDK-Vertrag <see cref="IAsyncCompletionSource"/>
/// und bildet die neutralen Vorschläge des geteilten Engine-Kerns (<see cref="NavCompletionService"/>,
/// <see cref="NavCompletionItem"/>) auf reiche VS-Completion-Items (Icon, Filter, QuickInfo-Tooltip,
/// per-Item-Ersetzungsbereich) ab. Die Tooltips baut der <see cref="QuickinfoBuilderService"/>; die
/// konkrete Ableitung ist <see cref="NavCompletionSource"/>.
/// </summary>
abstract class AsyncCompletionSource: IAsyncCompletionSource {

    protected AsyncCompletionSource(QuickinfoBuilderService quickinfoBuilderService) {
        QuickinfoBuilderService = quickinfoBuilderService;

    }

    /// <summary>Der Dienst, der die QuickInfo-Tooltips der Completion-Items (Symbol, Keyword, Datei) rendert.</summary>
    public QuickinfoBuilderService QuickinfoBuilderService { get; }

    /// <summary>
    /// Entscheidet, ob der gegebene <paramref name="trigger"/> überhaupt eine Completion-Session eröffnen
    /// soll: explizites Aufrufen (<see cref="CompletionTriggerReason.Invoke"/>) löst immer aus, ein
    /// gedrücktes Enter nie; alles Übrige entscheidet <see cref="ShouldTriggerCompletionOverride"/>.
    /// </summary>
    protected bool ShouldTriggerCompletion(CompletionTrigger trigger) {
        // The trigger reason guarantees that user wants a completion.
        if (trigger.Reason == CompletionTriggerReason.Invoke ||
            trigger.Reason == CompletionTriggerReason.InvokeAndCommitIfUnique) {
            return true;
        }

        // Enter does not trigger completion.
        if (trigger.Reason == CompletionTriggerReason.Insertion && trigger.Character == '\n') {
            return false;
        }

        return ShouldTriggerCompletionOverride(trigger);
    }

    /// <summary>
    /// Ob das getippte Zeichen die Completion auslösen soll. Bezieht die Sonderzeichen aus der einen
    /// Autorität <see cref="NavCompletionService.TriggerCharacters"/> (Buchstaben lösen ohnehin aus);
    /// welche Vorschläge dann tatsächlich kommen, entscheidet je Quelle <c>ShouldProvideCompletions</c>.
    /// </summary>
    protected virtual bool ShouldTriggerCompletionOverride(CompletionTrigger trigger) {
        return char.IsLetter(trigger.Character) ||
               NavCompletionService.IsTriggerCharacter(trigger.Character);
    }


    /// <summary>
    /// VS-SDK-Vertrag: legt fest, ob diese Quelle an der Session teilnimmt, und liefert dann den
    /// Ersetzungsbereich (<c>applicableToSpan</c>). Von der Ableitung implementiert.
    /// </summary>
    public abstract CompletionStartData InitializeCompletion(CompletionTrigger trigger, SnapshotPoint triggerLocation, CancellationToken token);

    /// <summary>
    /// VS-SDK-Vertrag: liefert die Vorschlagsliste (<see cref="CompletionContext"/>) für die Session.
    /// Von der Ableitung implementiert.
    /// </summary>
    public abstract Task<CompletionContext> GetCompletionContextAsync(IAsyncCompletionSession session, CompletionTrigger trigger, SnapshotPoint triggerLocation, SnapshotSpan applicableToSpan, CancellationToken token);

    /// <summary>
    /// VS-SDK-Vertrag: baut den QuickInfo-Tooltip für ein hervorgehobenes Completion-Item. Je nach
    /// mitgeführter Eigenschaft (Symbol, Keyword, <see cref="FileInfo"/>) über den
    /// <see cref="QuickinfoBuilderService"/> gerendert; sonst der reine Anzeigetext.
    /// </summary>
    public virtual async Task<object> GetDescriptionAsync(IAsyncCompletionSession session, CompletionItem item, CancellationToken token) {

        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

        if (item.Properties.TryGetProperty<ISymbol>(SymbolPropertyName, out var symbol)) {
            return QuickinfoBuilderService.BuildSymbolQuickInfoContent(symbol);
        }

        if (item.Properties.TryGetProperty<string>(KeywordPropertyName, out var keyword)) {
            item.Properties.TryGetProperty<string>(KeywordDescriptionPropertyName, out var description);
            return QuickinfoBuilderService.BuildKeywordQuickInfoContent(keyword, description ?? "");
        }

        if (item.Properties.TryGetProperty<FileInfo>(NavFileInfoPropertyName, out var fileInfo)) {
            return QuickinfoBuilderService.BuildNavFileInfoQuickInfoContent(fileInfo);
        }

        return item.DisplayText;
    }

    /// <summary>Verpackt <see cref="CreateCompletionContext"/> in einen bereits abgeschlossenen Task.</summary>
    protected static Task<CompletionContext> CreateCompletionContextTaskAsync(ImmutableArray<CompletionItem>.Builder itemsBuilder,
                                                                              InitialSelectionHint initialSelectionHint = InitialSelectionHint.SoftSelection) {
        return Task.FromResult(CreateCompletionContext(itemsBuilder, initialSelectionHint));
    }

    /// <summary>Baut aus den gesammelten Items einen <see cref="CompletionContext"/> mit gegebener Vorauswahl.</summary>
    protected static CompletionContext CreateCompletionContext(ImmutableArray<CompletionItem>.Builder itemsBuilder,
                                                               InitialSelectionHint initialSelectionHint = InitialSelectionHint.SoftSelection) {
        return new CompletionContext(itemsBuilder.ToImmutable(), null, initialSelectionHint);
    }

    /// <summary>Verpackt <see cref="CreateEmptyCompletionContext"/> in einen bereits abgeschlossenen Task.</summary>
    protected static Task<CompletionContext> CreateEmptyCompletionContextTaskAsync() {
        return Task.FromResult(CreateEmptyCompletionContext());
    }

    /// <summary>Ein leerer <see cref="CompletionContext"/> (keine Vorschläge).</summary>
    protected static CompletionContext CreateEmptyCompletionContext() {
        return new CompletionContext(ImmutableArray<CompletionItem>.Empty);
    }

    /// <summary>
    /// Baut ein VS-Completion-Item aus einem Nav-<paramref name="symbol"/>: Anzeigetext = Symbolname,
    /// Icon und Filter aus der Symbolart (<see cref="CompletionImages.FromSymbol"/>,
    /// <see cref="CompletionFilters.TryGetFromSymbol"/>). Das Symbol wird für den Tooltip am Item mitgeführt.
    /// </summary>
    protected CompletionItem CreateSymbolCompletion(ISymbol symbol, string description) {

        var filter = CompletionFilters.TryGetFromSymbol(symbol);

        var filters = filter != null ? ImmutableArray.Create(filter) : ImmutableArray<CompletionFilter>.Empty;

        var completionItem = new CompletionItem(displayText: symbol.Name,
                                                source: this,
                                                icon: CompletionImages.FromSymbol(symbol),
                                                filters: filters);

        completionItem.Properties.AddProperty(SymbolPropertyName, symbol);

        return completionItem;
    }

    /// <summary>
    /// Bildet einen neutralen <see cref="NavCompletionItem"/> des Engine-Service auf ein reiches VS-Item ab.
    /// Pfad-Vorschläge (<see cref="NavCompletionItemKind.File"/>) werden zu Datei-Items (relativer Einfügetext,
    /// QuickInfo-<see cref="FileInfo"/>); symbolbasierte Vorschläge behalten Icon und QuickInfo-Tooltip (über
    /// das mitgeführte Symbol); alles Übrige wird zum Keyword-Item. Trägt das Engine-Item einen
    /// <see cref="NavCompletionItem.ReplacementExtent"/> (Pfade, Edge-Keywords), wird der per-Item-Ersetzungs-
    /// bereich über den <see cref="ReplacementTrackingSpanProperty"/>-Pfad angehängt — sonst gilt der
    /// Identifier-Span der Session.
    /// </summary>
    protected CompletionItem ToCompletionItem(NavCompletionItem item, ITextSnapshot snapshot, [CanBeNull] DirectoryInfo navDirectory) {

        if (item.Kind == NavCompletionItemKind.File) {
            return CreatePathCompletion(item, snapshot, navDirectory);
        }

        var completionItem = item.Symbol != null
            ? CreateSymbolCompletion(item.Symbol, item.Label)
            : CreateKeywordCompletion(item.Label, item.Description);

        if (item.ReplacementExtent is { } extent) {
            ApplyReplacementExtent(completionItem, snapshot, extent);
        }

        return completionItem;
    }

    protected CompletionItem CreateKeywordCompletion(string keyword, [CanBeNull] string description = null) {

        var completionItem = new CompletionItem(displayText: keyword,
                                                source: this,
                                                icon: CompletionImages.Keyword,
                                                filters: ImmutableArray.Create(CompletionFilters.Keywords)
        );

        completionItem.Properties.AddProperty(KeywordPropertyName, keyword);
        // Die (bereits kontextabhängig aufgelöste) Bedeutung des Engine-Items mitführen, damit der Tooltip
        // sie NICHT aus dem bloßen Keyword-Literal rekonstruieren muss (was den Wirt-Kontext verlöre).
        completionItem.Properties.AddProperty(KeywordDescriptionPropertyName, description ?? "");

        return completionItem;
    }

    /// <summary>
    /// Bildet einen Pfad-Vorschlag der Engine (Kind <see cref="NavCompletionItemKind.File"/>, mit relativem
    /// <see cref="NavCompletionItem.InsertText"/> und dem <see cref="NavCompletionItem.ReplacementExtent"/>
    /// über den gesamten String-Inhalt) auf ein VS-Item ab: GEFILTERT wird über den Dateinamen
    /// (<see cref="NavCompletionItem.Label"/>), EINGEFÜGT der relative Pfad, ERSETZT der gesamte Inhalt
    /// zwischen den <c>"</c> (per-Item-Replacement über den <see cref="ReplacementTrackingSpanProperty"/>-Pfad).
    /// Die QuickInfo-<see cref="FileInfo"/> wird aus dem relativen Pfad rekonstruiert (das neutrale
    /// Engine-Item führt kein <see cref="FileInfo"/> mit).
    /// </summary>
    protected CompletionItem CreatePathCompletion(NavCompletionItem item, ITextSnapshot snapshot, [CanBeNull] DirectoryInfo navDirectory) {

        var completionItem = new CompletionItem(displayText: item.Label,
                                                source: this,
                                                icon: CompletionImages.NavFile,
                                                filters: ImmutableArray.Create(CompletionFilters.Files),
                                                suffix: "",
                                                insertText: item.InsertText,
                                                sortText: $"_{item.Label}",
                                                filterText: item.Label,
                                                attributeIcons: ImmutableArray<ImageElement>.Empty);

        if (item.ReplacementExtent is { } extent) {
            ApplyReplacementExtent(completionItem, snapshot, extent);
        }

        if (navDirectory != null && PathHelper.TryCombinePath(navDirectory.FullName, item.InsertText, out var fullPath)) {
            completionItem.Properties.AddProperty(NavFileInfoPropertyName, new FileInfo(fullPath));
        }

        return completionItem;
    }

    /// <summary>
    /// Hängt einem VS-Item den per-Item-Ersetzungsbereich an (absolute Dokument-Offsets aus dem Engine-Item):
    /// beim Commit ersetzt der <see cref="CompletionCommitManager"/> genau diesen (mitwachsenden) Bereich durch
    /// den Einfügetext — statt des Identifier-Spans der Session. So ersetzt ein Pfad den gesamten String-Inhalt
    /// und ein Edge-Keyword die bereits getippten Edge-Zeichen.
    /// </summary>
    static void ApplyReplacementExtent(CompletionItem completionItem, ITextSnapshot snapshot, TextExtent extent) {
        var replacementSpan = snapshot.CreateTrackingSpan(new Span(extent.Start, extent.Length), SpanTrackingMode.EdgeInclusive);
        completionItem.Properties.AddProperty(ReplacementTrackingSpanProperty, replacementSpan);
    }

    // ReSharper disable InconsistentNaming
    /// <summary>Schlüssel, unter dem am Completion-Item das zugehörige Nav-<see cref="ISymbol"/> für den Tooltip abgelegt wird.</summary>
    public static string SymbolPropertyName            => nameof(SymbolPropertyName);
    /// <summary>Schlüssel, unter dem am Completion-Item das Keyword-Literal abgelegt wird.</summary>
    public static string KeywordPropertyName           => nameof(KeywordPropertyName);
    /// <summary>Schlüssel, unter dem am Completion-Item die (kontextabhängig aufgelöste) Keyword-Beschreibung abgelegt wird.</summary>
    public static string KeywordDescriptionPropertyName => nameof(KeywordDescriptionPropertyName);
    /// <summary>Schlüssel, unter dem am Completion-Item die <see cref="FileInfo"/> eines Pfad-Vorschlags abgelegt wird.</summary>
    public static string NavFileInfoPropertyName       => nameof(NavFileInfoPropertyName);

    /// <summary>
    /// Schlüssel, unter dem am Completion-Item der per-Item-Ersetzungsbereich (<see cref="ITrackingSpan"/>)
    /// abgelegt wird; der <see cref="CompletionCommitManager"/> ersetzt beim Commit genau diesen Bereich.
    /// </summary>
    public static string ReplacementTrackingSpanProperty => nameof(ReplacementTrackingSpanProperty);
    // ReSharper restore InconsistentNaming

    /// <summary>
    /// Ermittelt das aktuelle <see cref="CodeGenerationUnit"/> (Semantikmodell) für die Trigger-Position —
    /// synchron über den <see cref="SemanticModelService"/> des zugehörigen TextBuffers. Nur auf dem UI-Thread.
    /// </summary>
    protected static CodeGenerationUnit GetCodeGenerationUnit(SnapshotPoint triggerLocation) {

        ThreadHelper.ThrowIfNotOnUIThread();

        var semanticModelService = SemanticModelService.GetOrCreateSingelton(triggerLocation.Snapshot.TextBuffer);

        var generationUnitAndSnapshot = semanticModelService.UpdateSynchronously();
        var codeGenerationUnit        = generationUnitAndSnapshot.CodeGenerationUnit;

        return codeGenerationUnit;
    }

}
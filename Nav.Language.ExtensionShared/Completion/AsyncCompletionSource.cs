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
using Pharmatechnik.Nav.Utilities.IO;

using ThreadHelper = Microsoft.VisualStudio.Shell.ThreadHelper;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.Completion; 

abstract class AsyncCompletionSource: IAsyncCompletionSource {

    protected AsyncCompletionSource(QuickinfoBuilderService quickinfoBuilderService) {
        QuickinfoBuilderService = quickinfoBuilderService;

    }

    public QuickinfoBuilderService QuickinfoBuilderService { get; }

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


    public abstract CompletionStartData InitializeCompletion(CompletionTrigger trigger, SnapshotPoint triggerLocation, CancellationToken token);

    public abstract Task<CompletionContext> GetCompletionContextAsync(IAsyncCompletionSession session, CompletionTrigger trigger, SnapshotPoint triggerLocation, SnapshotSpan applicableToSpan, CancellationToken token);

    public virtual async Task<object> GetDescriptionAsync(IAsyncCompletionSession session, CompletionItem item, CancellationToken token) {

        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

        if (item.Properties.TryGetProperty<ISymbol>(SymbolPropertyName, out var symbol)) {
            return QuickinfoBuilderService.BuildSymbolQuickInfoContent(symbol);
        }

        if (item.Properties.TryGetProperty<string>(KeywordPropertyName, out var keyword)) {
            return QuickinfoBuilderService.BuildKeywordQuickInfoContent(keyword);
        }

        if (item.Properties.TryGetProperty<DirectoryInfo>(DirectoryInfoPropertyName, out var dirInfo)) {
            return QuickinfoBuilderService.BuildDirectoryInfoQuickInfoContent(dirInfo);
        }

        if (item.Properties.TryGetProperty<FileInfo>(NavFileInfoPropertyName, out var fileInfo)) {
            return QuickinfoBuilderService.BuildNavFileInfoQuickInfoContent(fileInfo);
        }

        return item.DisplayText;
    }

    protected static Task<CompletionContext> CreateCompletionContextTaskAsync(ImmutableArray<CompletionItem>.Builder itemsBuilder,
                                                                              InitialSelectionHint initialSelectionHint = InitialSelectionHint.SoftSelection) {
        return Task.FromResult(CreateCompletionContext(itemsBuilder, initialSelectionHint));
    }

    protected static CompletionContext CreateCompletionContext(ImmutableArray<CompletionItem>.Builder itemsBuilder,
                                                               InitialSelectionHint initialSelectionHint = InitialSelectionHint.SoftSelection) {
        return new CompletionContext(itemsBuilder.ToImmutable(), null, initialSelectionHint);
    }

    protected static Task<CompletionContext> CreateEmptyCompletionContextTaskAsync() {
        return Task.FromResult(CreateEmptyCompletionContext());
    }

    protected static CompletionContext CreateEmptyCompletionContext() {
        return new CompletionContext(ImmutableArray<CompletionItem>.Empty);
    }

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
    /// Bildet einen neutralen <see cref="NavCompletionItem"/> des Engine-Service auf ein reiches VS-Item ab:
    /// symbolbasierte Vorschläge behalten Icon und QuickInfo-Tooltip (über das mitgeführte Symbol),
    /// Keyword-Vorschläge werden zu Keyword-Items.
    /// </summary>
    protected CompletionItem ToCompletionItem(NavCompletionItem item) {
        return item.Symbol != null
            ? CreateSymbolCompletion(item.Symbol, item.Label)
            : CreateKeywordCompletion(item.Label);
    }

    /// <summary>Ob der Vorschlag ein (sichtbares) Edge-Keyword ist — diese liefert die EdgeCompletionSource.</summary>
    protected static bool IsEdgeKeyword(NavCompletionItem item) {
        return item.Kind == NavCompletionItemKind.Keyword && SyntaxFacts.IsEdgeKeyword(item.Label);
    }

    protected CompletionItem CreateKeywordCompletion(string keyword) {

        var completionItem = new CompletionItem(displayText: keyword,
                                                source: this,
                                                icon: CompletionImages.Keyword,
                                                filters: ImmutableArray.Create(CompletionFilters.Keywords)
        );

        completionItem.Properties.AddProperty(KeywordPropertyName, keyword);

        return completionItem;
    }

    protected CompletionItem CreateDirectoryInfoCompletion(DirectoryInfo directory,
                                                           DirectoryInfo dir,
                                                           [CanBeNull] string displayText = null,
                                                           [CanBeNull] ImageElement icon = null,
                                                           [CanBeNull] ITrackingSpan replacementSpan = null) {

        var directoryName = directory.FullName + Path.DirectorySeparatorChar;
        var relativePath  = PathHelper.GetRelativePath(fromPath: directoryName, toPath: dir.FullName + Path.DirectorySeparatorChar);

        displayText ??= dir.Name;

        var completionItem = new CompletionItem(displayText: displayText,
                                                source: this,
                                                icon: icon ?? CompletionImages.Folder,
                                                filters: ImmutableArray.Create(CompletionFilters.Folders),
                                                suffix: "",
                                                insertText: relativePath,
                                                sortText: $"__{displayText}",
                                                filterText: displayText,
                                                attributeIcons: ImmutableArray<ImageElement>.Empty);

        completionItem.Properties.AddProperty(DirectoryInfoPropertyName, dir);
        if (replacementSpan != null) {
            completionItem.Properties.AddProperty(ReplacementTrackingSpanProperty, replacementSpan);
        }

        return completionItem;
    }

    protected CompletionItem CreateFileInfoCompletion(DirectoryInfo directory,
                                                      FileInfo file,
                                                      [CanBeNull] string displayText = null,
                                                      [CanBeNull] ITrackingSpan replacementSpan = null) {

        var directoryName = directory.FullName + Path.DirectorySeparatorChar;
        var relativePath  = PathHelper.GetRelativePath(fromPath: directoryName, toPath: file.FullName);

        displayText ??= file.Name;

        var completionItem = new CompletionItem(displayText: displayText,
                                                source: this,
                                                icon: CompletionImages.NavFile,
                                                filters: ImmutableArray.Create(CompletionFilters.Files),
                                                suffix: "",
                                                insertText: relativePath,
                                                sortText: $"_{displayText}",
                                                filterText: file.Name,
                                                attributeIcons: ImmutableArray<ImageElement>.Empty);

        completionItem.Properties.AddProperty(NavFileInfoPropertyName, file);
        if (replacementSpan != null) {
            completionItem.Properties.AddProperty(ReplacementTrackingSpanProperty, replacementSpan);
        }

        return completionItem;
    }

    // ReSharper disable InconsistentNaming
    public static string SymbolPropertyName        => nameof(SymbolPropertyName);
    public static string KeywordPropertyName       => nameof(KeywordPropertyName);
    public static string DirectoryInfoPropertyName => nameof(DirectoryInfoPropertyName);
    public static string NavFileInfoPropertyName   => nameof(NavFileInfoPropertyName);

    public static string ReplacementTrackingSpanProperty => nameof(ReplacementTrackingSpanProperty);
    // ReSharper restore InconsistentNaming

    protected static CodeGenerationUnit GetCodeGenerationUnit(SnapshotPoint triggerLocation) {

        ThreadHelper.ThrowIfNotOnUIThread();

        var semanticModelService = SemanticModelService.GetOrCreateSingelton(triggerLocation.Snapshot.TextBuffer);

        var generationUnitAndSnapshot = semanticModelService.UpdateSynchronously();
        var codeGenerationUnit        = generationUnitAndSnapshot.CodeGenerationUnit;

        return codeGenerationUnit;
    }

}
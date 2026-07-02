#region Using Directives

using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion.Data;

using Pharmatechnik.Nav.Language.Completion;
using Pharmatechnik.Nav.Language.Extension.QuickInfo;
using Pharmatechnik.Nav.Language.Text;

using Task = System.Threading.Tasks.Task;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.Completion; 

// ReSharper disable once InconsistentNaming
class NavCompletionSource: AsyncCompletionSource {

    public NavCompletionSource(QuickinfoBuilderService quickinfoBuilderService): base(quickinfoBuilderService) {

    }

    public override CompletionStartData InitializeCompletion(CompletionTrigger trigger, SnapshotPoint triggerLocation, CancellationToken token) {

        ThreadHelper.ThrowIfNotOnUIThread();

        if (!ShouldTriggerCompletion(trigger)) {
            return CompletionStartData.DoesNotParticipateInCompletion;
        }

        var codeGenerationUnit = GetCodeGenerationUnit(triggerLocation);

        if (ShouldProvideCompletions(triggerLocation, codeGenerationUnit, out var applicableToSpan)) {
            return new CompletionStartData(CompletionParticipation.ProvidesItems, applicableToSpan);
        }

        return CompletionStartData.DoesNotParticipateInCompletion;
    }

    public override async Task<CompletionContext> GetCompletionContextAsync(IAsyncCompletionSession session, CompletionTrigger trigger, SnapshotPoint triggerLocation, SnapshotSpan applicableToSpan, CancellationToken token) {

        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

        var codeGenerationUnit = GetCodeGenerationUnit(triggerLocation);

        if (!ShouldProvideCompletions(triggerLocation, codeGenerationUnit, out var myApplicableToSpan) ||
            myApplicableToSpan != applicableToSpan) {
            return CreateEmptyCompletionContext();
        }

        // Pfad-Vervollständigung (in `taskref "…"`) braucht die Solution — sie kennt alle erreichbaren *.nav.
        // Nur im String-Kontext holen (sonst irrelevant; der Snapshot ist gecacht und damit billig).
        var line     = triggerLocation.GetContainingLine();
        var inString = line.GetText().IsInQuotation(triggerLocation - line.Start);
        var solution = inString ? await NavLanguagePackage.GetSolutionAsync(token) : null;

        await Task.Yield();

        var navDirectory    = codeGenerationUnit.Syntax.SyntaxTree.SourceText.FileInfo?.Directory;
        var completionItems = ImmutableArray.CreateBuilder<CompletionItem>();

        // Eine Quelle der Wahrheit: der Engine-Service entscheidet kontextsensitiv über den Syntaxbaum, was
        // an dieser Position sinnvoll ist (inkl. Pfaden in `taskref "…"`). Diese Quelle zeigt alles AUSSER den
        // Edge-Keywords — die liefert EdgeCompletionSource mit ihrem eigenen Ersetzungsbereich.
        foreach (var item in NavCompletionService.GetCompletions(codeGenerationUnit, triggerLocation, solution)) {

            if (IsEdgeKeyword(item)) {
                continue;
            }

            completionItems.Add(item.Kind == NavCompletionItemKind.File
                                     ? CreatePathCompletion(item, triggerLocation.Snapshot, navDirectory)
                                     : ToCompletionItem(item));
        }

        return CreateCompletionContext(completionItems);
    }

    bool ShouldProvideCompletions(SnapshotPoint triggerLocation, CodeGenerationUnit codeGenerationUnit, out SnapshotSpan applicableToSpan) {

        applicableToSpan = default;

        // Keine Autocompletion in Kommentaren! — aus der angehängten Trivia (Roslyn-Modell), nicht über ein
        // FindToken auf den flachen Strom.
        if (codeGenerationUnit.Syntax.SyntaxTree.IsPositionInComment(triggerLocation)) {
            return false;
        }

        var line         = triggerLocation.GetContainingLine();
        var linePosition = triggerLocation - line.Start;
        var lineText     = line.GetText();

        // Zeichenketten ("…"): NICHT mehr pauschal unterdrücken — die Engine liefert innerhalb von `taskref "…"`
        // die Pfad-Vervollständigung (sonst leer). Gefiltert wird über den getippten DATEINAME-Teil (hinter dem
        // letzten Pfadtrenner), damit der Client gegen den Dateinamen matcht; den Ersetzungsbereich (gesamter
        // String-Inhalt) trägt jedes Pfad-Item selbst über NavCompletionItem.ReplacementExtent.
        if (lineText.IsInQuotation(linePosition)) {
            applicableToSpan = new SnapshotSpan(line.GetStartOfFileNamePart(triggerLocation), triggerLocation);
            return true;
        }

        // Code-Blöcke ([ … ]) werden NICHT mehr hier unterdrückt: die Engine entscheidet über den Syntaxbaum,
        // ob im Schlüsselwort-Slot direkt hinter `[` die Code-Block-Keywords angeboten werden (sonst liefert
        // sie eine leere Liste). Damit entfällt die frühere, separate CodeCompletionSource.

        var start = line.GetStartOfIdentifier(triggerLocation);

        applicableToSpan = new SnapshotSpan(start, triggerLocation);

        return true;
    }

}
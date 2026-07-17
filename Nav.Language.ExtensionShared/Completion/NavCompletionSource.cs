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
/// <summary>
/// Die konkrete Nav-Completion-Quelle (siehe <see cref="AsyncCompletionSource"/>). Entscheidet über den
/// Syntaxbaum, ob und wo Vorschläge sinnvoll sind (nicht in Kommentaren; innerhalb <c>taskref "…"</c> die
/// Pfad-Vervollständigung), und speist die neutralen Vorschläge des Engine-Kerns
/// <see cref="NavCompletionService"/> in reiche VS-Items ein.
/// </summary>
class NavCompletionSource: AsyncCompletionSource {

    public NavCompletionSource(QuickinfoBuilderService quickinfoBuilderService): base(quickinfoBuilderService) {

    }

    /// <summary>
    /// VS-SDK-Vertrag: prüft, ob der Trigger eine Session eröffnet und diese Quelle Vorschläge liefert.
    /// Nimmt teil, sobald <see cref="ShouldProvideCompletions"/> für die Position einen Ersetzungsbereich
    /// bestimmt; sonst <see cref="CompletionStartData.DoesNotParticipateInCompletion"/>. Nur auf dem UI-Thread.
    /// </summary>
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

    /// <summary>
    /// VS-SDK-Vertrag: liefert die Vorschlagsliste. Holt kontextsensitiv die neutralen Vorschläge von
    /// <see cref="NavCompletionService.GetCompletions"/> (im String-Kontext zusätzlich mit der Solution für
    /// die Pfad-Vervollständigung) und bildet jeden auf ein VS-Item ab (<see cref="AsyncCompletionSource.ToCompletionItem"/>).
    /// </summary>
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
        // an dieser Position sinnvoll ist (inkl. Pfaden in `taskref "…"` und Edge-Keywords). Items, die einen
        // eigenen Ersetzungsbereich brauchen (Pfade, Edge-Keywords), tragen ihn als ReplacementExtent selbst —
        // das Mapping honoriert ihn per-Item.
        foreach (var item in NavCompletionService.GetCompletions(codeGenerationUnit, triggerLocation, solution)) {
            completionItems.Add(ToCompletionItem(item, triggerLocation.Snapshot, navDirectory));
        }

        return CreateCompletionContext(completionItems);
    }

    /// <summary>
    /// Entscheidet, ob an <paramref name="triggerLocation"/> Vorschläge angeboten werden, und liefert dazu
    /// den Ersetzungsbereich (<paramref name="applicableToSpan"/>): in Kommentaren nie; innerhalb <c>"…"</c>
    /// über den getippten Dateinamen-Teil (für die Pfad-Vervollständigung); sonst über den gesamten
    /// Bezeichner unter dem Cursor.
    /// </summary>
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

        // Der Ersetzungsbereich umfasst den GESAMTEN Bezeichner unter dem Cursor (Anfang bis Ende), nicht nur bis
        // zum Cursor — sonst bleibt beim Commit der Rest hinter dem Cursor stehen (aus `dia|log` würde `dialoglog`).
        // Gefiltert wird von VS ohnehin nur mit dem Text von Bereichsanfang bis Cursor, das Erweitern nach hinten
        // ist also unschädlich.
        var start = line.GetStartOfIdentifier(triggerLocation);
        var end   = line.GetEndOfIdentifier(triggerLocation);

        applicableToSpan = new SnapshotSpan(start, end);

        return true;
    }

}
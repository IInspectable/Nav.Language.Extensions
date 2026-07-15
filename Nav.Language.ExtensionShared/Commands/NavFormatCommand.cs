#region Using Directives

using System.Collections.Generic;

using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;

using Pharmatechnik.Nav.Language.Extension.Common;
using Pharmatechnik.Nav.Language.Extension.CodeFixes;
using Pharmatechnik.Nav.Language.Formatting;
using Pharmatechnik.Nav.Language.Text;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.Commands;

/// <summary>
/// Gemeinsamer Einstiegspunkt zum Formatieren einer Nav-Datei im Editor. Wird sowohl vom
/// <see cref="FormatCommandHandler"/> (Standardbefehle „Format Document"/„Format Selection") als auch
/// von der Format-Schaltfläche in der Nav-Symbolleiste (<c>NavMarginControl</c>) genutzt, damit beide
/// exakt denselben Pfad gehen: SyntaxTree → Engine-<see cref="TextChange"/>s → undo-fähig anwenden.
/// </summary>
static class NavFormatCommand {

    /// <summary>
    /// Formatiert das gesamte Dokument: holt den aktuellen SyntaxTree, lässt <c>NavFormattingService</c> die
    /// nötigen <see cref="TextChange"/>s berechnen und wendet sie undo-fähig an.
    /// </summary>
    public static void FormatDocument(ITextView textView, ITextChangeService textChangeService) {

        ThreadHelper.ThrowIfNotOnUIThread();

        var syntaxTreeAndSnapshot = TryGetCurrentSyntaxTree(textView);
        if (syntaxTreeAndSnapshot == null) {
            return;
        }

        var changes = NavFormattingService.FormatDocument(
            syntaxTreeAndSnapshot.SyntaxTree,
            textView.GetEditorSettings(),
            textView.GetFormattingOptions());

        Apply(textView, textChangeService, changes, syntaxTreeAndSnapshot.Snapshot, undoDescription: "Format Document");
    }

    /// <summary>
    /// Formatiert nur den selektierten Bereich: begrenzt die Engine-Formatierung
    /// (<c>NavFormattingService.FormatRange</c>) auf den <see cref="TextExtent"/> der Selektion und wendet die
    /// Änderungen undo-fähig an.
    /// </summary>
    public static void FormatSelection(ITextView textView, ITextChangeService textChangeService) {

        ThreadHelper.ThrowIfNotOnUIThread();

        var syntaxTreeAndSnapshot = TryGetCurrentSyntaxTree(textView);
        if (syntaxTreeAndSnapshot == null) {
            return;
        }

        var selection = textView.Selection.StreamSelectionSpan.SnapshotSpan;
        var range     = TextExtent.FromBounds(selection.Start.Position, selection.End.Position);

        var changes = NavFormattingService.FormatRange(
            syntaxTreeAndSnapshot.SyntaxTree,
            range,
            textView.GetEditorSettings(),
            textView.GetFormattingOptions());

        Apply(textView, textChangeService, changes, syntaxTreeAndSnapshot.Snapshot, undoDescription: "Format Selection");
    }

    /// <summary>
    /// Liefert den zum aktuellen Snapshot passenden SyntaxTree über den <see cref="ParserService"/> (rein
    /// syntaktisch, kein Semantik-Build), oder <see langword="null"/>, wenn er nicht aktuell ist.
    /// </summary>
    static SyntaxTreeAndSnapshot TryGetCurrentSyntaxTree(ITextView textView) {

        // Der Formatter ist rein syntaktisch – der ParserService (SyntaxTree) genügt, ein Semantik-Build
        // ist nicht nötig. UpdateSynchronously stellt sicher, dass der SyntaxTree zum aktuellen Snapshot passt.
        var syntaxTreeAndSnapshot = ParserService.TryGet(textView.TextBuffer)?.UpdateSynchronously();

        if (syntaxTreeAndSnapshot == null || !syntaxTreeAndSnapshot.IsCurrent(textView.TextBuffer.CurrentSnapshot)) {
            return null;
        }

        return syntaxTreeAndSnapshot;
    }

    /// <summary>
    /// Wendet die berechneten <paramref name="changes"/> über den <see cref="ITextChangeService"/> undo-fähig
    /// an (mit <paramref name="undoDescription"/> als Rückgängig-Bezeichnung); bei leerer Liste passiert nichts.
    /// </summary>
    static void Apply(ITextView textView, ITextChangeService textChangeService,
                      IReadOnlyList<TextChange> changes, ITextSnapshot snapshot, string undoDescription) {

        if (changes.Count == 0) {
            return;
        }

        var textChangesAndSnapshot = new TextChangesAndSnapshot(changes, snapshot);
        textChangeService.ApplyTextChanges(textView, undoDescription, textChangesAndSnapshot);
    }

}

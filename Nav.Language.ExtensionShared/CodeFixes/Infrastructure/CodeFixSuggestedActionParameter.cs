#region Using Directives

using System;
using JetBrains.Annotations;

using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;

using Pharmatechnik.Nav.Language.CodeFixes;
using Pharmatechnik.Nav.Language.Extension.Common;
using Pharmatechnik.Nav.Language.Text;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.CodeFixes; 

/// <summary>
/// Der aufruf-spezifische Eingabe-Zustand einer Lightbulb-Anfrage: der abgefragte
/// <see cref="SnapshotSpan"/>-Bereich, das dazu passende <see cref="CodeGenerationUnitAndSnapshot"/>
/// (semantisches Modell + Snapshot) und der <see cref="ITextView"/>. Aus dem Bereich baut der
/// Konstruktor den VS-freien <see cref="CodeFixContext"/>, mit dem die Engine-Fix-Provider ihre
/// Vorschläge berechnen. Ein <see cref="CodeFixSuggestedActionParameter"/> gilt genau für den einen
/// Snapshot, aus dem er erzeugt wurde.
/// </summary>
class CodeFixSuggestedActionParameter {
        
    /// <summary>Erzeugt den Parameter für einen Bereich und baut daraus den <see cref="CodeFixContext"/>.</summary>
    /// <param name="range">Der von der Lightbulb abgefragte Quelltext-Bereich.</param>
    /// <param name="codeGenerationUnitAndSnapshot">Semantisches Modell samt zugehörigem Snapshot.</param>
    /// <param name="textView">Der Editor-View, auf den ein gewählter Fix angewandt wird.</param>
    /// <exception cref="ArgumentNullException"><paramref name="textView"/> oder <paramref name="codeGenerationUnitAndSnapshot"/> ist <c>null</c>.</exception>
    public CodeFixSuggestedActionParameter(SnapshotSpan range, CodeGenerationUnitAndSnapshot codeGenerationUnitAndSnapshot, ITextView textView) {
        TextView                      = textView                      ?? throw new ArgumentNullException(nameof(textView));
        CodeGenerationUnitAndSnapshot = codeGenerationUnitAndSnapshot ?? throw new ArgumentNullException(nameof(codeGenerationUnitAndSnapshot));
        CodeFixContext = new CodeFixContext(
            range          : new TextExtent(range.Start, range.Length),
            codeGenerationUnit: CodeGenerationUnitAndSnapshot.CodeGenerationUnit,
            textEditorSettings    : TextView.GetEditorSettings());
    }
        
    /// <summary>Das semantische Modell samt zugehörigem Snapshot, gegen das die Fixes berechnet werden.</summary>
    [NotNull]
    public CodeGenerationUnitAndSnapshot CodeGenerationUnitAndSnapshot { get; }
        
    /// <summary>Der Editor-View, auf den ein gewählter Fix angewandt wird.</summary>
    [NotNull]
    public ITextView TextView { get; }

    /// <summary>Der Textpuffer des <see cref="CodeGenerationUnitAndSnapshot"/>-Snapshots.</summary>
    [NotNull]
    public ITextBuffer TextBuffer => CodeGenerationUnitAndSnapshot.Snapshot.TextBuffer;

    /// <summary>Der aus dem Bereich gebaute VS-freie Kontext, mit dem die Engine-Fix-Provider arbeiten.</summary>
    [NotNull]
    public CodeFixContext CodeFixContext { get; }
}
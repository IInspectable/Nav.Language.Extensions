#region Using Directives

using System;
using System.Linq;
using System.Collections.Generic;

using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.Diagnostics;

/// <summary>
/// Tagger, der die Diagnosen einer <c>.nav</c>-Datei als <see cref="DiagnosticErrorTag"/> (Schlangenlinien
/// im Editor) liefert. Als <see cref="SemanticModelServiceDependent"/> bezieht er Syntax- und
/// Semantik-Diagnosen aus dem <see cref="SemanticModelService"/> und aktualisiert die Tags bei jeder
/// Modelländerung. Erzeugt vom <see cref="DiagnosticErrorTaggerProvider"/>.
/// </summary>
sealed class DiagnosticErrorTagger : SemanticModelServiceDependent, ITagger<DiagnosticErrorTag> {

    DiagnosticErrorTagger(ITextBuffer textBuffer): base(textBuffer) {
    }

    /// <summary>Fabrikmethode, die einen Tagger für den <paramref name="textBuffer"/> erzeugt (vom Provider aufgerufen).</summary>
    public static ITagger<T> Create<T>(ITextBuffer textBuffer) where T : ITag {
        return new DiagnosticErrorTagger(textBuffer) as ITagger<T>;
    }

    /// <summary>
    /// Liefert die Fehler-Tags für die angefragten <paramref name="spans"/>. Syntaxfehler werden immer
    /// gemeldet; die kontextabhängigen Semantik-Diagnosen (z.B. <c>taskref</c>-/Include-Auflösung) bleiben in
    /// read-only-Ansichten (Annotate/Blame, Diff, History) außen vor, da dort der Workspace-Kontext fehlt und
    /// nur Falsch-Fehler entstünden.
    /// </summary>
    public IEnumerable<ITagSpan<DiagnosticErrorTag>> GetTags(NormalizedSnapshotSpanCollection spans) {

        var codeGenerationUnitAndSnapshot = SemanticModelService.CodeGenerationUnitAndSnapshot;
        if(codeGenerationUnitAndSnapshot == null) {
            yield break;
        }

        var syntaxTree         = codeGenerationUnitAndSnapshot.CodeGenerationUnit.Syntax.SyntaxTree;
        var codeGenerationUnit = codeGenerationUnitAndSnapshot.CodeGenerationUnit;

        // In read-only-Ansichten (Annotate/Blame, Diff/Vergleich, History) fehlt der Workspace-Kontext.
        // Die kontextabhängige Semantikanalyse (z.B. die taskref-/Include-Auflösung) erzeugt dort nur
        // eine Lawine von Falsch-Fehlern. Syntaxfehler hingegen sind unabhängig vom Kontext gültig und
        // bleiben sichtbar.
        var includeSemanticDiagnostics = !TextBuffer.IsReadOnly(0);

        foreach (var span in spans) {

            //==================
            // Syntax Fehler
            foreach (var diagnostic in syntaxTree.Diagnostics.SelectMany(diag=> diag.ExpandLocations())) {
                if (diagnostic.Location.Start <= span.End && diagnostic.Location.End >= span.Start) {

                    var errorSpan = new SnapshotSpan(codeGenerationUnitAndSnapshot.Snapshot, new Span(diagnostic.Location.Start, diagnostic.Location.Length));

                    var errorTag = new TagSpan<DiagnosticErrorTag>(
                        errorSpan.TranslateTo(span.Snapshot, SpanTrackingMode.EdgeExclusive),
                        new DiagnosticErrorTag(diagnostic));

                    yield return errorTag;
                }
            }

            if (!includeSemanticDiagnostics) {
                continue;
            }

            //==================
            // Semantic Fehler
            foreach (var diagnostic in codeGenerationUnit.Diagnostics.SelectMany(diag => diag.ExpandLocations())) {
                if (diagnostic.Location.Start <= span.End && diagnostic.Location.End >= span.Start) {

                    var errorSpan = new SnapshotSpan(codeGenerationUnitAndSnapshot.Snapshot, new Span(diagnostic.Location.Start, diagnostic.Location.Length));

                    var errorTag = new TagSpan<DiagnosticErrorTag>(
                        errorSpan.TranslateTo(span.Snapshot, SpanTrackingMode.EdgeExclusive),
                        new DiagnosticErrorTag(diagnostic));

                    yield return errorTag;
                }
            }
        }
    }

    /// <summary>Wird ausgelöst, wenn sich die Menge der Diagnose-Tags geändert hat (nach einer Modelländerung).</summary>
    public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

    /// <summary>Meldet über <see cref="TagsChanged"/>, dass die Diagnosen nach einer Semantikmodell-Änderung neu abzufragen sind.</summary>
    protected override void OnSemanticModelChanged(object sender, SnapshotSpanEventArgs snapshotSpanEventArgs) {
        TagsChanged?.Invoke(this, snapshotSpanEventArgs);
    }       
}
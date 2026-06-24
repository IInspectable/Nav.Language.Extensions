#region Using Directives

using System;
using System.Linq;
using System.Collections.Generic;

using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.Diagnostics; 

sealed class DiagnosticErrorTagger : SemanticModelServiceDependent, ITagger<DiagnosticErrorTag> {
        
    DiagnosticErrorTagger(ITextBuffer textBuffer): base(textBuffer) {
    }

    public static ITagger<T> Create<T>(ITextBuffer textBuffer) where T : ITag {
        return new DiagnosticErrorTagger(textBuffer) as ITagger<T>;
    }

    public IEnumerable<ITagSpan<DiagnosticErrorTag>> GetTags(NormalizedSnapshotSpanCollection spans) {

        var codeGenerationUnitAndSnapshot = SemanticModelService.CodeGenerationUnitAndSnapshot;
        if(codeGenerationUnitAndSnapshot == null) {
            yield break;
        }

        var syntaxTree         = codeGenerationUnitAndSnapshot.CodeGenerationUnit.Syntax.SyntaxTree;
        var codeGenerationUnit = codeGenerationUnitAndSnapshot.CodeGenerationUnit;

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

    public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

    protected override void OnSemanticModelChanged(object sender, SnapshotSpanEventArgs snapshotSpanEventArgs) {
        TagsChanged?.Invoke(this, snapshotSpanEventArgs);
    }       
}
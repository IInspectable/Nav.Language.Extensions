#region Using Directives

using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;

using Pharmatechnik.Nav.Language.Extension.GoToLocation;
using Pharmatechnik.Nav.Language.Text;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.GoTo; 

sealed class GoToTagger : SemanticModelServiceDependent, ITagger<GoToTag> {
        
    GoToTagger(ITextBuffer textBuffer) : base(textBuffer) {

    }

    public static ITagger<T> Create<T>(ITextBuffer textBuffer) where T : ITag {
        return new GoToTagger(textBuffer) as ITagger<T>;
    }

    public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

    protected override void OnSemanticModelChanged(object sender, SnapshotSpanEventArgs snapshotSpanEventArgs) {
        TagsChanged?.Invoke(this, snapshotSpanEventArgs);
    }

    public IEnumerable<ITagSpan<GoToTag>> GetTags(NormalizedSnapshotSpanCollection spans) {

        var codeGenerationUnitAndSnapshot = SemanticModelService.CodeGenerationUnitAndSnapshot;
        if (codeGenerationUnitAndSnapshot == null) {
            yield break;
        }
            
        foreach (var span in spans) {
                
            var extent  = TextExtent.FromBounds(span.Start, span.End);
            var symbols = codeGenerationUnitAndSnapshot.CodeGenerationUnit.Symbols[extent, includeOverlapping: true];

            foreach (var symbol in symbols) {

                var goToTag = GoToSymbolBuilder.Build(codeGenerationUnitAndSnapshot, symbol, TextBuffer);
                if(goToTag != null) {
                    yield return goToTag;
                }
            }
        }
    }
}
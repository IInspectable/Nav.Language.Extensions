using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;

namespace Pharmatechnik.Nav.Language.Extension.Outlining; 

class TaskReferenceOutlineTagger {

    public static IEnumerable<ITagSpan<IOutliningRegionTag>> GetTags(SyntaxTreeAndSnapshot syntaxTreeAndSnapshot, IOutliningRegionTagCreator tagCreator) {

        // Task Declarations
        foreach (var taskReferenceDefinition in syntaxTreeAndSnapshot.SyntaxTree.Root.DescendantNodes<TaskDeclarationSyntax>()) {
            var extent = taskReferenceDefinition.Extent;
               
            if (extent.Length <= 0) {
                continue;
            }

            var nameToken = taskReferenceDefinition.Identifier;

            if (nameToken.IsMissing) {
                continue;
            }

            // Die Region beginnt unmittelbar hinter dem Namen (dessen Trailing-Trivia eingeschlossen) und reicht
            // bis zum Knotenende — so bleibt der Name als Kopf der eingeklappten Region sichtbar.
            int start  = nameToken.End;
            int length = extent.End - start;

            if (length <= 0) {
                continue;
            }

            var rgnSpan  = new SnapshotSpan(new SnapshotPoint(syntaxTreeAndSnapshot.Snapshot, start),        length);
            var hintSpan = new SnapshotSpan(new SnapshotPoint(syntaxTreeAndSnapshot.Snapshot, extent.Start), extent.Length);
            var rgnTag   = tagCreator.CreateTag("...", hintSpan);

            yield return new TagSpan<IOutliningRegionTag>(rgnSpan, rgnTag);
        }


        // Zusammenhängende Blöcke von taskref "file" als Region zusammenfassen
        var allRelevant = syntaxTreeAndSnapshot.SyntaxTree.Root.DescendantNodes<TaskDefinitionSyntax>().Concat<SyntaxNode>(
                                                    syntaxTreeAndSnapshot.SyntaxTree.Root.DescendantNodes <TaskDeclarationSyntax>())
                                               .Concat(
                                                    syntaxTreeAndSnapshot.SyntaxTree.Root.DescendantNodes<IncludeDirectiveSyntax>())
                                               .OrderBy(s => s.Extent.Start);

        IncludeDirectiveSyntax firstInclude = null;
        IncludeDirectiveSyntax lastInclude  = null;
        foreach (var syntaxNode in allRelevant) {
            if (syntaxNode is IncludeDirectiveSyntax include) {
                firstInclude ??= include;
                lastInclude  =   include;
            } else {
                if (firstInclude != null && firstInclude != lastInclude) {

                    var extendStart = firstInclude.Extent;
                    var extendEnd   = lastInclude.Extent;

                    var keywordToken = firstInclude.TaskrefKeyword;
                        
                    if (keywordToken.IsMissing) {
                        yield break;
                    }

                    int start = keywordToken.End + 1;

                    int length = extendEnd.End - start;

                    var regionSpan = new SnapshotSpan(new SnapshotPoint(syntaxTreeAndSnapshot.Snapshot, start),             length);
                    var hintSpan   = new SnapshotSpan(new SnapshotPoint(syntaxTreeAndSnapshot.Snapshot, extendStart.Start), extendEnd.End - extendStart.Start);
                    var tag        = tagCreator.CreateTag("...", hintSpan);

                    yield return new TagSpan<IOutliningRegionTag>(regionSpan, tag);
                }
                firstInclude = lastInclude = null;
            }
        }
    }
}
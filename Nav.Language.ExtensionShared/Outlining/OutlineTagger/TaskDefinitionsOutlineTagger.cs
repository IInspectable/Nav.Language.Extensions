using System.Collections.Generic;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;

namespace Pharmatechnik.Nav.Language.Extension.Outlining; 

/// <summary>
/// Teil-Tagger für Outlining, der jede Task-Definition (<see cref="TaskDefinitionSyntax"/>) zu einer
/// aufklappbaren Region macht. Aufgerufen vom <see cref="OutliningTagger"/>.
/// </summary>
class TaskDefinitionsOutlineTagger {

    /// <summary>
    /// Liefert je Task-Definition eine Region, die unmittelbar hinter dem Task-Namen beginnt, sodass der
    /// Name als Kopf der eingeklappten Region sichtbar bleibt. Task-Definitionen ohne Namen werden
    /// übersprungen.
    /// </summary>
    /// <param name="syntaxTreeAndSnapshot">Syntaxbaum samt zugehörigem <see cref="ITextSnapshot"/>.</param>
    /// <param name="tagCreator">Fabrik für die <see cref="IOutliningRegionTag"/>-Instanzen.</param>
    public static IEnumerable<ITagSpan<IOutliningRegionTag>> GetTags(SyntaxTreeAndSnapshot syntaxTreeAndSnapshot, IOutliningRegionTagCreator tagCreator) {

        foreach (var taskDef in syntaxTreeAndSnapshot.SyntaxTree.Root.DescendantNodes<TaskDefinitionSyntax>()) {

            var extent = taskDef.Extent;

            var nameToken = taskDef.Identifier;
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

            var regionSpan = new SnapshotSpan(new SnapshotPoint(syntaxTreeAndSnapshot.Snapshot, start),        length);
            var hintSpan   = new SnapshotSpan(new SnapshotPoint(syntaxTreeAndSnapshot.Snapshot, extent.Start), extent.Length);
            var tag        = tagCreator.CreateTag("...", hintSpan);

            yield return new TagSpan<IOutliningRegionTag>(regionSpan, tag);
        }
    }
}
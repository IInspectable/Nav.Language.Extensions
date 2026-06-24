#region Using Directives

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Classification;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Tagging;

using Pharmatechnik.Nav.Language.Text;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.Classification; 

sealed class SemanticClassificationTagger: SemanticModelServiceDependent, ITagger<IClassificationTag> {

    SemanticClassificationTagger(IClassificationTypeRegistryService classificationTypeRegistryService, ITextBuffer textBuffer): base(textBuffer) {
        ClassificationTypeRegistryService = classificationTypeRegistryService;
    }

    public static SemanticClassificationTagger Create(IClassificationTypeRegistryService classificationTypeRegistryService, ITextBuffer textBuffer) {
        return new SemanticClassificationTagger(classificationTypeRegistryService, textBuffer);
    }

    public IClassificationTypeRegistryService ClassificationTypeRegistryService { get; }

    public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

    public IEnumerable<ITagSpan<IClassificationTag>> GetTags(NormalizedSnapshotSpanCollection spans) {

        var codeGenerationUnitAndSnapshot = SemanticModelService.CodeGenerationUnitAndSnapshot;
        if (codeGenerationUnitAndSnapshot == null) {
            yield break;
        }

        foreach (var span in spans) {

            // Dead Code
            foreach (var deadCode in BuildClassificationSpan(
                         textExtents                  : GetDeadCodeExtents(codeGenerationUnitAndSnapshot.CodeGenerationUnit),
                         classificationType           : ClassificationTypeRegistryService.GetClassificationType(ClassificationTypeNames.DeadCode),
                         range                        : span,
                         codeGenerationUnitAndSnapshot: codeGenerationUnitAndSnapshot)) {

                yield return deadCode;
            }

            // C# Code
            foreach (var csCode in GetCSharpCodeClassifications(span, codeGenerationUnitAndSnapshot)) {
                yield return csCode;
            }

            // Semantic Highlighting
            var advancedOptions = NavLanguagePackage.AdvancedOptions;
            if (advancedOptions.SemanticHighlighting) {

                // Init Nodes
                foreach (var initNode in BuildClassificationSpan(
                             textExtents                  : GetInitNodeExtents(codeGenerationUnitAndSnapshot.CodeGenerationUnit),
                             classificationType           : ClassificationTypeRegistryService.GetClassificationType(ClassificationTypeNames.ConnectionPoint),
                             range                        : span,
                             codeGenerationUnitAndSnapshot: codeGenerationUnitAndSnapshot)) {

                    yield return initNode;
                }

                // Exit Nodes
                foreach (var exitNode in BuildClassificationSpan(
                             textExtents                  : GetExitNodeExtents(codeGenerationUnitAndSnapshot.CodeGenerationUnit),
                             classificationType           : ClassificationTypeRegistryService.GetClassificationType(ClassificationTypeNames.ConnectionPoint),
                             range                        : span,
                             codeGenerationUnitAndSnapshot: codeGenerationUnitAndSnapshot)) {

                    yield return exitNode;
                }

                // Choice Nodes
                foreach (var choiceNode in BuildClassificationSpan(
                             textExtents                  : GetChoiceNodeExtents(codeGenerationUnitAndSnapshot.CodeGenerationUnit),
                             classificationType           : ClassificationTypeRegistryService.GetClassificationType(ClassificationTypeNames.ChoiceNode),
                             range                        : span,
                             codeGenerationUnitAndSnapshot: codeGenerationUnitAndSnapshot)) {

                    yield return choiceNode;
                }

                // Gui Nodes
                foreach (var guiNode in BuildClassificationSpan(
                             textExtents                  : GetGuiNodeExtents(codeGenerationUnitAndSnapshot.CodeGenerationUnit),
                             classificationType           : ClassificationTypeRegistryService.GetClassificationType(ClassificationTypeNames.GuiNode),
                             range                        : span,
                             codeGenerationUnitAndSnapshot: codeGenerationUnitAndSnapshot)) {

                    yield return guiNode;
                }
            }
        }
    }

    protected override void OnSemanticModelChanged(object sender, SnapshotSpanEventArgs e) {
        TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(e.Span));
    }

    static IEnumerable<ITagSpan<IClassificationTag>> BuildClassificationSpan(IEnumerable<TextExtent> textExtents, IClassificationType classificationType, SnapshotSpan range, CodeGenerationUnitAndSnapshot codeGenerationUnitAndSnapshot) {

        var rangeExtent = TextExtent.FromBounds(range.Start.Position, range.End.Position);

        foreach (var extent in textExtents.Where(e => !e.IsMissing)) {

            if (!extent.IntersectsWith(rangeExtent)) {
                continue;
            }

            var tokenSpan = new SnapshotSpan(codeGenerationUnitAndSnapshot.Snapshot, new Span(extent.Start, extent.Length));
            var tagSpan   = tokenSpan.TranslateTo(range.Snapshot, SpanTrackingMode.EdgeExclusive);
            var tag       = new ClassificationTag(classificationType);

            yield return new TagSpan<IClassificationTag>(tagSpan, tag);

        }
    }

    static IEnumerable<TextExtent> GetDeadCodeExtents(CodeGenerationUnit codeGenerationUnit) {
        var diagnostics = codeGenerationUnit.Diagnostics;
        var candidates  = diagnostics.Where(diagnostic => diagnostic.Category == DiagnosticCategory.DeadCode);
        return candidates.SelectMany(diag => diag.GetLocations()).Select(loc => loc.Extent);
    }

    static IEnumerable<TextExtent> GetChoiceNodeExtents(CodeGenerationUnit codeGenerationUnit) {
        var choiceNodes = codeGenerationUnit.Symbols.OfType<IChoiceNodeSymbol>();
        foreach (var choiceNode in choiceNodes) {
            yield return choiceNode.Syntax.Identifier.Extent;

            foreach (var sourceNode in choiceNode.Outgoings.Select(trans => trans.SourceReference).Where(source => source != null)) {
                yield return sourceNode.Location.Extent;
            }

            foreach (var targetNode in choiceNode.Incomings.Select(trans => trans.TargetReference).Where(target => target != null)) {
                yield return targetNode.Location.Extent;
            }
        }
    }

    static IEnumerable<TextExtent> GetGuiNodeExtents(CodeGenerationUnit codeGenerationUnit) {
        var viewNodes = codeGenerationUnit.Symbols.OfType<IViewNodeSymbol>();
        foreach (var viewNode in viewNodes) {
            yield return viewNode.Syntax.Identifier.Extent;

            foreach (var sourceNode in viewNode.Outgoings.Select(trans => trans.SourceReference).Where(source => source != null)) {
                yield return sourceNode.Location.Extent;
            }

            foreach (var targetNode in viewNode.Incomings.Select(trans => trans.TargetReference).Where(target => target != null)) {
                yield return targetNode.Location.Extent;
            }
        }

        var dialogNodes = codeGenerationUnit.Symbols.OfType<IDialogNodeSymbol>();
        foreach (var dialogNode in dialogNodes) {
            yield return dialogNode.Syntax.Identifier.Extent;

            foreach (var sourceNode in dialogNode.Outgoings.Select(trans => trans.SourceReference).Where(source => source != null)) {
                yield return sourceNode.Location.Extent;
            }

            foreach (var targetNode in dialogNode.Incomings.Select(trans => trans.TargetReference).Where(target => target != null)) {
                yield return targetNode.Location.Extent;
            }
        }
    }

    static IEnumerable<TextExtent> GetInitNodeExtents(CodeGenerationUnit codeGenerationUnit) {
        var initNodes = codeGenerationUnit.Symbols.OfType<IInitNodeSymbol>();
        foreach (var initNode in initNodes) {
            if (initNode.Alias != null) {
                yield return initNode.Alias.Location.Extent;
            }

            foreach (var sourceNode in initNode.Outgoings.Select(trans => trans.SourceReference).Where(source => source != null)) {
                yield return sourceNode.Location.Extent;
            }
        }
    }

    static IEnumerable<TextExtent> GetExitNodeExtents(CodeGenerationUnit codeGenerationUnit) {
        var exitNodes = codeGenerationUnit.Symbols.OfType<IExitNodeSymbol>();
        foreach (var exitNode in exitNodes) {
            yield return exitNode.Syntax.Identifier.Extent;

            foreach (var targetNode in exitNode.Incomings.Select(trans => trans.TargetReference).Where(target => target != null)) {
                yield return targetNode.Location.Extent;
            }
        }

        foreach (var taskNode in codeGenerationUnit.Symbols.OfType<ITaskNodeSymbol>()) {
            foreach (var cpRef in taskNode.Outgoings.Select(trans => trans.ExitConnectionPointReference).Where(cpRef => cpRef != null)) {
                yield return cpRef.Location.Extent;
            }
        }
    }

    IEnumerable<ITagSpan<IClassificationTag>> GetCSharpCodeClassifications(SnapshotSpan range, CodeGenerationUnitAndSnapshot codeGenerationUnitAndSnapshot) {

        var codeExtents = GetCodeExtents(codeGenerationUnitAndSnapshot.CodeGenerationUnit);
        var rangeExtent = TextExtent.FromBounds(range.Start.Position, range.End.Position);

        foreach (var extent in codeExtents.Where(e => !e.IsMissing)) {

            if (!extent.IntersectsWith(rangeExtent)) {
                continue;
            }

            var codeSpan        = new Span(extent.Start, extent.Length);
            var source          = codeGenerationUnitAndSnapshot.Snapshot.GetText(codeSpan);
            var sourceText      = Microsoft.CodeAnalysis.Text.SourceText.From(source);
            var classifiedSpans = ThreadHelper.JoinableTaskFactory.Run(() => ClassifyCSharpCodeAsync(sourceText));

            foreach (var classifiedSpan in classifiedSpans) {

                var tokenSpan = new SnapshotSpan(codeGenerationUnitAndSnapshot.Snapshot, new Span(
                                                     start: classifiedSpan.TextSpan.Start + codeSpan.Start,
                                                     length: classifiedSpan.TextSpan.Length));

                var tagSpan = tokenSpan.TranslateTo(range.Snapshot, SpanTrackingMode.EdgeExclusive);

                var classificationType = ClassificationTypeRegistryService.GetClassificationType(classifiedSpan.ClassificationType);
                var tag                = new ClassificationTag(classificationType);

                yield return new TagSpan<IClassificationTag>(tagSpan, tag);
            }

        }
    }

    static IEnumerable<TextExtent> GetCodeExtents(CodeGenerationUnit codeGenerationUnit) {
        return codeGenerationUnit.Syntax.DescendantNodes<CodeDeclarationSyntax>().SelectMany(cds=> cds.GetGetStringLiterals())
                                 .Select(n => TextExtent.FromBounds(n.Extent.Start +1, n.Extent.End -1));            
    }

    static async Task<IEnumerable<ClassifiedSpan>> ClassifyCSharpCodeAsync(Microsoft.CodeAnalysis.Text.SourceText sourceText) {

        var workspace    = new AdhocWorkspace();
        var projName     = "AdHocClassification";
        var projectId    = ProjectId.CreateNewId();
        var versionStamp = Microsoft.CodeAnalysis.VersionStamp.Create();
        var projectInfo  = ProjectInfo.Create(projectId, versionStamp, projName, projName, LanguageNames.CSharp);
        var newProject   = workspace.AddProject(projectInfo);
        var newDocument  = workspace.AddDocument(newProject.Id, "Code.cs", sourceText);

        var classifiedSpans = await Classifier.GetClassifiedSpansAsync(newDocument, new TextSpan(0, sourceText.Length)).ConfigureAwait(false);

        return classifiedSpans;
    }
}
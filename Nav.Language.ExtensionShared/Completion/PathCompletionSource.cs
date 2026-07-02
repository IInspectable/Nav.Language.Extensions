#region Using Directives

using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion.Data;
using Microsoft.VisualStudio.Text;

using Pharmatechnik.Nav.Language.Extension.QuickInfo;
using Pharmatechnik.Nav.Language.Text;
using Pharmatechnik.Nav.Utilities.IO;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.Completion; 

class PathCompletionSource: AsyncCompletionSource {

    public PathCompletionSource(QuickinfoBuilderService quickinfoBuilderService)
        : base(quickinfoBuilderService) {

    }

    public override CompletionStartData InitializeCompletion(CompletionTrigger trigger, SnapshotPoint triggerLocation, CancellationToken token) {

        if (!ShouldTriggerCompletion(trigger)) {
            return CompletionStartData.DoesNotParticipateInCompletion;
        }

        if (ShouldProvideCompletions(triggerLocation, out var applicableToSpan, out _)) {
            return new CompletionStartData(CompletionParticipation.ProvidesItems, applicableToSpan);
        }

        return CompletionStartData.DoesNotParticipateInCompletion;

    }

    private const string ParentFolderDisplayString = "..";

    public override async Task<CompletionContext> GetCompletionContextAsync(IAsyncCompletionSession session, CompletionTrigger trigger, SnapshotPoint triggerLocation, SnapshotSpan applicableToSpan, CancellationToken token) {

        if (!ShouldProvideCompletions(triggerLocation, out var myApplicableToSpan, out var replacementSpan) ||
            myApplicableToSpan != applicableToSpan) {
            return CreateEmptyCompletionContext();
        }

        var semanticModelService      = SemanticModelService.GetOrCreateSingelton(triggerLocation.Snapshot.TextBuffer);
        var generationUnitAndSnapshot = semanticModelService.CodeGenerationUnitAndSnapshot;
        if (generationUnitAndSnapshot == null) {
            return CreateEmptyCompletionContext();
        }

        var codeGenerationUnit = generationUnitAndSnapshot.CodeGenerationUnit;

        var line         = triggerLocation.GetContainingLine();
        var linePosition = triggerLocation - line.Start;
        var lineText     = line.GetText();

        var completionItems = ImmutableArray.CreateBuilder<CompletionItem>();

        var quotedExtent       = lineText.QuotedExtent(linePosition);
        var previousIdentifier = lineText.GetPreviousIdentifier(quotedExtent.Start - 1);

        // Es gibt derzeit eigentlich nur die taskrefs wo innerhalb von "" etwas vorgeschlagen werden kann
        if (previousIdentifier == SyntaxFacts.TaskrefKeyword) {

            var navDirectory = codeGenerationUnit.Syntax.SyntaxTree.SourceText.FileInfo?.Directory;

            if (navDirectory != null) {

                // "typed" ist alles links vom Caret bis zum "
                var typed = lineText.Substring(quotedExtent.Start, length: linePosition - quotedExtent.Start);
                var parts = SplitPath(typed);

                var solution = await NavLanguagePackage.GetSolutionAsync(token);
                // Wenn der Benutzer gerade anfängt einen Dateinamen anzugeben, er aber noch keinen Pfad geschrieben hat, dann zeigen wir
                // ALLE nav-Files, die von der Solution aus zu erreichen sind.
                if (String.IsNullOrWhiteSpace(parts.DirPart)) {
                    foreach (var file in solution.SolutionFiles) {
                        completionItems.Add(CreateFileInfoCompletion(navDirectory, file, replacementSpan: replacementSpan));
                    }
                }

                // Es wurden noch keine Nav-Files vorgeschlagen:
                // - entweder der Benutzer hat schon einen Pfad angegeben, z.B. "..\
                // - oder es gibt keine Solution
                // - oder es sind keine Nav-File von der Solution aus erreichbar
                if (!completionItems.Any()) {

                    var searchDirectory = navDirectory;

                    // Der Benutzer hat schon angefangen, eine Pfad zu schreiben, als z.B. "..\
                    if (!String.IsNullOrWhiteSpace(parts.DirPart)) {

                        // Wenn der Pfad absolut ist (z.B. "c:\), nehmen wir direkt dieses Verzeichnis als Suchverzeichnis
                        if (PathHelper.TryGetIsPathRooted(parts.DirPart) == true) {
                            PathHelper.TryGetDirectoryinfo(parts.DirPart, out searchDirectory);
                            // Andernfalls stellen wir das Verzeichnis des aktuellen Nav-Files voran.
                        } else if (PathHelper.TryCombinePath(navDirectory.FullName, parts.DirPart, out var fullPath)) {
                            PathHelper.TryGetDirectoryinfo(fullPath, out searchDirectory);
                        }
                    }

                    if (searchDirectory != null) {

                        // 1. Sofern das Verzeichnis ein übergeordnetes Verzeichnis hat, '..' als erste Auswahl anbieten.
                        if (searchDirectory.Parent != null) {
                            completionItems.Add(CreateDirectoryInfoCompletion(navDirectory, searchDirectory.Parent,
                                                                              displayText: ParentFolderDisplayString,
                                                                              icon: CompletionImages.ParentFolder,
                                                                              replacementSpan: replacementSpan));
                        }

                        // 2. jetzt alle Verzeichnisse anzeigen
                        foreach (var dir in searchDirectory.TryEnumerateDirectories()) {
                            completionItems.Add(CreateDirectoryInfoCompletion(navDirectory, dir, replacementSpan: replacementSpan));
                        }

                        // 3. und am Ende die Nav-Files im Suchverzeichnis
                        foreach (var file in searchDirectory.TryEnumerateFiles(searchPattern: $"*{NavLanguageContentDefinitions.FileExtension}",
                                                                               searchOption: SearchOption.TopDirectoryOnly)) {

                            completionItems.Add(CreateFileInfoCompletion(navDirectory, file, replacementSpan: replacementSpan));
                        }
                    }
                }
            }
        }

        // Wenn das Parent Directory der einzige Vorschlag ist, dann entfernen wir auch diesen, das ansonsten automatisch zum übergeordneten
        // Verzeichnis gesprungen wird, wenn die AutoCompletion z.B. mittel Ctrl + Leer getriggert wird.
        if (completionItems.Count == 1 && completionItems[0].DisplayText == ParentFolderDisplayString) {
            completionItems.Clear();
        }

        return CreateCompletionContext(completionItems);

    }

    bool ShouldProvideCompletions(SnapshotPoint triggerLocation, out SnapshotSpan applicableToSpan, out ITrackingSpan replacementSpan) {

        applicableToSpan = default;
        replacementSpan  = default;

        var line         = triggerLocation.GetContainingLine();
        var linePosition = triggerLocation - line.Start;
        var lineText     = line.GetText();

        if (lineText.IsInQuotation(linePosition)) {

            var quotedExtent       = lineText.QuotedExtent(linePosition);
            var previousIdentifier = lineText.GetPreviousIdentifier(quotedExtent.Start - 1);

            if (previousIdentifier == SyntaxFacts.TaskrefKeyword) {

                applicableToSpan = new SnapshotSpan(
                    line.GetStartOfFileNamePart(triggerLocation),
                    triggerLocation);

                // Wir ersetzten grundsätzlich alles zwischen den ""
                replacementSpan = applicableToSpan.Snapshot.CreateTrackingSpan(
                    new SnapshotSpan(
                        line.Start + quotedExtent.Start,
                        line.Start + quotedExtent.End),
                    SpanTrackingMode.EdgeInclusive);

                return true;
            }

        }

        return false;

    }

    (string DirPart, string FilePart) SplitPath(string path) {

        var dirPart  = "";
        var filePath = "";

        if (!string.IsNullOrEmpty(path)) {

            var index = path.Length;
            while (index > 0) {

                char ch = path[--index];

                if (ch == Path.DirectorySeparatorChar ||
                    ch == Path.AltDirectorySeparatorChar) {

                    dirPart  = path.Substring(0, length: index + 1);
                    filePath = path.Substring(index            + 1, length: path.Length - index - 1);

                    break;
                }
            }
        }

        return (DirPart: dirPart, FilePart: filePath);
    }

}
#region Using Directives

using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Imaging.Interop;

using Pharmatechnik.Nav.Language.Extension.Images;
using Pharmatechnik.Nav.Language.CodeAnalysis.Annotation;
using Pharmatechnik.Nav.Language.CodeAnalysis.FindSymbols;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.GoToLocation.Provider;

/// <summary>
/// Liefert für eine <c>{Choice}Logic</c>-Methode (NavChoice) die C#-Aufrufstellen der zugehörigen
/// <c>{Choice}(…)</c>-Forwards (<c>next.{Choice}(…)</c>). Die Suche erfasst die gesamte WFS-Klasse, also
/// auch <c>partial</c>-Deklarationen in anderen Dateien.
/// </summary>
class NavChoiceCallerLocationInfoProvider: CodeAnalysisLocationInfoProvider {

    readonly NavChoiceAnnotation _choiceAnnotation;

    public NavChoiceCallerLocationInfoProvider(ITextBuffer sourceBuffer,
                                               NavChoiceAnnotation choiceAnnotation): base(sourceBuffer) {
        _choiceAnnotation = choiceAnnotation;
    }

    static ImageMoniker ImageMoniker => ImageMonikers.GoToNodeDeclaration;

    protected override Task<IEnumerable<LocationInfo>> GetLocationsAsync(Project project, CancellationToken cancellationToken) {

        return Task.Run<IEnumerable<LocationInfo>>(async () => {

            var document = SourceBuffer.CurrentSnapshot.GetOpenDocumentInCurrentContextWithChanges();
            if (document == null) {
                return System.Array.Empty<LocationInfo>();
            }

            var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
            if (semanticModel == null) {
                return System.Array.Empty<LocationInfo>();
            }

            var root = await semanticModel.SyntaxTree.GetRootAsync(cancellationToken).ConfigureAwait(false);

            // Die Klasse, die die {Choice}Logic-Methode enthält, im aktuellen Tree wiederfinden, um ein
            // verlässliches Symbol (mit allen partiellen Deklarationen) zu erhalten.
            var classDeclaration = root.DescendantNodesAndSelf()
                                       .OfType<ClassDeclarationSyntax>()
                                       .FirstOrDefault(c => c.Identifier.ValueText == _choiceAnnotation.ClassDeclarationSyntax.Identifier.ValueText);
            if (classDeclaration == null) {
                return System.Array.Empty<LocationInfo>();
            }

            if (semanticModel.GetDeclaredSymbol(classDeclaration, cancellationToken) is not INamedTypeSymbol classSymbol) {
                return System.Array.Empty<LocationInfo>();
            }

            // Alle Dokumente, in denen die (ggf. partielle) Klasse deklariert ist.
            var documents = classSymbol.DeclaringSyntaxReferences
                                       .Select(reference => project.Solution.GetDocument(reference.SyntaxTree))
                                       .Where(doc => doc != null)
                                       .GroupBy(doc => doc.Id)
                                       .Select(group => group.First());

            var infos = new List<LocationInfo>();
            foreach (var doc in documents) {

                cancellationToken.ThrowIfCancellationRequested();

                var choiceCalls = AnnotationReader.ReadNavTaskAnnotations(doc)
                                                  .OfType<NavChoiceCallAnnotation>()
                                                  .Where(call => call.TaskName    == _choiceAnnotation.TaskName    &&
                                                                 call.NavFileName == _choiceAnnotation.NavFileName &&
                                                                 call.ChoiceName  == _choiceAnnotation.ChoiceName);

                foreach (var call in choiceCalls) {

                    var location = LocationFinder.ToLocation(call.Identifier.GetLocation());
                    if (location == null) {
                        continue;
                    }

                    infos.Add(LocationInfo.FromLocation(
                                  location    : location,
                                  displayName : $"{call.Identifier.Identifier.Text} (Zeile {location.StartLine + 1})",
                                  imageMoniker: ImageMoniker));
                }
            }

            return infos.OrderBy(info => info.Location?.FilePath)
                        .ThenBy(info => info.Location?.Start)
                        .ToArray();

        }, cancellationToken);
    }
}

#region Using Directives

using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

using Microsoft.CodeAnalysis;
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

    /// <summary>Bindet den Provider an <paramref name="sourceBuffer"/> und die <c>{Choice}Logic</c>-Annotation <paramref name="choiceAnnotation"/>.</summary>
    public NavChoiceCallerLocationInfoProvider(ITextBuffer sourceBuffer,
                                               NavChoiceAnnotation choiceAnnotation): base(sourceBuffer) {
        _choiceAnnotation = choiceAnnotation;
    }

    static ImageMoniker ImageMoniker => ImageMonikers.GoToNodeDeclaration;

    protected override async Task<IEnumerable<LocationInfo>> GetLocationsAsync(Project project, CancellationToken cancellationToken) {

        var classSymbol = await FindContainingClassSymbolAsync(
            _choiceAnnotation.ClassDeclarationSyntax.Identifier.ValueText, cancellationToken).ConfigureAwait(false);
        if (classSymbol == null) {
            return System.Array.Empty<LocationInfo>();
        }

        var callers = await LocationFinder.FindCallerLocations(
            project,
            classSymbol,
            call => call is NavChoiceCallAnnotation choiceCall             &&
                    choiceCall.TaskName    == _choiceAnnotation.TaskName    &&
                    choiceCall.NavFileName == _choiceAnnotation.NavFileName &&
                    choiceCall.ChoiceName  == _choiceAnnotation.ChoiceName,
            cancellationToken).ConfigureAwait(false);

        return callers.Select(caller =>
                                  LocationInfo.FromLocation(
                                      location    : caller,
                                      displayName : $"{caller.CallerName} (Zeile {caller.StartLine + 1})",
                                      imageMoniker: ImageMoniker));
    }
}

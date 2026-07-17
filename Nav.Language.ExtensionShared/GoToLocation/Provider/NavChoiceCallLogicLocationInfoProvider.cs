#region Using Directives

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
/// Liefert für eine Choice-Aufrufstelle (<c>next.{Choice}(…)</c>, <c>NavChoiceCall</c>) die C#-Implementierung
/// der geteilten <c>{Choice}Logic</c>-Entscheidungsmethode (Abstieg von der <c>{Task}WFSBase</c> auf die
/// abgeleitete Nutzer-Klasse).
/// </summary>
class NavChoiceCallLogicLocationInfoProvider: CodeAnalysisLocationInfoProvider {

    readonly NavChoiceCallAnnotation _callAnnotation;

    /// <summary>Bindet den Provider an <paramref name="sourceBuffer"/> und die Choice-Aufrufstellen-Annotation <paramref name="callAnnotation"/>.</summary>
    public NavChoiceCallLogicLocationInfoProvider(ITextBuffer sourceBuffer,
                                                  NavChoiceCallAnnotation callAnnotation): base(sourceBuffer) {
        _callAnnotation = callAnnotation;
    }

    static ImageMoniker ImageMoniker => ImageMonikers.GoToMethodPublic;

    protected override async Task<IEnumerable<LocationInfo>> GetLocationsAsync(Project project, CancellationToken cancellationToken) {

        try {
            var location = await LocationFinder.FindCallChoiceLogicDeclarationLocationAsync(
                project             : project,
                choiceCallAnnotation: _callAnnotation,
                cancellationToken   : cancellationToken).ConfigureAwait(false);

            return ToEnumerable(LocationInfo.FromLocation(
                                    location    : location,
                                    displayName : $"{_callAnnotation.ChoiceName}Logic",
                                    imageMoniker: ImageMoniker));

        } catch (LocationNotFoundException ex) {
            return ToEnumerable(LocationInfo.FromError(ex, ImageMoniker));
        }
    }
}

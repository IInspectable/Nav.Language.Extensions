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

class NavInitCallLocationInfoProvider: CodeAnalysisLocationInfoProvider {

    readonly NavInitCallAnnotation          _callAnnotation;
    readonly IEnumerable<NavExitAnnotation> _exitAnnotations;

    public NavInitCallLocationInfoProvider(ITextBuffer sourceBuffer,
                                           NavInitCallAnnotation callAnnotation,
                                           IEnumerable<NavExitAnnotation> exitAnnotations): base(sourceBuffer) {
        _callAnnotation  = callAnnotation;
        _exitAnnotations = exitAnnotations;
    }

    static ImageMoniker ImageMoniker => ImageMonikers.GoToMethodPublic;

    protected override async Task<IEnumerable<LocationInfo>> GetLocationsAsync(Project project, CancellationToken cancellationToken) {

        LocationInfo beginLocationInfo;
        try {
            var location = await LocationFinder.FindCallBeginLogicDeclarationLocationsAsync(
                project           : project,
                initCallAnnotation: _callAnnotation,
                cancellationToken : cancellationToken).ConfigureAwait(false);

            beginLocationInfo = LocationInfo.FromLocation(
                location          : location,
                displayName       : "BeginLogic",
                imageMoniker      : ImageMoniker);

        } catch (LocationNotFoundException ex) {
            beginLocationInfo = LocationInfo.FromError(ex, ImageMoniker);
        }

        // Zweites Ziel: die zugehörige After{Node}-Rücksprungmethode. Die Zuordnung (Begin-Prefix
        // abstreifen, passende <NavExit>-Annotation suchen) macht der LocationFinder VS-frei; fehlt sie,
        // bleibt es beim BeginLogic-Ziel.
        var afterLocation = LocationFinder.FindInitCallAfterLocation(_callAnnotation, _exitAnnotations);
        if (afterLocation == null) {
            return ToEnumerable(beginLocationInfo);
        }

        var afterLocationInfo = LocationInfo.FromLocation(
            location    : afterLocation,
            displayName : afterLocation.CallerName,
            imageMoniker: ImageMoniker);

        return new[] {beginLocationInfo, afterLocationInfo};

    }

}
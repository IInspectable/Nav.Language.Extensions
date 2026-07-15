#region Using Directives

using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

using Microsoft.VisualStudio.Imaging.Interop;
using Pharmatechnik.Nav.Language.CodeAnalysis.Annotation;
using Pharmatechnik.Nav.Language.CodeAnalysis.FindSymbols;
using Pharmatechnik.Nav.Language.Extension.Images;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.GoToLocation.Provider; 

/// <summary>
/// Springt von einer <c>NavExit</c>-Annotation im generierten C#-Code zum zugehörigen Exit-ConnectionPoint
/// in der <c>.nav</c>-Quelle. Angezeigt wird „Exit {Name}" mit dem Exit-ConnectionPoint-Icon.
/// </summary>
class NavExitAnnotationLocationInfoProvider : NavAnnotationLocationInfoProvider<NavExitAnnotation> {

    /// <summary>Bindet den Provider an die <paramref name="annotation"/> des Exit-ConnectionPoints.</summary>
    public NavExitAnnotationLocationInfoProvider(NavExitAnnotation annotation) : base(annotation) {
    }

    static ImageMoniker ImageMoniker => ImageMonikers.ExitConnectionPoint;

    protected override async Task<IEnumerable<LocationInfo>> GetLocationsAsync(string sourceText, CancellationToken cancellationToken = new()) {

        try {

            var locs = await LocationFinder.FindNavLocationsAsync(
                sourceText       : sourceText,
                annotation       : Annotation,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            return locs.Select(location => LocationInfo.FromLocation(
                                   location    : location,
                                   displayName : $"Exit {location.Name}",
                                   imageMoniker: ImageMoniker));

        } catch (LocationNotFoundException ex) {
            return ToEnumerable(LocationInfo.FromError(ex, ImageMoniker));
        }
    }
}
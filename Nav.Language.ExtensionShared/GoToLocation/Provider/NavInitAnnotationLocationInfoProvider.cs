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
/// Springt von einer <c>NavInit</c>-Annotation im generierten C#-Code zum zugehörigen Init-ConnectionPoint
/// in der <c>.nav</c>-Quelle. Angezeigt wird der Init-Name mit dem Init-ConnectionPoint-Icon.
/// </summary>
class NavInitAnnotationLocationInfoProvider : NavAnnotationLocationInfoProvider<NavInitAnnotation> {

    /// <summary>Bindet den Provider an die <paramref name="annotation"/> des Init-ConnectionPoints.</summary>
    public NavInitAnnotationLocationInfoProvider(NavInitAnnotation annotation) : base(annotation) {
    }

    static ImageMoniker ImageMoniker { get { return ImageMonikers.InitConnectionPoint; } }

    protected override async Task<IEnumerable<LocationInfo>> GetLocationsAsync(string sourceText, CancellationToken cancellationToken = new()) {

        try {

            var locs = await LocationFinder.FindNavLocationsAsync(
                sourceText       : sourceText,
                annotation       : Annotation,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            return locs.Select(location => LocationInfo.FromLocation(
                                   location    : location,
                                   displayName : Annotation.InitName,
                                   imageMoniker: ImageMoniker));

        } catch (LocationNotFoundException ex) {
            return ToEnumerable(LocationInfo.FromError(ex, ImageMoniker));
        }
    }
}
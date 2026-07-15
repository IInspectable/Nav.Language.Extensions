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
/// Springt von einer Choice-Aufrufstelle (<c>next.{Choice}(…)</c>, <c>NavChoiceCall</c>) im generierten
/// C#-Code zum zugehörigen Choice-Knoten in der <c>.nav</c>-Quelle. Angezeigt wird der Choice-Name mit dem
/// ChoiceNode-Icon.
/// </summary>
class NavChoiceCallLocationInfoProvider : NavAnnotationLocationInfoProvider<NavChoiceCallAnnotation> {

    /// <summary>Bindet den Provider an die <paramref name="annotation"/> der Choice-Aufrufstelle.</summary>
    public NavChoiceCallLocationInfoProvider(NavChoiceCallAnnotation annotation) : base(annotation) {
    }

    static ImageMoniker ImageMoniker { get { return ImageMonikers.ChoiceNode; } }

    protected override async Task<IEnumerable<LocationInfo>> GetLocationsAsync(string sourceText, CancellationToken cancellationToken = new()) {

        try {

            var locs = await LocationFinder.FindNavLocationsAsync(
                sourceText       : sourceText,
                annotation       : Annotation,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            return locs.Select(location => LocationInfo.FromLocation(
                                   location    : location,
                                   displayName : Annotation.ChoiceName,
                                   imageMoniker: ImageMoniker));

        } catch (LocationNotFoundException ex) {
            return ToEnumerable(LocationInfo.FromError(ex, ImageMoniker));
        }
    }
}
